namespace SmartWsdlKit
{
	/// <summary>
	/// Holds diagnostic log details for SOAP service invocations.
	/// </summary>
	public class SoapDiagnosticLog
	{
		/// <summary>
		/// Gets the name of the operation called.
		/// </summary>
		public string OperationName { get; set; } = string.Empty;

		/// <summary>
		/// Gets the target endpoint URL.
		/// </summary>
		public string EndpointUrl { get; set; } = string.Empty;

		/// <summary>
		/// Gets the raw XML payload of the request.
		/// </summary>
		public string RequestXml { get; set; } = string.Empty;

		/// <summary>
		/// Gets the request HTTP and custom headers.
		/// </summary>
		public Dictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Gets the raw response content (XML or HTML/text in case of error).
		/// </summary>
		public string ResponseXml { get; set; } = string.Empty;

		/// <summary>
		/// Gets the response HTTP headers.
		/// </summary>
		public Dictionary<string, string> ResponseHeaders { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Gets the HTTP response status code.
		/// </summary>
		public int HttpStatusCode { get; set; }

		/// <summary>
		/// Gets the execution duration of the request in milliseconds.
		/// </summary>
		public double ExecutionTimeMs { get; set; }

		/// <summary>
		/// Gets whether the request was executed successfully without throwing an exception.
		/// </summary>
		public bool IsSuccess { get; set; }

		/// <summary>
		/// Gets the SOAP Fault code, if a fault was returned.
		/// </summary>
		public string? SoapFaultCode { get; set; }

		/// <summary>
		/// Gets the SOAP Fault string/reason, if a fault was returned.
		/// </summary>
		public string? SoapFaultString { get; set; }

		/// <summary>
		/// Gets the SOAP Fault detail XML, if a fault was returned.
		/// </summary>
		public string? SoapFaultDetail { get; set; }

		/// <summary>
		/// Gets the exception message if the request failed.
		/// </summary>
		public string? ErrorMessage { get; set; }

		/// <summary>
		/// Gets the timestamp when the request started.
		/// </summary>
		public DateTime Timestamp { get; set; } = DateTime.UtcNow;
	}
}
