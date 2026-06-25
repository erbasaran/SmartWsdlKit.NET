using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SmartWsdlKit.SourceGenerators
{
	/// <summary>
	/// Roslyn incremental source generator that translates WSDL files into C# proxy clients at compile time.
	/// </summary>
	[Generator]
	public class WsdlClientSourceGenerator : IIncrementalGenerator
	{
		private static readonly DiagnosticDescriptor WarningDescriptor = new DiagnosticDescriptor(
			id: "SWK001",
			title: "WSDL Parsing failed",
			messageFormat: "Failed to parse WSDL file '{0}': {1}",
			category: "SmartWsdlKitGenerator",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true);

		/// <summary>
		/// Initializes the generator pipeline.
		/// </summary>
		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			// Find all AdditionalFiles ending in .wsdl
			var wsdlFiles = context.AdditionalTextsProvider
				.Where(file => file.Path.EndsWith(".wsdl", StringComparison.OrdinalIgnoreCase) ||
							   file.Path.EndsWith(".wsdl.xml", StringComparison.OrdinalIgnoreCase));

			// Generate source code for each WSDL file
			context.RegisterSourceOutput(wsdlFiles, (spc, additionalFile) =>
			{
				var filePath = additionalFile.Path;
				try
				{
					// WsdlParser will load the file path and resolve imports recursively
					var wsdl = WsdlParser.Load(filePath);

					var options = new CodeGeneratorOptions
					{
						Namespace = "SmartWsdlKit.Generated",
						GenerateRecords = true, // Default to records for modern source generators
						EnableNullableReferenceTypes = true
					};

					var generatedCode = WsdlCodeGenerator.Generate(wsdl, options);
					var hintName = Path.GetFileNameWithoutExtension(filePath) + ".g.cs";

					spc.AddSource(hintName, SourceText.From(generatedCode, Encoding.UTF8));
				}
				catch (Exception ex)
				{
					spc.ReportDiagnostic(Diagnostic.Create(
						WarningDescriptor,
						Location.None,
						Path.GetFileName(filePath),
						ex.Message));
				}
			});
		}
	}
}
