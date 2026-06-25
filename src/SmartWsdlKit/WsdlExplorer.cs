namespace SmartWsdlKit
{
	/// <summary>
	/// Lightweight discovery summary of a WSDL SOAP service.
	/// </summary>
	public class WsdlServiceInfo
	{
		/// <summary>
		/// Gets the name of the service.
		/// </summary>
		public string ServiceName { get; internal set; } = string.Empty;

		/// <summary>
		/// Gets the list of concrete endpoint URLs.
		/// </summary>
		public IReadOnlyList<string> EndpointUrls { get; internal set; } = Array.Empty<string>();

		/// <summary>
		/// Gets the SOAP version protocols supported.
		/// </summary>
		public IReadOnlyList<string> SoapVersions { get; internal set; } = Array.Empty<string>();

		/// <summary>
		/// Gets the names of all operations supported.
		/// </summary>
		public IReadOnlyList<string> SupportedOperations { get; internal set; } = Array.Empty<string>();
	}

	/// <summary>
	/// Service discovery utility to inspect SOAP endpoints without loading code structures.
	/// </summary>
	public static class WsdlExplorer
	{
		/// <summary>
		/// Explores a WSDL address and returns a metadata summary.
		/// </summary>
		public static WsdlServiceInfo Explore(string url)
		{
			var wsdl = WsdlDocument.Load(url);
			return MapWsdlToInfo(wsdl);
		}

		/// <summary>
		/// Explores a WSDL address asynchronously and returns a metadata summary.
		/// </summary>
		public static async Task<WsdlServiceInfo> ExploreAsync(string url)
		{
			var wsdl = await WsdlDocument.LoadAsync(url).ConfigureAwait(false);
			return MapWsdlToInfo(wsdl);
		}

		private static WsdlServiceInfo MapWsdlToInfo(WsdlDocument wsdl)
		{
			var serviceName = wsdl.Services.FirstOrDefault()?.Name ?? "UnknownService";
			var endpoints = wsdl.Services.SelectMany(s => s.Endpoints).Select(e => e.Address).Distinct().ToList();
			var versions = wsdl.Bindings.Select(b => b.SoapVersion.ToString()).Distinct().ToList();
			var ops = wsdl.Operations.Select(o => o.Name).Distinct().ToList();

			return new WsdlServiceInfo
			{
				ServiceName = serviceName,
				EndpointUrls = endpoints,
				SoapVersions = versions,
				SupportedOperations = ops
			};
		}
	}
}
