namespace SmartWsdlKit
{
	/// <summary>
	/// Represents the generated report from analyzing a WSDL service.
	/// </summary>
	public class WsdlAnalysisReport
	{
		/// <summary>
		/// Gets the list of services found in the WSDL.
		/// </summary>
		public IReadOnlyList<string> Services { get; internal set; } = Array.Empty<string>();

		/// <summary>
		/// Gets the list of operation names discovered.
		/// </summary>
		public IReadOnlyList<string> Operations { get; internal set; } = Array.Empty<string>();

		/// <summary>
		/// Gets the list of schema namespaces imported.
		/// </summary>
		public IReadOnlyList<string> ImportedSchemas { get; internal set; } = Array.Empty<string>();

		/// <summary>
		/// Gets the complexity score of the SOAP service.
		/// </summary>
		public int ComplexityScore { get; internal set; }

		/// <summary>
		/// Gets any warnings regarding security, version compatibility, or protocol constraints.
		/// </summary>
		public IReadOnlyList<string> Warnings { get; internal set; } = Array.Empty<string>();

		/// <summary>
		/// Gets validation issues found in the WSDL structure.
		/// </summary>
		public IReadOnlyList<string> ValidationIssues { get; internal set; } = Array.Empty<string>();
	}

	/// <summary>
	/// Inspects and analyzes WSDL definitions.
	/// </summary>
	public static class WsdlAnalyzer
	{
		/// <summary>
		/// Generates a comprehensive inspection report for a parsed WSDL document.
		/// </summary>
		public static WsdlAnalysisReport Analyze(WsdlDocument wsdl)
		{
			if (wsdl == null)
				throw new ArgumentNullException(nameof(wsdl));

			var report = new WsdlAnalysisReport();
			var warnings = new List<string>();
			var validationIssues = new List<string>();

			// Collect names
			report.Services = wsdl.Services.Select(s => s.Name).ToList();
			report.Operations = wsdl.Operations.Select(o => o.Name).ToList();

			// Imported schemas namespaces
			var schemasList = new List<string>();
			foreach (System.Xml.Schema.XmlSchema schema in wsdl.Schemas.Schemas())
			{
				if (schema.TargetNamespace != null && !schemasList.Contains(schema.TargetNamespace))
				{
					schemasList.Add(schema.TargetNamespace);
				}
			}
			report.ImportedSchemas = schemasList;

			// Calculate complexity score
			// Formula: Operations * 3 + Services * 5 + Schema Types * 2 + Imports * 10
			int typeCount = wsdl.Schemas.GlobalTypes.Count;
			int elementCount = wsdl.Schemas.GlobalElements.Count;
			int complexity = (wsdl.Operations.Count * 3) +
							 (wsdl.Services.Count * 5) +
							 ((typeCount + elementCount) * 2) +
							 (wsdl.Imports.Count * 10);
			report.ComplexityScore = complexity;

			// Security check: HTTP vs HTTPS endpoints
			foreach (var svc in wsdl.Services)
			{
				foreach (var endpoint in svc.Endpoints)
				{
					if (endpoint.Address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
					{
						warnings.Add($"Insecure endpoint location found on service '{svc.Name}' port '{endpoint.Name}': '{endpoint.Address}'. Consider using HTTPS.");
					}
				}
			}

			// SOAP Version check
			bool hasSoap11 = false;
			foreach (var binding in wsdl.Bindings)
			{
				if (binding.SoapVersion == SoapVersion.Soap11)
				{
					hasSoap11 = true;
				}
			}
			if (hasSoap11)
			{
				warnings.Add("WSDL contains legacy SOAP 1.1 bindings. Upgrading to SOAP 1.2 is recommended where supported.");
			}

			// Validation issues
			if (wsdl.Services.Count == 0)
			{
				validationIssues.Add("WSDL does not define any active service endpoints.");
			}
			if (wsdl.Operations.Count == 0)
			{
				validationIssues.Add("No operations found across WSDL interface contracts.");
			}

			// Check if any operations lack elements in schema
			foreach (var op in wsdl.Operations)
			{
				if (string.IsNullOrEmpty(op.InputElementName))
				{
					validationIssues.Add($"Operation '{op.Name}' is missing an input element definition.");
				}
			}

			report.Warnings = warnings;
			report.ValidationIssues = validationIssues;

			return report;
		}
	}
}
