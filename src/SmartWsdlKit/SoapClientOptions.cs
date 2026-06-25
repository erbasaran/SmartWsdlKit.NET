using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SmartWsdlKit
{
	/// <summary>
	/// Supported attachment encoding protocols.
	/// </summary>
	public enum SoapAttachmentMode
	{
		/// <summary>
		/// SOAP with Attachments (multipart/related where root part is text/xml or application/soap+xml).
		/// </summary>
		SwA,

		/// <summary>
		/// Message Transmission Optimization Mechanism (multipart/related where root part is application/xop+xml).
		/// </summary>
		Mtom
	}

	/// <summary>
	/// Configuration options for the SoapClient.
	/// </summary>
	public class SoapClientOptions
	{
		/// <summary>
		/// Gets or sets the base endpoint address of the SOAP service.
		/// </summary>
		public Uri? BaseAddress { get; set; }

		/// <summary>
		/// Gets or sets the HTTP request timeout. Default is 30 seconds.
		/// </summary>
		public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

		/// <summary>
		/// Gets or sets the number of retry attempts for failed requests. Default is 0.
		/// </summary>
		public int RetryCount { get; set; } = 0;

		/// <summary>
		/// Gets or sets the delay between retries. Default is 2 seconds.
		/// </summary>
		public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

		/// <summary>
		/// Gets or sets the HTTP proxy.
		/// </summary>
		public IWebProxy? Proxy { get; set; }

		/// <summary>
		/// Gets or sets the container used to store cookies.
		/// </summary>
		public CookieContainer Cookies { get; set; } = new CookieContainer();

		/// <summary>
		/// Gets or sets the User-Agent header value.
		/// </summary>
		public string UserAgent { get; set; } = "SmartWsdlKit/1.0";

		/// <summary>
		/// Gets or sets custom HTTP request headers.
		/// </summary>
		public Dictionary<string, string> CustomHeaders { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Gets or sets the SSL/TLS protocols to enable.
		/// </summary>
		public SslProtocols? SslProtocols { get; set; }

		/// <summary>
		/// Gets or sets the callback to validate server certificates. Useful for bypassing validation in development environments.
		/// </summary>
		public Func<System.Net.Http.HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>? ServerCertificateCustomValidationCallback { get; set; }

		/// <summary>
		/// Gets or sets the character encoding for the request payload. Default is UTF-8.
		/// </summary>
		public Encoding RequestEncoding { get; set; } = Encoding.UTF8;

		/// <summary>
		/// Gets or sets the character encoding used to parse responses if not specified by the content type. Default is UTF-8.
		/// </summary>
		public Encoding ResponseEncoding { get; set; } = Encoding.UTF8;

		/// <summary>
		/// Gets or sets a custom HttpMessageHandler for mock testing or specialized connection management.
		/// </summary>
		public System.Net.Http.HttpMessageHandler? BackchannelHandler { get; set; }

		/// <summary>
		/// Gets or sets the attachment encoding mode (SwA or MTOM). Default is SwA.
		/// </summary>
		public SoapAttachmentMode AttachmentMode { get; set; } = SoapAttachmentMode.SwA;

		/// <summary>
		/// Gets or sets whether custom lightweight retry and circuit breaker logic is enabled. Default is false.
		/// </summary>
		public bool EnableResilience { get; set; } = false;

		/// <summary>
		/// Gets or sets the consecutive failure threshold to open the circuit. Default is 5.
		/// </summary>
		public int CircuitBreakerFailureThreshold { get; set; } = 5;

		/// <summary>
		/// Gets or sets the duration the circuit breaker stays open before attempting reset. Default is 30 seconds.
		/// </summary>
		public TimeSpan CircuitBreakerResetTimeout { get; set; } = TimeSpan.FromSeconds(30);

		/// <summary>
		/// Gets or sets whether SOAP traffic diagnostics inspector logging is enabled. Default is false.
		/// </summary>
		public bool EnableDiagnostics { get; set; } = false;

		/// <summary>
		/// Gets or sets network credentials (e.g. for NTLM/Kerberos authentication).
		/// </summary>
		public ICredentials? Credentials { get; set; }
	}
}
