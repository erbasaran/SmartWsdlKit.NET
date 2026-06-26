# SmartWsdlKit.SourceGenerators

[![NuGet Version](https://img.shields.io/nuget/v/SmartWsdlKit.SourceGenerators.svg)](https://www.nuget.org/packages/SmartWsdlKit.SourceGenerators)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**SmartWsdlKit.SourceGenerators** is a companion Roslyn Incremental Source Generator package for the core `SmartWsdlKit` SDK.

It automatically parses local WSDL files at compile-time and generates strongly-typed, modern C# SOAP client proxies, service interfaces, and DTO record models. This eliminates the need for legacy Visual Studio Connected Services, `svcutil` command line tools, and dynamic reflection overhead entirely.

## Why Use Source Generators?

1. **Zero Runtime Reflection:** All XML serialization and method mapping code is generated during compilation, providing maximum execution speed.
2. **Instant IntelliSense:** Generated proxies are available immediately in your IDE as soon as you save your WSDL file.
3. **Compile-Time Safety:** If the service contract changes or the WSDL is malformed, compiler warnings/errors are reported directly in your build output or error list.
4. **Modern C# Code:** Generates immutable C# 9.0 Records for DTOs and clean asynchronous interfaces.

---

## Installation

Add both the core runtime package and the source generator package to your C# project:

```shell
dotnet add package SmartWsdlKit
dotnet add package SmartWsdlKit.SourceGenerators
```

---

## How to Configure

### 1. Add your WSDL File
Add your WSDL file (e.g. `Calculator.wsdl`) to your project structure (for example, in a `Services/` directory).

### 2. Register WSDL as AdditionalFiles
Open your project's `.csproj` file and register the WSDL file inside an `<ItemGroup>` using the `<AdditionalFiles>` build action:

```xml
<ItemGroup>
  <!-- Register the WSDL file for compilation-time processing -->
  <AdditionalFiles Include="Services/Calculator.wsdl" />
</ItemGroup>
```

### 3. Build & Run
When you build your project, the Incremental Source Generator will automatically run and output strongly typed classes under the `SmartWsdlKit.Generated` namespace.

---

## Consumption Example

Assuming your `Calculator.wsdl` defines a `Calculator` service with an `Add` operation, you can consume the generated client directly:

```csharp
using System;
using System.Threading.Tasks;
using SmartWsdlKit;
using SmartWsdlKit.Generated; // Generated namespace

class Program
{
    static async Task Main()
    {
        var options = new SoapClientOptions
        {
            BaseAddress = new Uri("http://www.dneonline.com/calculator.asmx"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        // Instantiating the generated client proxy
        using var client = new CalculatorSoapClient(options);

        // Calling the generated asynchronous method
        var response = await client.AddAsync(new AddRequest
        {
            intA = 15,
            intB = 25
        });

        Console.WriteLine($"Result: {response.AddResult}"); // Outputs: 40
    }
}
```

---

## Configuration Customizations
By default, the source generator:
- Places generated code in the `SmartWsdlKit.Generated` namespace.
- Emits DTOs as C# 9.0 `record` types.
- Enables C# Nullable Reference Types (`#nullable enable`).

---

## Diagnostic Rules

The generator analyzes the WSDL structure during compilation and reports validation issues directly to the compiler output:

| Diagnostic ID | Severity | Title | Description |
|---|---|---|---|
| **SWK001** | Warning | WSDL Parsing Failed | Reported when the WSDL file is malformed, cannot be parsed, or imports cannot be resolved recursively. |

---

## License

This project is licensed under the **MIT License**.
