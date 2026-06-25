namespace SmartWsdlKit
{
	/// <summary>
	/// Represents the state of the circuit breaker.
	/// </summary>
	public enum CircuitState
	{
		Closed,
		Open,
		HalfOpen
	}

	/// <summary>
	/// Exception thrown when the circuit breaker is open.
	/// </summary>
	public class CircuitBreakerOpenException : SoapException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="CircuitBreakerOpenException"/> class.
		/// </summary>
		public CircuitBreakerOpenException(string message) : base(message) { }
	}

	/// <summary>
	/// Thread-safe lightweight resilience engine for retrying and circuit breaking.
	/// </summary>
	public class SoapResilienceEngine
	{
		private readonly object _lock = new object();
		private CircuitState _state = CircuitState.Closed;
		private int _failureCount = 0;
		private DateTime _lastStateTransition = DateTime.MinValue;

		private readonly int _failureThreshold;
		private readonly TimeSpan _resetTimeout;

		/// <summary>
		/// Gets the current state of the circuit breaker.
		/// </summary>
		public CircuitState State
		{
			get
			{
				lock (_lock)
				{
					UpdateState();
					return _state;
				}
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SoapResilienceEngine"/> class.
		/// </summary>
		public SoapResilienceEngine(int failureThreshold, TimeSpan resetTimeout)
		{
			_failureThreshold = failureThreshold;
			_resetTimeout = resetTimeout;
		}

		private void UpdateState()
		{
			if (_state == CircuitState.Open)
			{
				if (DateTime.UtcNow - _lastStateTransition > _resetTimeout)
				{
					_state = CircuitState.HalfOpen;
					_lastStateTransition = DateTime.UtcNow;
				}
			}
		}

		/// <summary>
		/// Records a successful request, resetting failures and closing the circuit.
		/// </summary>
		public void RecordSuccess()
		{
			lock (_lock)
			{
				_failureCount = 0;
				if (_state == CircuitState.HalfOpen)
				{
					_state = CircuitState.Closed;
					_lastStateTransition = DateTime.UtcNow;
				}
			}
		}

		/// <summary>
		/// Records a request failure, potentially tripping the circuit breaker.
		/// </summary>
		public void RecordFailure(Exception ex)
		{
			if (!IsTransientOrServerError(ex))
			{
				return; // Non-transient client errors do not trip the circuit
			}

			lock (_lock)
			{
				_failureCount++;
				if (_state == CircuitState.Closed && _failureCount >= _failureThreshold)
				{
					_state = CircuitState.Open;
					_lastStateTransition = DateTime.UtcNow;
				}
				else if (_state == CircuitState.HalfOpen)
				{
					_state = CircuitState.Open;
					_lastStateTransition = DateTime.UtcNow;
				}
			}
		}

		private bool IsTransientOrServerError(Exception ex)
		{
			if (ex is SoapFaultException faultEx)
			{
				var code = faultEx.FaultCode;
				// Soap 1.1 "Client" / Soap 1.2 "Sender" represent client input errors (not transient/service failures)
				if (!string.IsNullOrEmpty(code) &&
					(code.IndexOf("Client", StringComparison.OrdinalIgnoreCase) >= 0 ||
					 code.IndexOf("Sender", StringComparison.OrdinalIgnoreCase) >= 0))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Executes the specified asynchronous action with retries and circuit breaker checks.
		/// </summary>
		public async Task<T> ExecuteAsync<T>(
			Func<Task<T>> action,
			int retryCount,
			TimeSpan retryDelay,
			bool enableCircuitBreaker,
			CancellationToken cancellationToken)
		{
			if (enableCircuitBreaker)
			{
				lock (_lock)
				{
					UpdateState();
					if (_state == CircuitState.Open)
					{
						var resetTime = _lastStateTransition.Add(_resetTimeout);
						throw new CircuitBreakerOpenException($"Circuit breaker is OPEN. Request blocked. Reset will be attempted after {resetTime:yyyy-MM-dd HH:mm:ss} UTC.");
					}
				}
			}

			int attempt = 0;
			while (true)
			{
				try
				{
					var result = await action().ConfigureAwait(false);
					if (enableCircuitBreaker)
					{
						RecordSuccess();
					}
					return result;
				}
				catch (Exception ex)
				{
					if (enableCircuitBreaker)
					{
						RecordFailure(ex);
					}

					attempt++;
					if (attempt > retryCount || (enableCircuitBreaker && State == CircuitState.Open) || cancellationToken.IsCancellationRequested)
					{
						throw;
					}

					await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
				}
			}
		}
	}
}
