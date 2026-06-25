namespace SmartWsdlKit
{
	/// <summary>
	/// Base exception for all SmartWsdlKit related errors.
	/// </summary>
	public class SoapException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SoapException"/> class.
		/// </summary>
		public SoapException() { }

		/// <summary>
		/// Initializes a new instance of the <see cref="SoapException"/> class with a specified error message.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public SoapException(string message) : base(message) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="SoapException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception.</param>
		public SoapException(string message, Exception innerException) : base(message, innerException) { }
	}

	/// <summary>
	/// Exception thrown when a SOAP service returns a SOAP Fault.
	/// </summary>
	public class SoapFaultException : SoapException
	{
		/// <summary>
		/// Gets the fault code (e.g., soap:Client, soap:Server, or Soap 1.2 codes like Value, Receiver).
		/// </summary>
		public string FaultCode { get; }

		/// <summary>
		/// Gets the fault string/reason describing the error.
		/// </summary>
		public string FaultString { get; }

		/// <summary>
		/// Gets the actor/role that caused the fault.
		/// </summary>
		public string? FaultActor { get; }

		/// <summary>
		/// Gets the detailed XML string of the fault, if provided.
		/// </summary>
		public string? DetailXml { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="SoapFaultException"/> class.
		/// </summary>
		public SoapFaultException(string faultCode, string faultString, string? faultActor, string? detailXml)
			: base($"SOAP Fault received. Code: '{faultCode}', Reason: '{faultString}'")
		{
			FaultCode = faultCode ?? string.Empty;
			FaultString = faultString ?? string.Empty;
			FaultActor = faultActor;
			DetailXml = detailXml;
		}
	}

	/// <summary>
	/// Exception thrown when parsing of WSDL fails.
	/// </summary>
	public class WsdlParseException : SoapException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="WsdlParseException"/> class.
		/// </summary>
		public WsdlParseException() { }

		/// <summary>
		/// Initializes a new instance of the <see cref="WsdlParseException"/> class with a specified error message.
		/// </summary>
		public WsdlParseException(string message) : base(message) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="WsdlParseException"/> class with a specified error message and inner exception.
		/// </summary>
		public WsdlParseException(string message, Exception innerException) : base(message, innerException) { }
	}

	/// <summary>
	/// Exception thrown when XML Schema validation fails.
	/// </summary>
	public class SchemaValidationException : SoapException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SchemaValidationException"/> class.
		/// </summary>
		public SchemaValidationException() { }

		/// <summary>
		/// Initializes a new instance of the <see cref="SchemaValidationException"/> class with a specified error message.
		/// </summary>
		public SchemaValidationException(string message) : base(message) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="SchemaValidationException"/> class with a specified error message and inner exception.
		/// </summary>
		public SchemaValidationException(string message, Exception innerException) : base(message, innerException) { }
	}
}
