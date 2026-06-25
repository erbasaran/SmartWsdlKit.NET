using System.Collections;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("SmartWsdlKit.UnitTests")]

namespace SmartWsdlKit
{
	/// <summary>
	/// Type of password encoding for WS-Security UsernameToken.
	/// </summary>
	public enum WsSecurityPasswordType
	{
		/// <summary>
		/// Plaintext password (PasswordText).
		/// </summary>
		Plaintext,

		/// <summary>
		/// Hash password with Nonce and Created timestamp (PasswordDigest).
		/// </summary>
		PasswordDigest
	}

	/// <summary>
	/// Location of the API Key.
	/// </summary>
	public enum ApiKeyLocation
	{
		/// <summary>
		/// In the HTTP header.
		/// </summary>
		Header,

		/// <summary>
		/// In the query string.
		/// </summary>
		Query
	}

	/// <summary>
	/// A high-performance, thread-safe, async-first SOAP client.
	/// </summary>
	public class SoapClient : IDisposable
	{
		private HttpClient _httpClient;
		private readonly SoapClientOptions _options;
		private readonly WsdlDocument? _wsdl;
		private static readonly XNamespace Soap11Ns = "http://schemas.xmlsoap.org/soap/envelope/";
		private static readonly XNamespace Soap12Ns = "http://www.w3.org/2003/05/soap-envelope";
		private string? _basicAuthUsername;
		private string? _basicAuthPassword;
		private string? _bearerToken;
		private string? _apiKeyName;
		private string? _apiKeyValue;
		private ApiKeyLocation _apiKeyLocation = ApiKeyLocation.Header;
		private string? _wsSecurityUsername;
		private string? _wsSecurityPassword;
		private WsSecurityPasswordType _wsSecurityPasswordType = WsSecurityPasswordType.Plaintext;

		private readonly SoapResilienceEngine _resilienceEngine;
		private readonly List<SoapDiagnosticLog> _diagnostics = new List<SoapDiagnosticLog>();
		private readonly object _diagLock = new object();

		/// <summary>
		/// Gets all diagnostics log entries collected when EnableDiagnostics is true.
		/// </summary>
		public IReadOnlyList<SoapDiagnosticLog> Diagnostics
		{
			get
			{
				lock (_diagLock)
				{
					return _diagnostics.ToArray();
				}
			}
		}

		/// <summary>
		/// Gets the last diagnostics log entry collected.
		/// </summary>
		public SoapDiagnosticLog? GetLastDiagnostic()
		{
			lock (_diagLock)
			{
				return _diagnostics.Count > 0 ? _diagnostics[_diagnostics.Count - 1] : null;
			}
		}

		/// <summary>
		/// Clears all diagnostics logs.
		/// </summary>
		public void ClearDiagnostics()
		{
			lock (_diagLock)
			{
				_diagnostics.Clear();
			}
		}

		/// <summary>
		/// Gets the configuration options used by this client.
		/// </summary>
		public SoapClientOptions Options => _options;

		/// <summary>
		/// Initializes a new instance of the <see cref="SoapClient"/> class using a base URL.
		/// </summary>
		public SoapClient(SoapClientOptions options)
		{
			_options = options ?? throw new ArgumentNullException(nameof(options));
			_resilienceEngine = new SoapResilienceEngine(options.CircuitBreakerFailureThreshold, options.CircuitBreakerResetTimeout);
			_httpClient = CreateHttpClient(options, null, null, null);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SoapClient"/> class with NTLM authentication credentials.
		/// </summary>
		public SoapClient(SoapClientOptions options, string ntlmUsername, string ntlmPassword, string? ntlmDomain = null)
		{
			_options = options ?? throw new ArgumentNullException(nameof(options));
			_resilienceEngine = new SoapResilienceEngine(options.CircuitBreakerFailureThreshold, options.CircuitBreakerResetTimeout);
			var creds = string.IsNullOrEmpty(ntlmDomain)
				? new NetworkCredential(ntlmUsername, ntlmPassword)
				: new NetworkCredential(ntlmUsername, ntlmPassword, ntlmDomain);

			_options.Credentials = creds;
			_httpClient = CreateHttpClient(options, creds, null, null);
		}

		private SoapClient(SoapClientOptions options, WsdlDocument wsdl)
		{
			_options = options ?? throw new ArgumentNullException(nameof(options));
			_wsdl = wsdl;
			_resilienceEngine = new SoapResilienceEngine(options.CircuitBreakerFailureThreshold, options.CircuitBreakerResetTimeout);

			// Resolve target base address from WSDL if not explicitly set
			if (_options.BaseAddress == null && wsdl.Services.Count > 0 && wsdl.Services[0].Endpoints.Count > 0)
			{
				var address = wsdl.Services[0].Endpoints[0].Address;
				if (!string.IsNullOrEmpty(address))
				{
					_options.BaseAddress = new Uri(address);
				}
			}

			_httpClient = CreateHttpClient(_options, null, null, null);
		}

		/// <summary>
		/// Creates a SoapClient dynamically by loading and parsing WSDL metadata.
		/// </summary>
		public static SoapClient FromWsdl(string wsdlUrl, SoapClientOptions? options = null)
		{
			var wsdl = WsdlDocument.Load(wsdlUrl);
			return new SoapClient(options ?? new SoapClientOptions(), wsdl);
		}

		/// <summary>
		/// Creates a SoapClient dynamically by loading and parsing WSDL metadata asynchronously.
		/// </summary>
		public static async Task<SoapClient> FromWsdlAsync(string wsdlUrl, SoapClientOptions? options = null)
		{
			var wsdl = await WsdlDocument.LoadAsync(wsdlUrl).ConfigureAwait(false);
			return new SoapClient(options ?? new SoapClientOptions(), wsdl);
		}

		private static HttpClient CreateHttpClient(
			SoapClientOptions options,
			ICredentials? credentials,
			string? basicUsername,
			string? basicPassword)
		{
			if (options.BackchannelHandler != null)
			{
				var mockClient = new HttpClient(options.BackchannelHandler)
				{
					Timeout = options.Timeout
				};
				if (options.BaseAddress != null)
				{
					mockClient.BaseAddress = options.BaseAddress;
				}
				return mockClient;
			}

			var handler = new HttpClientHandler();

			if (options.Proxy != null)
			{
				handler.Proxy = options.Proxy;
				handler.UseProxy = true;
			}

			handler.CookieContainer = options.Cookies;

			if (options.ServerCertificateCustomValidationCallback != null)
			{
				handler.ServerCertificateCustomValidationCallback = options.ServerCertificateCustomValidationCallback;
			}

			if (options.SslProtocols.HasValue)
			{
				handler.SslProtocols = options.SslProtocols.Value;
			}

			if (credentials != null)
			{
				handler.Credentials = credentials;
				handler.UseDefaultCredentials = false;
			}
			else if (options.Credentials != null)
			{
				handler.Credentials = options.Credentials;
				handler.UseDefaultCredentials = false;
			}
			else if (!string.IsNullOrEmpty(basicUsername))
			{
				handler.Credentials = new NetworkCredential(basicUsername, basicPassword);
			}

			var client = new HttpClient(handler)
			{
				Timeout = options.Timeout
			};

			if (options.BaseAddress != null)
			{
				client.BaseAddress = options.BaseAddress;
			}

			client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);

			foreach (var kvp in options.CustomHeaders)
			{
				client.DefaultRequestHeaders.TryAddWithoutValidation(kvp.Key, kvp.Value);
			}

			return client;
		}

		/// <summary>
		/// Configures Basic Authentication.
		/// </summary>
		public SoapClient WithBasicAuth(string username, string password)
		{
			_basicAuthUsername = username;
			_basicAuthPassword = password;
			return this;
		}

		/// <summary>
		/// Configures Bearer Token Authentication.
		/// </summary>
		public SoapClient WithBearerToken(string token)
		{
			_bearerToken = token;
			return this;
		}

		/// <summary>
		/// Configures API Key Authentication.
		/// </summary>
		public SoapClient WithApiKey(string name, string value, ApiKeyLocation location = ApiKeyLocation.Header)
		{
			_apiKeyName = name;
			_apiKeyValue = value;
			_apiKeyLocation = location;
			return this;
		}

		/// <summary>
		/// Configures WS-Security UsernameToken authentication.
		/// </summary>
		public SoapClient WithWsSecurity(string username, string password, WsSecurityPasswordType passwordType = WsSecurityPasswordType.Plaintext)
		{
			_wsSecurityUsername = username;
			_wsSecurityPassword = password;
			_wsSecurityPasswordType = passwordType;
			return this;
		}

		private void RecreateHttpClient()
		{
			_httpClient?.Dispose();
			_httpClient = CreateHttpClient(_options, null, null, null);
		}

		/// <summary>
		/// Configures NTLM authentication fluently.
		/// </summary>
		public SoapClient WithNtlmAuth(string username, string password, string? domain = null)
		{
			_options.Credentials = string.IsNullOrEmpty(domain)
				? new NetworkCredential(username, password)
				: new NetworkCredential(username, password, domain);
			RecreateHttpClient();
			return this;
		}

		/// <summary>
		/// Enables diagnostics inspector logging fluently.
		/// </summary>
		public SoapClient EnableDiagnostics()
		{
			_options.EnableDiagnostics = true;
			return this;
		}

		/// <summary>
		/// Initiates a SOAP operation request builder.
		/// </summary>
		public SoapOperationBuilder Operation(string name)
		{
			WsdlOperation? opMeta = null;
			if (_wsdl != null)
			{
				opMeta = _wsdl.Operations.FindOperation(name);
			}

			return new SoapOperationBuilder(this, name, opMeta);
		}

		internal Task<SoapResponse> ExecuteAsync(
			string operationName,
			WsdlOperation? metadata,
			Dictionary<string, object?> parameters,
			List<XElement> customSoapHeaders,
			Dictionary<string, string> customHttpHeaders,
			List<SoapAttachment> attachments,
			string? rawXmlOverride,
			string? soapActionOverride,
			CancellationToken cancellationToken)
		{
			return _resilienceEngine.ExecuteAsync(async () =>
			{
				return await ExecuteInternalAsync(operationName, metadata, parameters, customSoapHeaders, customHttpHeaders, attachments, rawXmlOverride, soapActionOverride, cancellationToken).ConfigureAwait(false);
			}, _options.RetryCount, _options.RetryDelay, _options.EnableResilience, cancellationToken);
		}

		private async Task<SoapResponse> ExecuteInternalAsync(
			string operationName,
			WsdlOperation? metadata,
			Dictionary<string, object?> parameters,
			List<XElement> customSoapHeaders,
			Dictionary<string, string> customHttpHeaders,
			List<SoapAttachment> attachments,
			string? rawXmlOverride,
			string? soapActionOverride,
			CancellationToken cancellationToken)
		{
			// Determine operation parameters
			var targetNs = metadata?.InputElementNamespace ?? _wsdl?.TargetNamespace ?? "http://tempuri.org/";
			var inputName = metadata?.InputElementName ?? operationName;
			var soapAction = soapActionOverride ?? metadata?.SoapAction ?? string.Empty;

			// Determine SOAP Version (fallback to 1.1 if not defined in WSDL metadata)
			var soapVersion = SoapVersion.Soap11;
			if (_wsdl != null && metadata != null)
			{
				var binding = _wsdl.Bindings.FindBindingForInterface(metadata.Interface.Name);
				if (binding != null)
				{
					soapVersion = binding.SoapVersion;
				}
			}

			// Build SOAP Request Envelope
			string requestXml;
			if (!string.IsNullOrEmpty(rawXmlOverride))
			{
				requestXml = rawXmlOverride;
			}
			else
			{
				requestXml = BuildSoapEnvelope(inputName, targetNs, soapVersion, parameters, customSoapHeaders, attachments);
			}

			// Determine endpoint URL
			var endpointUrl = _options.BaseAddress;
			if (_wsdl != null && metadata != null)
			{
				var binding = _wsdl.Bindings.FindBindingForInterface(metadata.Interface.Name);
				if (binding != null)
				{
					var endpoint = _wsdl.Services.FindEndpointForBinding(binding.Name);
					if (endpoint != null && Uri.TryCreate(endpoint.Address, UriKind.Absolute, out var uri))
					{
						endpointUrl = uri;
					}
				}
			}

			if (endpointUrl == null)
			{
				throw new InvalidOperationException("No BaseAddress or Service Endpoint Address found. Set BaseAddress in SoapClientOptions.");
			}

			// For API Key query parameters
			if (!string.IsNullOrEmpty(_apiKeyName) && !string.IsNullOrEmpty(_apiKeyValue) && _apiKeyLocation == ApiKeyLocation.Query)
			{
				var builder = new UriBuilder(endpointUrl);
				var queryToAppend = $"{Uri.EscapeDataString(_apiKeyName)}={Uri.EscapeDataString(_apiKeyValue)}";
				builder.Query = string.IsNullOrEmpty(builder.Query) ? queryToAppend : builder.Query.Substring(1) + "&" + queryToAppend;
				endpointUrl = builder.Uri;
			}

			var stopwatch = System.Diagnostics.Stopwatch.StartNew();
			var requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			string responseXml = string.Empty;
			int responseStatusCode = 0;
			bool isSuccess = false;
			string? faultCode = null;
			string? faultString = null;
			string? faultDetail = null;
			string? exceptionMessage = null;

			HttpResponseMessage response = null!;
			try
			{
				var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);

				// Add body content with correct encoding and Content-Type
				var mediaType = soapVersion == SoapVersion.Soap12 ? "application/soap+xml" : "text/xml";

				HttpContent content;
				if (attachments != null && attachments.Count > 0)
				{
					var startContentId = "rootpart@smartwsdlkit.org";
					var multipart = new MultipartContent("related");

					var rootMediaType = _options.AttachmentMode == SoapAttachmentMode.Mtom
						? "application/xop+xml"
						: mediaType;

					multipart.Headers.ContentType!.Parameters.Add(new NameValueHeaderValue("type", $"\"{rootMediaType}\""));
					multipart.Headers.ContentType!.Parameters.Add(new NameValueHeaderValue("start", $"\"<{startContentId}>\""));

					string xmlContentType;
					if (_options.AttachmentMode == SoapAttachmentMode.Mtom)
					{
						xmlContentType = $"application/xop+xml; charset={_options.RequestEncoding.WebName}; type=\"{mediaType}\"";
					}
					else
					{
						xmlContentType = $"{mediaType}; charset={_options.RequestEncoding.WebName}";
					}

					var xmlContent = new StringContent(requestXml, _options.RequestEncoding);
					xmlContent.Headers.ContentType = MediaTypeHeaderValue.Parse(xmlContentType);
					xmlContent.Headers.TryAddWithoutValidation("Content-ID", $"<{startContentId}>");
					xmlContent.Headers.TryAddWithoutValidation("Content-Transfer-Encoding", "8bit");

					multipart.Add(xmlContent);

					foreach (var attachment in attachments)
					{
						var binaryContent = new ByteArrayContent(attachment.Data);
						binaryContent.Headers.ContentType = new MediaTypeHeaderValue(attachment.ContentType);
						binaryContent.Headers.TryAddWithoutValidation("Content-ID", $"<{attachment.ContentId}>");
						binaryContent.Headers.TryAddWithoutValidation("Content-Transfer-Encoding", "binary");
						if (!string.IsNullOrEmpty(attachment.FileName))
						{
							binaryContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
							{
								FileName = attachment.FileName
							};
						}
						multipart.Add(binaryContent);
					}
					content = multipart;
				}
				else
				{
					var xmlContent = new StringContent(requestXml, _options.RequestEncoding, mediaType);
					if (soapVersion == SoapVersion.Soap12 && !string.IsNullOrEmpty(soapAction))
					{
						xmlContent.Headers.ContentType!.Parameters.Add(new NameValueHeaderValue("action", $"\"{soapAction}\""));
					}
					content = xmlContent;
				}

				request.Content = content;

				// Add headers
				if (soapVersion == SoapVersion.Soap11 && !string.IsNullOrEmpty(soapAction))
				{
					request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{soapAction}\"");
				}

				// Authentications
				if (!string.IsNullOrEmpty(_bearerToken))
				{
					request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);
				}
				else if (!string.IsNullOrEmpty(_basicAuthUsername))
				{
					var credentialBuffer = Encoding.ASCII.GetBytes($"{_basicAuthUsername}:{_basicAuthPassword}");
					request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentialBuffer));
				}

				if (!string.IsNullOrEmpty(_apiKeyName) && !string.IsNullOrEmpty(_apiKeyValue) && _apiKeyLocation == ApiKeyLocation.Header)
				{
					request.Headers.TryAddWithoutValidation(_apiKeyName, _apiKeyValue);
				}

				// Add per-request custom HTTP headers
				foreach (var header in customHttpHeaders)
				{
					request.Headers.TryAddWithoutValidation(header.Key, header.Value);
				}

				// Capture request headers for diagnostics
				if (_options.EnableDiagnostics)
				{
					foreach (var h in request.Headers)
					{
						requestHeaders[h.Key] = string.Join(", ", h.Value);
					}
					if (request.Content != null)
					{
						foreach (var h in request.Content.Headers)
						{
							requestHeaders[h.Key] = string.Join(", ", h.Value);
						}
					}
				}

				response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
				responseStatusCode = (int)response.StatusCode;

				// Capture response headers for diagnostics
				if (_options.EnableDiagnostics)
				{
					foreach (var h in response.Headers)
					{
						responseHeaders[h.Key] = string.Join(", ", h.Value);
					}
					if (response.Content != null)
					{
						foreach (var h in response.Content.Headers)
						{
							responseHeaders[h.Key] = string.Join(", ", h.Value);
						}
					}
				}

				// Read response content
				var responseBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

				var responseEncoding = _options.ResponseEncoding;
				var contentTypeHeader = response.Content.Headers.ContentType;
				if (contentTypeHeader != null && !string.IsNullOrEmpty(contentTypeHeader.CharSet))
				{
					try
					{
						responseEncoding = Encoding.GetEncoding(contentTypeHeader.CharSet);
					}
					catch { }
				}

				responseXml = responseEncoding.GetString(responseBytes);

				// Try to parse as XML envelope even if status code is not 200 or 500
				XDocument respDoc;
				try
				{
					respDoc = XDocument.Parse(responseXml);
				}
				catch (Exception ex)
				{
					if (!response.IsSuccessStatusCode)
					{
						throw new SoapException($"HTTP request failed with status code {response.StatusCode} and response is not valid XML. Raw response: {responseXml}", ex);
					}
					throw new WsdlParseException($"Failed to parse response XML: {ex.Message}. Raw response: {responseXml}", ex);
				}

				var envelopeNamespace = soapVersion == SoapVersion.Soap12 ? Soap12Ns : Soap11Ns;
				var bodyEl = respDoc.Root?.Element(envelopeNamespace + "Body");
				if (bodyEl == null)
				{
					bodyEl = respDoc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Body");
					if (bodyEl == null)
					{
						if (!response.IsSuccessStatusCode)
						{
							throw new SoapException($"HTTP request failed with status code {response.StatusCode}. Raw response: {responseXml}");
						}
						throw new SoapException("Invalid SOAP response envelope: Body element missing.");
					}
				}

				// Check for SOAP Fault inside Body
				var faultEl = bodyEl.Element(envelopeNamespace + "Fault") ??
							  bodyEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Fault");

				if (faultEl != null)
				{
					string faultCodeStr;
					string faultStringStr;
					string? faultActorStr;
					string? detailXmlStr = null;

					if (soapVersion == SoapVersion.Soap12)
					{
						var codeValEl = faultEl.Element(envelopeNamespace + "Code")?.Element(envelopeNamespace + "Value") ??
										faultEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Code")?.Elements().FirstOrDefault(e => e.Name.LocalName == "Value");
						faultCodeStr = codeValEl?.Value ?? string.Empty;

						var reasonEl = faultEl.Element(envelopeNamespace + "Reason") ??
									   faultEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Reason");
						faultStringStr = reasonEl?.Value ?? string.Empty;

						var roleEl = faultEl.Element(envelopeNamespace + "Role") ??
									 faultEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Role");
						faultActorStr = roleEl?.Value;
					}
					else
					{
						faultCodeStr = faultEl.Element("faultcode")?.Value ?? faultEl.Elements().FirstOrDefault(e => e.Name.LocalName == "faultcode")?.Value ?? string.Empty;
						faultStringStr = faultEl.Element("faultstring")?.Value ?? faultEl.Elements().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value ?? string.Empty;
						faultActorStr = faultEl.Element("faultactor")?.Value ?? faultEl.Elements().FirstOrDefault(e => e.Name.LocalName == "faultactor")?.Value;
					}

					var detailEl = faultEl.Element("detail") ?? faultEl.Elements().FirstOrDefault(e => e.Name.LocalName == "detail");
					if (detailEl != null)
					{
						detailXmlStr = detailEl.ToString();
					}

					throw new SoapFaultException(faultCodeStr, faultStringStr, faultActorStr, detailXmlStr);
				}

				if (!response.IsSuccessStatusCode)
				{
					throw new SoapException($"HTTP request failed with status code {response.StatusCode}. Response envelope structure was valid XML but returned error. Raw: {responseXml}");
				}

				isSuccess = true;
				return new SoapResponse(responseXml, bodyEl);
			}
			catch (SoapFaultException sfEx)
			{
				faultCode = sfEx.FaultCode;
				faultString = sfEx.FaultString;
				faultDetail = sfEx.DetailXml;
				exceptionMessage = sfEx.Message;
				throw;
			}
			catch (Exception ex)
			{
				exceptionMessage = ex.Message;
				throw;
			}
			finally
			{
				stopwatch.Stop();
				if (_options.EnableDiagnostics)
				{
					var log = new SoapDiagnosticLog
					{
						OperationName = operationName,
						EndpointUrl = endpointUrl.ToString(),
						RequestXml = requestXml,
						RequestHeaders = requestHeaders,
						ResponseXml = responseXml,
						ResponseHeaders = responseHeaders,
						HttpStatusCode = responseStatusCode,
						ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
						IsSuccess = isSuccess,
						SoapFaultCode = faultCode,
						SoapFaultString = faultString,
						SoapFaultDetail = faultDetail,
						ErrorMessage = exceptionMessage,
						Timestamp = DateTime.UtcNow
					};
					lock (_diagLock)
					{
						_diagnostics.Add(log);
					}
				}
			}
		}

		internal string BuildSoapEnvelope(
			string operationName,
			string targetNamespace,
			SoapVersion soapVersion,
			Dictionary<string, object?> parameters,
			List<XElement> customSoapHeaders,
			List<SoapAttachment>? attachments = null)
		{
			XNamespace envNs = soapVersion == SoapVersion.Soap12 ? Soap12Ns : Soap11Ns;
			XNamespace tns = targetNamespace;

			var envelope = new XElement(envNs + "Envelope",
				new XAttribute(XNamespace.Xmlns + (soapVersion == SoapVersion.Soap12 ? "soap12" : "soap"), envNs.NamespaceName),
				new XAttribute(XNamespace.Xmlns + "tns", targetNamespace)
			);

			// Construct Headers
			var header = new XElement(envNs + "Header");
			var hasHeaders = false;

			// WS-Security UsernameToken header
			if (!string.IsNullOrEmpty(_wsSecurityUsername))
			{
				hasHeaders = true;
				XNamespace wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
				XNamespace wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";

				var securityEl = new XElement(wsse + "Security",
					new XAttribute(XNamespace.Xmlns + "wsse", wsse.NamespaceName),
					new XAttribute(XNamespace.Xmlns + "wsu", wsu.NamespaceName)
				);

				var tokenEl = new XElement(wsse + "UsernameToken",
					new XAttribute(wsu + "Id", "UsernameToken-1"),
					new XElement(wsse + "Username", _wsSecurityUsername)
				);

				if (_wsSecurityPasswordType == WsSecurityPasswordType.PasswordDigest)
				{
					byte[] nonceBytes = new byte[16];
					using (var rng = RandomNumberGenerator.Create())
					{
						rng.GetBytes(nonceBytes);
					}
					var nonceBase64 = Convert.ToBase64String(nonceBytes);
					var created = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

					var createdBytes = Encoding.UTF8.GetBytes(created);
					var passBytes = Encoding.UTF8.GetBytes(_wsSecurityPassword ?? string.Empty);

					var buffer = new byte[nonceBytes.Length + createdBytes.Length + passBytes.Length];
					Buffer.BlockCopy(nonceBytes, 0, buffer, 0, nonceBytes.Length);
					Buffer.BlockCopy(createdBytes, 0, buffer, nonceBytes.Length, createdBytes.Length);
					Buffer.BlockCopy(passBytes, 0, buffer, nonceBytes.Length + createdBytes.Length, passBytes.Length);

					byte[] hash;
					using (var sha1 = SHA1.Create())
					{
						hash = sha1.ComputeHash(buffer);
					}
					var digest = Convert.ToBase64String(hash);

					tokenEl.Add(new XElement(wsse + "Password",
						new XAttribute("Type", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest"),
						digest
					 ));
					tokenEl.Add(new XElement(wsse + "Nonce",
						new XAttribute("EncodingType", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary"),
						nonceBase64
					 ));
					tokenEl.Add(new XElement(wsu + "Created", created));
				}
				else
				{
					tokenEl.Add(new XElement(wsse + "Password",
						new XAttribute("Type", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText"),
						_wsSecurityPassword ?? string.Empty
					 ));
				}

				securityEl.Add(tokenEl);
				header.Add(securityEl);
			}

			// Custom XML headers
			foreach (var h in customSoapHeaders)
			{
				hasHeaders = true;
				header.Add(h);
			}

			if (hasHeaders)
			{
				envelope.Add(header);
			}

			// Construct Body
			var body = new XElement(envNs + "Body");
			var payload = new XElement(tns + operationName);

			foreach (var param in parameters)
			{
				SerializeParameter(payload, param.Key, param.Value, tns, attachments);
			}

			body.Add(payload);
			envelope.Add(body);

			return envelope.ToString();
		}

		private void SerializeParameter(XElement parent, string key, object? value, XNamespace tns, List<SoapAttachment>? attachments)
		{
			if (value == null)
			{
				XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
				var nilElement = new XElement(tns + key,
					new XAttribute(xsi + "nil", "true"),
					new XAttribute(XNamespace.Xmlns + "xsi", xsi.NamespaceName)
				);
				parent.Add(nilElement);
				return;
			}

			if (value is SoapAttachment attachment)
			{
				if (attachments != null && !attachments.Exists(a => a.ContentId == attachment.ContentId))
				{
					attachments.Add(attachment);
				}

				if (_options.AttachmentMode == SoapAttachmentMode.Mtom)
				{
					XNamespace xop = "http://www.w3.org/2004/08/xop/include";
					var includeEl = new XElement(xop + "Include",
						new XAttribute("href", $"cid:{attachment.ContentId}"),
						new XAttribute(XNamespace.Xmlns + "xop", xop.NamespaceName)
					);
					var el = new XElement(tns + key, includeEl);
					parent.Add(el);
				}
				else
				{
					var el = new XElement(tns + key,
						new XAttribute("href", $"cid:{attachment.ContentId}")
					);
					parent.Add(el);
				}
				return;
			}

			// If list or array
			if (value is IEnumerable list && !(value is string))
			{
				foreach (var item in list)
				{
					if (item == null) continue;
					SerializeParameter(parent, key, item, tns, attachments);
				}
				return;
			}

			// Object serialization
			var type = value.GetType();
			if (type.IsPrimitive || value is string || value is decimal || value is DateTime || value is DateTimeOffset || type.IsEnum)
			{
				var el = new XElement(tns + key, FormatPrimitiveValue(value));
				parent.Add(el);
			}
			else
			{
				// Complex Object
				var complexEl = new XElement(tns + key);
				var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
				foreach (var prop in properties)
				{
					var propVal = prop.GetValue(value);

					// Support Custom XmlElementAttribute naming
					var elName = prop.Name;
					var xmlElAttr = prop.GetCustomAttribute<XmlElementAttribute>();
					if (xmlElAttr != null && !string.IsNullOrEmpty(xmlElAttr.ElementName))
					{
						elName = xmlElAttr.ElementName;
					}

					SerializeParameter(complexEl, elName, propVal, tns, attachments);
				}
				parent.Add(complexEl);
			}
		}

		private string FormatPrimitiveValue(object value)
		{
			if (value is DateTime dt)
			{
				return dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
			}
			if (value is DateTimeOffset dto)
			{
				return dto.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
			}
			if (value is decimal dec)
			{
				return dec.ToString(CultureInfo.InvariantCulture);
			}
			if (value is double dbl)
			{
				return dbl.ToString(CultureInfo.InvariantCulture);
			}
			if (value is float fl)
			{
				return fl.ToString(CultureInfo.InvariantCulture);
			}
			if (value is bool b)
			{
				return b ? "true" : "false";
			}
			if (value is Enum e)
			{
				var enumType = e.GetType();
				var name = Enum.GetName(enumType, e);
				if (name != null)
				{
					var field = enumType.GetField(name);
					var attr = field?.GetCustomAttribute<XmlEnumAttribute>();
					if (attr != null && !string.IsNullOrEmpty(attr.Name))
					{
						return attr.Name;
					}
				}
				return e.ToString();
			}
			return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
		}

		/// <summary>
		/// Disposes internal HttpClient handler resources.
		/// </summary>
		public void Dispose()
		{
			_httpClient.Dispose();
		}
	}

	/// <summary>
	/// Fluent builder to construct and trigger a SOAP operation request.
	/// </summary>
	public class SoapOperationBuilder
	{
		private readonly SoapClient _client;
		private readonly string _operationName;
		private readonly WsdlOperation? _metadata;
		private readonly Dictionary<string, object?> _parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
		private readonly List<XElement> _customSoapHeaders = new List<XElement>();
		private readonly Dictionary<string, string> _customHttpHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private readonly List<SoapAttachment> _attachments = new List<SoapAttachment>();
		private string? _rawXmlOverride;
		private string? _soapActionOverride;

		internal SoapOperationBuilder(SoapClient client, string operationName, WsdlOperation? metadata)
		{
			_client = client;
			_operationName = operationName;
			_metadata = metadata;
		}

		/// <summary>
		/// Adds a parameters to the SOAP request body.
		/// </summary>
		public SoapOperationBuilder With(string name, object? value)
		{
			_parameters[name] = value;
			return this;
		}

		/// <summary>
		/// Appends a custom XML header to the SOAP Header element.
		/// </summary>
		public SoapOperationBuilder WithSoapHeader(XElement element)
		{
			if (element != null)
			{
				_customSoapHeaders.Add(element);
			}
			return this;
		}

		/// <summary>
		/// Appends a custom SOAP header element with a name and value (serialized in target namespace).
		/// </summary>
		public SoapOperationBuilder WithSoapHeader(string name, object? value)
		{
			var targetNs = _metadata?.InputElementNamespace ?? _client.Options.BaseAddress?.ToString() ?? "http://tempuri.org/";
			return WithSoapHeader(name, targetNs, value);
		}

		/// <summary>
		/// Appends a custom SOAP header element with a name, namespace, and value.
		/// </summary>
		public SoapOperationBuilder WithSoapHeader(string name, string ns, object? value)
		{
			if (string.IsNullOrEmpty(name)) return this;

			XNamespace xns = ns;
			XElement headerElement;

			if (value == null)
			{
				headerElement = new XElement(xns + name);
			}
			else if (value is XElement xel)
			{
				headerElement = xel;
			}
			else if (value is string str)
			{
				headerElement = new XElement(xns + name, str);
			}
			else
			{
				headerElement = new XElement(xns + name);
				var type = value.GetType();
				if (type.IsPrimitive || value is decimal || value is DateTime || value is DateTimeOffset || type.IsEnum)
				{
					headerElement.Value = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
				}
				else
				{
					foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
					{
						var propVal = prop.GetValue(value);
						if (propVal != null)
						{
							headerElement.Add(new XElement(xns + prop.Name, Convert.ToString(propVal, CultureInfo.InvariantCulture)));
						}
					}
				}
			}

			_customSoapHeaders.Add(headerElement);
			return this;
		}

		/// <summary>
		/// Adds a custom HTTP header to this specific request.
		/// </summary>
		public SoapOperationBuilder WithHttpHeader(string name, string value)
		{
			if (!string.IsNullOrEmpty(name))
			{
				_customHttpHeaders[name] = value ?? string.Empty;
			}
			return this;
		}

		/// <summary>
		/// Adds a SOAP attachment (MTOM or SOAP with Attachments) to the request.
		/// </summary>
		public SoapOperationBuilder WithAttachment(string contentId, byte[] data, string contentType = "application/octet-stream", string? fileName = null)
		{
			_attachments.Add(new SoapAttachment(contentId, data, contentType, fileName));
			return this;
		}

		/// <summary>
		/// Overrides the request SOAP body with raw XML.
		/// </summary>
		public SoapOperationBuilder WithBody(string rawXml)
		{
			_rawXmlOverride = rawXml;
			return this;
		}

		/// <summary>
		/// Overrides the request SOAP body with an XML element.
		/// </summary>
		public SoapOperationBuilder WithBody(XElement body)
		{
			_rawXmlOverride = body?.ToString();
			return this;
		}

		/// <summary>
		/// Overrides the SOAP Action HTTP Header or content-type action parameter.
		/// </summary>
		public SoapOperationBuilder WithSoapAction(string soapAction)
		{
			_soapActionOverride = soapAction;
			return this;
		}

		/// <summary>
		/// Executes the SOAP request asynchronously and returns the response metadata.
		/// </summary>
		public Task<SoapResponse> ExecuteAsync(CancellationToken cancellationToken = default)
		{
			return _client.ExecuteAsync(
				_operationName,
				_metadata,
				_parameters,
				_customSoapHeaders,
				_customHttpHeaders,
				_attachments,
				_rawXmlOverride,
				_soapActionOverride,
				cancellationToken
			);
		}

		/// <summary>
		/// Executes the SOAP request asynchronously and deserializes the response directly into the specified type.
		/// </summary>
		public async Task<T> ExecuteAsync<T>(CancellationToken cancellationToken = default)
		{
			var response = await ExecuteAsync(cancellationToken).ConfigureAwait(false);
			return response.As<T>();
		}

		/// <summary>
		/// Executes the SOAP request asynchronously and deserializes the response directly into the specified type.
		/// </summary>
		public async Task<object> ExecuteAsync(Type type, CancellationToken cancellationToken = default)
		{
			if (type == null) throw new ArgumentNullException(nameof(type));
			var response = await ExecuteAsync(cancellationToken).ConfigureAwait(false);
			return response.As(type);
		}
	}

	/// <summary>
	/// Represents the result of a SOAP invocation containing raw and parsed XML.
	/// </summary>
	public class SoapResponse
	{
		/// <summary>
		/// Gets the raw XML string returned by the SOAP endpoint.
		/// </summary>
		public string RawXml { get; }

		/// <summary>
		/// Gets the parsed XML Body element inside the SOAP Envelope.
		/// </summary>
		public XElement Body { get; }

		internal SoapResponse(string rawXml, XElement body)
		{
			RawXml = rawXml;
			Body = body;
		}

		/// <summary>
		/// Deserializes the first child element of the SOAP Body into the specified type using XML reflection serialization.
		/// </summary>
		public T As<T>()
		{
			var targetElement = Body.FirstNode as XElement;
			if (targetElement == null)
			{
				throw new SoapException("SOAP response body does not contain a child element to deserialize.");
			}

			// Using standard XmlSerializer mapping
			var serializer = new XmlSerializer(typeof(T), new XmlRootAttribute(targetElement.Name.LocalName)
			{
				Namespace = targetElement.Name.NamespaceName
			});

			using var reader = targetElement.CreateReader();
			return (T)serializer.Deserialize(reader);
		}

		/// <summary>
		/// Deserializes the first child element of the SOAP Body into the specified type using XML reflection serialization.
		/// </summary>
		public object As(Type type)
		{
			if (type == null) throw new ArgumentNullException(nameof(type));
			var targetElement = Body.FirstNode as XElement;
			if (targetElement == null)
			{
				throw new SoapException("SOAP response body does not contain a child element to deserialize.");
			}

			// Using standard XmlSerializer mapping
			var serializer = new XmlSerializer(type, new XmlRootAttribute(targetElement.Name.LocalName)
			{
				Namespace = targetElement.Name.NamespaceName
			});

			using var reader = targetElement.CreateReader();
			return serializer.Deserialize(reader);
		}

		/// <summary>
		/// Deserializes the SOAP response body element dynamically to a Dictionary structure.
		/// </summary>
		public Dictionary<string, object?> ToDictionary()
		{
			var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
			var targetElement = Body.FirstNode as XElement;
			if (targetElement != null)
			{
				foreach (var el in targetElement.Elements())
				{
					dict[el.Name.LocalName] = ParseValue(el);
				}
			}
			return dict;
		}

		private object? ParseValue(XElement element)
		{
			// If xsi:nil="true" is set
			var xsiNil = element.Attribute(XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance") + "nil")?.Value;
			if (xsiNil == "true" || xsiNil == "1")
			{
				return null;
			}

			if (!element.HasElements)
			{
				return element.Value;
			}

			// If it's a list (multiple adjacent tags with the same local name)
			var children = new List<XElement>(element.Elements());
			if (children.Count > 1 && children.TrueForAll(c => c.Name == children[0].Name))
			{
				var list = new List<object?>();
				foreach (var child in children)
				{
					list.Add(ParseValue(child));
				}
				return list;
			}

			// Convert to sub-dictionary
			var subDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
			foreach (var child in element.Elements())
			{
				subDict[child.Name.LocalName] = ParseValue(child);
			}
			return subDict;
		}
	}

	internal static class ModelHelperExtensions
	{
		public static WsdlOperation? FindOperation(this IEnumerable<WsdlOperation> operations, string name)
		{
			foreach (var op in operations)
			{
				if (op.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					return op;
				}
			}
			return null;
		}

		public static WsdlBinding? FindBindingForInterface(this IEnumerable<WsdlBinding> bindings, string interfaceName)
		{
			foreach (var b in bindings)
			{
				if (b.Interface.Name.Equals(interfaceName, StringComparison.OrdinalIgnoreCase))
				{
					return b;
				}
			}
			return null;
		}

		public static WsdlEndpoint? FindEndpointForBinding(this IEnumerable<WsdlService> services, string bindingName)
		{
			foreach (var s in services)
			{
				foreach (var e in s.Endpoints)
				{
					if (e.Binding.Name.Equals(bindingName, StringComparison.OrdinalIgnoreCase))
					{
						return e;
					}
				}
			}
			return null;
		}
	}
}
