using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace SmartWsdlKit.Sample.ConsoleApp
{
	/// <summary>
	/// Represents the strongly-typed XML model of the CapitalCity response element.
	/// </summary>
	[XmlRoot(ElementName = "CapitalCityResponse", Namespace = "http://www.oorsprong.org/websamples.countryinfo")]
	public class CapitalCityResponse
	{
		/// <summary>
		/// Gets or sets the capital city string result.
		/// </summary>
		[XmlElement(ElementName = "CapitalCityResult")]
		public string CapitalCityResult { get; set; } = string.Empty;
	}

	/// <summary>
	/// Represents the strongly-typed XML model of the ListOfLanguagesByCode response element.
	/// </summary>
	[XmlRoot(ElementName = "ListOfLanguagesByCodeResponse", Namespace = "http://www.oorsprong.org/websamples.countryinfo")]
	public class ListOfLanguagesByCodeResponse
	{
		/// <summary>
		/// Gets or sets the list of language objects returned.
		/// </summary>
		[XmlArray(ElementName = "ListOfLanguagesByCodeResult", Namespace = "http://www.oorsprong.org/websamples.countryinfo")]
		[XmlArrayItem(ElementName = "tLanguage", Namespace = "http://www.oorsprong.org/websamples.countryinfo")]
		public List<LanguageCodeAndName> Languages { get; set; } = new List<LanguageCodeAndName>();
	}

	/// <summary>
	/// Holds the language code and its corresponding name.
	/// </summary>
	[XmlType(Namespace = "http://www.oorsprong.org/websamples.countryinfo")]
	public class LanguageCodeAndName
	{
		/// <summary>
		/// Gets or sets the language ISO code.
		/// </summary>
		[XmlElement(ElementName = "sISOCode", Namespace = "http://www.oorsprong.org/websamples.countryinfo")]
		public string sISOCode { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the name of the language.
		/// </summary>
		[XmlElement(ElementName = "sName", Namespace = "http://www.oorsprong.org/websamples.countryinfo")]
		public string sName { get; set; } = string.Empty;
	}

	class Program
	{
		private const string WsdlUrl = "http://webservices.oorsprong.org/websamples.countryinfo/CountryInfoService.wso?WSDL";

		static async Task Main(string[] args)
		{
			Console.OutputEncoding = Encoding.UTF8;
			PrintHeader("SmartWsdlKit SDK Comprehensive Capabilities Demo");

			try
			{
				// ----------------------------------------------------
				// Step 1: WSDL Metadata Exploration & Analysis
				// ----------------------------------------------------
				PrintSection("1. WSDL Exploration, Analysis, and Generation");
				Console.WriteLine($"[INFO] Loading live WSDL from: {WsdlUrl}");

				var explorerResult = await WsdlExplorer.ExploreAsync(WsdlUrl);
				Console.WriteLine($"  [Explorer] Service Name:      {explorerResult.ServiceName}");
				Console.WriteLine($"  [Explorer] Total Operations:  {explorerResult.SupportedOperations.Count}");
				Console.WriteLine($"  [Explorer] First 5 Operations: {string.Join(", ", explorerResult.SupportedOperations.Take(5))}");

				var wsdlDoc = await WsdlDocument.LoadAsync(WsdlUrl);
				Console.WriteLine($"  [Analyzer] Namespace:         {wsdlDoc.TargetNamespace}");
				var report = WsdlAnalyzer.Analyze(wsdlDoc);
				Console.WriteLine($"  [Analyzer] WSDL Complexity:   {report.ComplexityScore} / 100");
				Console.WriteLine($"  [Analyzer] Warnings Found:    {report.Warnings.Count}");

				// Generate OpenAPI 3.0 & C# Proxy Code
				var openApiJson = OpenApiGenerator.Generate(wsdlDoc);
				var proxyCode = WsdlCodeGenerator.Generate(wsdlDoc, new CodeGeneratorOptions
				{
					Namespace = "SmartWsdlKit.Sample.Generated",
					GenerateRecords = true
				});
				Console.WriteLine($"  [CodeGen] Generated OpenAPI Spec Size: {openApiJson.Length} bytes");
				Console.WriteLine($"  [CodeGen] Generated C# Proxy Code Size: {proxyCode.Length} chars");


				// ----------------------------------------------------
				// Step 2: Strongly Typed Call using Generic Casting (.ExecuteAsync<T>)
				// ----------------------------------------------------
				PrintSection("2. Strongly Typed SOAP Invocation & Generic Casting");

				var clientOptions = new SoapClientOptions
				{
					EnableDiagnostics = true,
					EnableResilience = true,
					Timeout = TimeSpan.FromSeconds(15)
				};

				using var client = await SoapClient.FromWsdlAsync(WsdlUrl, clientOptions);

				Console.WriteLine("[CALL] Executing 'CapitalCity' for Turkey ('TR') using .ExecuteAsync<CapitalCityResponse>()...");
				var capitalCityResp = await client.Operation("CapitalCity")
					.With("sCountryISOCode", "TR")
					.ExecuteAsync<CapitalCityResponse>();

				Console.WriteLine($"  [CAST] Returned Type:  {capitalCityResp.GetType().FullName}");
				Console.WriteLine($"  [CAST] Capital Result: {capitalCityResp.CapitalCityResult}");


				// ----------------------------------------------------
				// Step 3: Complex List Deserialization using Type Casting (.ExecuteAsync(Type))
				// ----------------------------------------------------
				PrintSection("3. Complex List Deserialization & Type Parameter Casting");

				Console.WriteLine("[CALL] Executing 'ListOfLanguagesByCode' using .ExecuteAsync(typeof(ListOfLanguagesByCodeResponse))...");
				var languagesObj = await client.Operation("ListOfLanguagesByCode")
					.ExecuteAsync(typeof(ListOfLanguagesByCodeResponse));

				Console.WriteLine($"  [CAST] Returned Type:  {languagesObj.GetType().FullName}");

				if (languagesObj is ListOfLanguagesByCodeResponse listResp)
				{
					Console.WriteLine($"  [CAST] Total Languages Fetched: {listResp.Languages.Count}");
					Console.WriteLine("\n  Code | Language Name");
					Console.WriteLine("  -----|--------------------");
					foreach (var lang in listResp.Languages.Take(8))
					{
						Console.WriteLine($"  {lang.sISOCode,-4} | {lang.sName}");
					}
					if (listResp.Languages.Count > 8)
					{
						Console.WriteLine("  ...  | ...");
					}
				}


				// ----------------------------------------------------
				// Step 4: Soap Traffic Diagnostics Audit
				// ----------------------------------------------------
				PrintSection("4. Soap Traffic Diagnostics Audit");

				var diagnosticLog = client.GetLastDiagnostic();
				if (diagnosticLog != null)
				{
					Console.WriteLine($"  [Diag] Last Request URL:       {diagnosticLog.EndpointUrl}");
					Console.WriteLine($"  [Diag] HTTP Status Code:       {diagnosticLog.HttpStatusCode}");
					Console.WriteLine($"  [Diag] Execution Time:         {diagnosticLog.ExecutionTimeMs:F2} ms");
					Console.WriteLine($"  [Diag] Is Success Status:      {diagnosticLog.IsSuccess}");

					Console.WriteLine("\n--- SENT SOAP REQUEST ENVELOPE (DIAGNOSTICS) ---");
					Console.WriteLine(diagnosticLog.RequestXml);

					Console.WriteLine("\n--- RECEIVED SOAP RESPONSE ENVELOPE (DIAGNOSTICS) ---");
					// Print first 500 characters of response to prevent console cluttering
					var responseSnippet = diagnosticLog.ResponseXml.Length > 600
						? diagnosticLog.ResponseXml.Substring(0, 600) + "\n... [TRUNCATED] ..."
						: diagnosticLog.ResponseXml;
					Console.WriteLine(responseSnippet);
				}


				// ----------------------------------------------------
				// Step 5: Advanced Configurations & Security (WS-Security / Custom Headers)
				// ----------------------------------------------------
				PrintSection("5. Client Options & Security (WS-Security Digest & Custom Headers)");

				Console.WriteLine("[INFO] Configuring a new SoapClient with WS-Security Password Digest and Custom HTTP/SOAP headers...");

				var secureOptions = new SoapClientOptions
				{
					BaseAddress = new Uri("http://127.0.0.1:9999/secure-soap-service"),
					EnableDiagnostics = true,
					UserAgent = "SmartWsdlKit-SecureAgent/1.0"
				};
				// Add global custom HTTP header to all client requests
				secureOptions.CustomHeaders.Add("X-Partner-Id", "987654");

				using var secureClient = new SoapClient(secureOptions);
				secureClient.WithWsSecurity("WsdlUser", "SecretPassword123", WsSecurityPasswordType.PasswordDigest);

				// Prepare custom SOAP header element
				XNamespace customNs = "http://example.com/security";
				var soapHeaderElement = new XElement(customNs + "SessionId", "SESS-XYZ-987");

				try
				{
					// Trigger request to a local dummy address to build the SOAP envelope and capture log
					await secureClient.Operation("CapitalCity")
						.With("sCountryISOCode", "US")
						.WithSoapHeader(soapHeaderElement)
						.WithHttpHeader("X-Request-Correlation", Guid.NewGuid().ToString())
						.ExecuteAsync();
				}
				catch
				{
					// Expected connection failure since server doesn't exist.
					// We only want the generated request XML captured by Diagnostics.
				}

				var secureDiag = secureClient.GetLastDiagnostic();
				if (secureDiag != null)
				{
					Console.WriteLine("\n--- GENERATED SECURE SOAP REQUEST ENVELOPE (WS-SECURITY DIGEST) ---");
					Console.WriteLine(secureDiag.RequestXml);
				}


				// ----------------------------------------------------
				// Step 6: Built-in Resilience & Circuit Breaker Demonstration
				// ----------------------------------------------------
				PrintSection("6. Fault Tolerance (Retries & Circuit Breaker) Demonstration");

				Console.WriteLine("[INFO] Creating a resilient SoapClient configured with a nonexistent URL...");
				var resilientOptions = new SoapClientOptions
				{
					BaseAddress = new Uri("http://invalid-nonexistent-soap-server.local/service.asmx"),
					EnableResilience = true,
					RetryCount = 2,
					RetryDelay = TimeSpan.FromMilliseconds(50),
					CircuitBreakerFailureThreshold = 2,
					CircuitBreakerResetTimeout = TimeSpan.FromMilliseconds(500)
				};

				using var resilientClient = new SoapClient(resilientOptions);

				Console.WriteLine("[CALL] Attempting execution 1 (Should retry 2 times)...");
				try
				{
					await resilientClient.Operation("GetData").ExecuteAsync();
				}
				catch (Exception ex)
				{
					Console.WriteLine($"  [FAIL] Execution 1 failed as expected: {ex.Message}");
				}

				Console.WriteLine("\n[CALL] Circuit Breaker state is now OPEN. Attempting execution 2 (Should fail-fast instantly)...");
				try
				{
					await resilientClient.Operation("GetData").ExecuteAsync();
				}
				catch (CircuitBreakerOpenException cbEx)
				{
					Console.WriteLine($"  [FAIL-FAST] Execution 2 failed-fast successfully! Reason: {cbEx.Message}");
				}
			}
			catch (Exception ex)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"\n[ERROR] An unexpected error occurred: {ex.Message}");
				Console.ResetColor();
			}

			Console.WriteLine();
			PrintHeader("Demo Completed Successfully");
		}

		private static void PrintHeader(string text)
		{
			var line = new string('=', 65);
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine(line);
			Console.WriteLine($"  {text}");
			Console.WriteLine(line);
			Console.ResetColor();
			Console.WriteLine();
		}

		private static void PrintSection(string title)
		{
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine($"\n=== {title} ===");
			Console.ResetColor();
			Console.WriteLine();
		}
	}
}
