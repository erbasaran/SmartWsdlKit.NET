# SmartWsdlKit

[![NuGet Version](https://img.shields.io/nuget/v/SmartWsdlKit.svg)](https://www.nuget.org/packages/SmartWsdlKit)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**SmartWsdlKit** is a lightweight, high-performance, enterprise-grade WSDL and SOAP client toolkit for .NET.

It is designed from the ground up to eliminate the complexity of Visual Studio Connected Services, `svcutil`, and legacy SOAP integrations by providing a modern, developer-friendly, and async-first API.

## Features

- **WSDL 1.1 & 2.0 Parsing:** Stream-based XML parsing of services, ports, bindings, operations, messages, and schemas.
- **Dynamic SOAP Invocation:** Execute SOAP requests using a fluent builder API without compile-time proxy generation.
- **Dynamic Object Casting:** Deserialize SOAP responses on-the-fly into strongly-typed POCOs using generic type parameters or runtime `Type` objects.
- **SOAP Attachments:** Built-in support for **SOAP with Attachments (SwA)** and **Message Transmission Optimization Mechanism (MTOM)** with automatic parameter mapping.
- **Lightweight Resilience Engine:** Polly-less, thread-safe Retry and Circuit Breaker policies to manage transient network/service failures.
- **SOAP Traffic Inspector:** Enable diagnostics to capture request/response logs, HTTP/SOAP headers, execution timing, and SOAP Fault analyses.
- **Robust Authentication:** Native support for Anonymous, Basic Auth, Bearer Token, API Key, NTLM, and **WS-Security UsernameToken** (Plaintext & Password Digest).
- **Strongly-Typed Code Generation:** Generate C# POCOs, modern C# Records, interface contracts, and client wrappers on-the-fly.
- **Roslyn Source Generators:** Optional compilation-time proxy generation to eliminate reflection overhead entirely.
- **OpenAPI & JSON Schema Conversion:** Convert WSDL documents into OpenAPI 3.x specifications and XSD schemas to JSON schemas.
- **Zero Third-Party Dependencies:** Relying strictly on .NET BCL types (`System.Xml`, `System.Xml.Linq`, `System.Net.Http`) for maximum performance and security compliance.

---

## Installation

Add the core package to your project:

```shell
dotnet add package SmartWsdlKit
```

To enable compile-time generation, add the source generator package:

```shell
dotnet add package SmartWsdlKit.SourceGenerators
```

---

## Quick Start

### Loading & Exploring a WSDL

```csharp
using System;
using System.Threading.Tasks;
using SmartWsdlKit;

// Explore high-level service metadata quickly
var info = await WsdlExplorer.ExploreAsync("http://webservices.oorsprong.org/websamples.countryinfo/CountryInfoService.wso?WSDL");
Console.WriteLine($"Service: {info.ServiceName}");
Console.WriteLine($"Operations: {string.Join(", ", info.SupportedOperations)}");

// Load and parse the full WSDL document
var wsdl = await WsdlDocument.LoadAsync("http://webservices.oorsprong.org/websamples.countryinfo/CountryInfoService.wso?WSDL");
foreach (var op in wsdl.Operations)
{
    Console.WriteLine($"Operation: {op.Name} | Action: {op.SoapAction}");
}
```

---

## SOAP Client Features

### Dynamic Invocation & Dictionaries

Execute SOAP calls dynamically without generating proxy classes:

```csharp
using System;
using System.Threading.Tasks;
using SmartWsdlKit;

var options = new SoapClientOptions
{
    BaseAddress = new Uri("http://webservices.oorsprong.org/websamples.countryinfo/CountryInfoService.wso"),
    Timeout = TimeSpan.FromSeconds(30)
};

using var client = new SoapClient(options);

// Invoke operation dynamically
var response = await client
    .Operation("CapitalCity")
    .With("sCountryISOCode", "TR")
    .ExecuteAsync();

// Read fields directly using dictionary
var resultDict = response.ToDictionary();
Console.WriteLine($"Capital City: {resultDict["CapitalCityResult"]}"); // Outputs: Ankara
```

### Strongly-Typed Response Casting

Annotate your target classes with standard C# `System.Xml.Serialization` attributes, and cast the response dynamically:

#### 1. Generic Casting (`ExecuteAsync<T>`)

```csharp
using System;
using System.Xml.Serialization;
using System.Threading.Tasks;
using SmartWsdlKit;

[XmlRoot(ElementName = "CapitalCityResponse", Namespace = "http://www.oorsprong.org/websamples.countryinfo")]
public class CapitalCityResponse
{
    [XmlElement(ElementName = "CapitalCityResult")]
    public string CapitalCityResult { get; set; } = string.Empty;
}

// Option A: Execute and cast directly using generic type parameters
var capitalCity = await client
    .Operation("CapitalCity")
    .With("sCountryISOCode", "TR")
    .ExecuteAsync<CapitalCityResponse>();

Console.WriteLine($"Capital City: {capitalCity.CapitalCityResult}"); // Outputs: Ankara

// Option B: Get raw response first, then deserialize/cast as needed
var response = await client
    .Operation("CapitalCity")
    .With("sCountryISOCode", "TR")
    .ExecuteAsync();

CapitalCityResponse result = response.As<CapitalCityResponse>();
Console.WriteLine($"Capital City: {result.CapitalCityResult}"); // Outputs: Ankara
```

#### 2. Runtime Type Parameter Casting (`ExecuteAsync(Type)`)

If you don't know the type at compile-time or need dynamic runtime dispatch, pass a `System.Type` parameter:

```csharp
using System;
using System.Threading.Tasks;
using SmartWsdlKit;

Type targetType = typeof(CapitalCityResponse);

// Option A: Execute and cast using runtime Type parameter
object objResult = await client
    .Operation("CapitalCity")
    .With("sCountryISOCode", "TR")
    .ExecuteAsync(targetType);

if (objResult is CapitalCityResponse capitalCity)
{
    Console.WriteLine($"Capital City: {capitalCity.CapitalCityResult}"); // Outputs: Ankara
}

// Option B: Deserialize raw response using Type parameter
var response = await client
    .Operation("CapitalCity")
    .With("sCountryISOCode", "TR")
    .ExecuteAsync();

object result = response.As(targetType);
```

### SOAP Attachments (SwA & MTOM)

SmartWsdlKit automatically detects parameters of type `SoapAttachment` and serializes them using the selected mode:

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using SmartWsdlKit;

var options = new SoapClientOptions
{
    BaseAddress = new Uri("http://mock-service.local/files"),
    AttachmentMode = SoapAttachmentMode.Mtom // or SoapAttachmentMode.SwA
};

using var client = new SoapClient(options);

// 1. Automatic mapping via parameters
var attachment = new SoapAttachment(
    contentId: "photo-attachment-1", 
    data: new byte[] { 1, 2, 3, 4 }, // byte array representing file
    contentType: "image/jpeg", 
    fileName: "profile.jpg"
);

var response = await client
    .Operation("UploadUserProfile")
    .With("Username", "john_doe")
    .With("PhotoData", attachment) // Automatically serialized (xop:Include for MTOM, href/cid for SwA)
    .ExecuteAsync();

// 2. Or add attachments explicitly to the request builder
var responseExplicit = await client
    .Operation("UploadRawFile")
    .WithAttachment("file-1", new byte[] { 5, 6, 7, 8 }, "application/pdf", "doc.pdf")
    .ExecuteAsync();
```

### Lightweight Resilience (Retry & Circuit Breaker)

Configure thread-safe retries and circuit breaking without adding bulky dependencies like Polly:

```csharp
using System;
using System.Threading.Tasks;
using SmartWsdlKit;

var options = new SoapClientOptions
{
    BaseAddress = new Uri("http://invalid-nonexistent-soap-server.local/service.asmx"),
    EnableResilience = true, // Turn on custom resilience engine
    RetryCount = 3,          // Retry up to 3 times on transient failures
    RetryDelay = TimeSpan.FromSeconds(1),
    CircuitBreakerFailureThreshold = 5,              // Trip circuit on 5 consecutive failures
    CircuitBreakerResetTimeout = TimeSpan.FromSeconds(30) // Wait 30s before Half-Open state
};

using var client = new SoapClient(options);

try
{
    var response = await client.Operation("PerformTransaction")
        .With("Amount", 150.00m)
        .ExecuteAsync();
}
catch (CircuitBreakerOpenException cbEx)
{
    Console.WriteLine($"Fast-failed! Circuit is open: {cbEx.Message}");
}
catch (SoapException ex)
{
    Console.WriteLine($"Request failed: {ex.Message}");
}
```

### SOAP Traffic Diagnostics Inspector

Track and analyze SOAP requests, responses, HTTP/SOAP headers, timing statistics, and soap faults:

```csharp
using System;
using System.Threading.Tasks;
using SmartWsdlKit;

var options = new SoapClientOptions
{
    BaseAddress = new Uri("http://webservices.oorsprong.org/websamples.countryinfo/CountryInfoService.wso"),
    EnableDiagnostics = true
};

using var client = new SoapClient(options);

// Invoke your calls...
var response = await client.Operation("CapitalCity")
    .With("sCountryISOCode", "TR")
    .ExecuteAsync();

// Inspect the traffic
var diagnostic = client.GetLastDiagnostic();
if (diagnostic != null)
{
    Console.WriteLine($"[Diagnostic Report - {diagnostic.OperationName}]");
    Console.WriteLine($"Endpoint URL: {diagnostic.EndpointUrl}");
    Console.WriteLine($"Status Code: {diagnostic.HttpStatusCode}");
    Console.WriteLine($"Success: {diagnostic.IsSuccess}");
    Console.WriteLine($"Execution Duration: {diagnostic.ExecutionTimeMs} ms");
    Console.WriteLine($"Request SOAP XML: \n{diagnostic.RequestXml}");
    Console.WriteLine($"Response SOAP XML: \n{diagnostic.ResponseXml}");

    // Inspect HTTP Headers
    foreach (var header in diagnostic.ResponseHeaders)
    {
        Console.WriteLine($"Header: {header.Key} = {header.Value}");
    }
}
```

### Custom HTTP & SOAP XML Headers

You can customize both HTTP protocol headers and SOAP `<soap:Header>` elements dynamically on a per-request basis:

```csharp
using System;
using System.Xml.Linq;
using System.Threading.Tasks;
using SmartWsdlKit;

var options = new SoapClientOptions
{
    BaseAddress = new Uri("http://webservices.oorsprong.org/websamples.countryinfo/CountryInfoService.wso")
};

using var client = new SoapClient(options);

// 1. Custom HTTP Headers
var responseHttp = await client.Operation("CapitalCity")
    .With("sCountryISOCode", "TR")
    .WithHttpHeader("X-Client-Id", "smart-client-99")
    .WithHttpHeader("X-Custom-Auth", "token-xyz-123")
    .ExecuteAsync();

// 2. Custom SOAP XML Headers (placed inside <soap:Header>)
var responseSoap = await client.Operation("CapitalCity")
    .With("sCountryISOCode", "TR")
    // A. With default target namespace mapping: <tns:ApiKey>123456</tns:ApiKey>
    .WithSoapHeader("ApiKey", "123456")
    
    // B. With custom namespace mapping: <ns:SessionId xmlns:ns="http://schemas.example.com">session-abc</ns:SessionId>
    .WithSoapHeader("SessionId", "http://schemas.example.com", "session-abc")
    
    // C. With complex custom XML structure using XElement:
    .WithSoapHeader(new XElement("UserInfo",
        new XElement("UserId", "user-100"),
        new XElement("UserRole", "Supervisor")
    ))
    .ExecuteAsync();
```

### Strongly-Typed Client Code Generation

Generate the C# DTO types and client wrapper from the WSDL first:

```csharp
using System.IO;
using System.Threading.Tasks;
using SmartWsdlKit;

// 1. Load WSDL
var wsdl = await WsdlDocument.LoadAsync("http://webservices.oorsprong.org/websamples.countryinfo/CountryInfoService.wso?WSDL");

// 2. Generate the C# DTO types and proxy client wrapper
var csharpCode = WsdlCodeGenerator.Generate(wsdl, new CodeGeneratorOptions
{
    Namespace = "MyService",
    GenerateRecords = true
});
File.WriteAllText("GeneratedClient.cs", csharpCode);
```

Then consume the generated client in your application:

```csharp
using System;
using System.Threading.Tasks;
using MyService;
using SmartWsdlKit;

class Program
{
    static async Task Main()
    {
        var options = new SoapClientOptions
        {
            BaseAddress = new Uri("http://webservices.oorsprong.org/websamples.countryinfo/CountryInfoService.wso")
        };

        // Consume the generated client wrapper
        using var client = new CountryInfoServiceSoapTypeClient(options);
        
        var request = new CapitalCityRequest { sCountryISOCode = "TR" };
        var response = await client.CapitalCityAsync(request);

        Console.WriteLine($"Capital Name: {response.CapitalCityResult}"); // Outputs: Ankara
    }
}
```

---

## Authentication Configurations

SmartWsdlKit provides clean and fluent configurations for securing your SOAP integrations:

```csharp
using System;
using SmartWsdlKit;

var options = new SoapClientOptions
{
    BaseAddress = new Uri("http://webservices.oorsprong.org/websamples.countryinfo/CountryInfoService.wso")
};

using var client = new SoapClient(options);

// 1. Basic Authentication
client.WithBasicAuth("admin_user", "secure_password_123");

// 2. Bearer Token
client.WithBearerToken("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...");

// 3. WS-Security UsernameToken (Plaintext)
client.WithWsSecurity("WsdlUser", "SecretPassword123", WsSecurityPasswordType.Plaintext);

// 4. WS-Security UsernameToken (Password Digest with SHA1, Nonce, and Created timestamp)
client.WithWsSecurity("WsdlUser", "SecretPassword123", WsSecurityPasswordType.PasswordDigest);

// 5. API Key (Header or Query)
client.WithApiKey("X-API-Key", "api-key-value-98765", ApiKeyLocation.Header);

// 6. NTLM / Windows Authentication
client.WithNtlmAuth("win_user", "win_pass", "my_domain");
```

---

## OpenAPI & JSON Schema Generation

Convert standard WSDL schemas into OpenAPI 3.0 specs to wrap SOAP endpoints behind API Gateways:

```csharp
using System.IO;
using System.Threading.Tasks;
using SmartWsdlKit;

var wsdl = await WsdlDocument.LoadAsync("http://webservices.oorsprong.org/websamples.countryinfo/CountryInfoService.wso?WSDL");

var openApiJson = OpenApiGenerator.Generate(wsdl);
File.WriteAllText("openapi-spec.json", openApiJson);
```

---

## Performance Notes & Best Practices

1. **Reuse SoapClient:** SoapClient manages an underlying `HttpClient` instance. Keep it as a singleton or register it as a long-lived dependency to prevent socket exhaustion.
2. **Culture Invariance:** SmartWsdlKit automatically formats parameters (like `decimal`, `double`, and `DateTime`) using `CultureInfo.InvariantCulture`. Avoid manual string formatting which might clash with localized system cultures (e.g. Turkish `İ/i` or European comma separators).
3. **SOAP Fault Handling:** If the service returns a SOAP Fault, SmartWsdlKit parses the Fault information and throws a structured `SoapFaultException` containing the code, reason, and detail elements.
4. **Non-200 Response Fallback:** If a SOAP endpoint returns a non-200 status code and the response is not valid XML (e.g. gateway HTML error pages), SmartWsdlKit extracts the raw HTML text as a backup plan and includes it inside the thrown `SoapException`.

---

## Troubleshooting

- **SocketException (No such host is known):** Verify that the `BaseAddress` in your `SoapClientOptions` is correct, or check if the target endpoint is reachable.
- **WsdlParseException:** Ensure the WSDL URL is accessible and points to a valid WSDL 1.1 or 2.0 structure. Relative imports are resolved automatically, but require network/file permissions.

---

## License

This project is licensed under the **MIT License**.