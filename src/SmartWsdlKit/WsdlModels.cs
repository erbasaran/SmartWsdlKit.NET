using System.Xml.Schema;

namespace SmartWsdlKit
{
	/// <summary>
	/// Represents a parsed WSDL definition document supporting both WSDL 1.1 and 2.0.
	/// </summary>
	public class WsdlDocument
	{
		/// <summary>
		/// Gets the target namespace of the WSDL document.
		/// </summary>
		public string TargetNamespace { get; internal set; } = string.Empty;

		/// <summary>
		/// Gets the services exposed by this WSDL.
		/// </summary>
		public IReadOnlyList<WsdlService> Services { get; internal set; } = Array.Empty<WsdlService>();

		/// <summary>
		/// Gets the bindings defined in this WSDL.
		/// </summary>
		public IReadOnlyList<WsdlBinding> Bindings { get; internal set; } = Array.Empty<WsdlBinding>();

		/// <summary>
		/// Gets the interfaces (portTypes in WSDL 1.1) defined in this WSDL.
		/// </summary>
		public IReadOnlyList<WsdlInterface> Interfaces { get; internal set; } = Array.Empty<WsdlInterface>();

		/// <summary>
		/// Gets all operations across all interfaces.
		/// </summary>
		public IReadOnlyList<WsdlOperation> Operations { get; internal set; } = Array.Empty<WsdlOperation>();

		/// <summary>
		/// Gets the compiled XML Schema set containing all types defined or imported in this WSDL.
		/// </summary>
		public XmlSchemaSet Schemas { get; internal set; } = new XmlSchemaSet();

		/// <summary>
		/// Gets the list of raw imported documents (WSDLs/XSDs) resolved during loading.
		/// </summary>
		public IReadOnlyList<string> Imports { get; internal set; } = Array.Empty<string>();

		/// <summary>
		/// Loads a WSDL document from a URI (file or HTTP/HTTPS).
		/// </summary>
		public static WsdlDocument Load(string url)
		{
			return WsdlParser.Load(url);
		}

		/// <summary>
		/// Loads a WSDL document asynchronously from a URI.
		/// </summary>
		public static System.Threading.Tasks.Task<WsdlDocument> LoadAsync(string url)
		{
			return WsdlParser.LoadAsync(url);
		}
	}

	/// <summary>
	/// Represents a service in the WSDL.
	/// </summary>
	public class WsdlService
	{
		/// <summary>
		/// Gets the name of the service.
		/// </summary>
		public string Name { get; internal set; } = string.Empty;

		/// <summary>
		/// Gets the documentation, if any.
		/// </summary>
		public string? Documentation { get; internal set; }

		/// <summary>
		/// Gets the endpoints (ports) associated with this service.
		/// </summary>
		public IReadOnlyList<WsdlEndpoint> Endpoints { get; internal set; } = Array.Empty<WsdlEndpoint>();
	}

	/// <summary>
	/// Represents an endpoint (port in WSDL 1.1, endpoint in WSDL 2.0).
	/// </summary>
	public class WsdlEndpoint
	{
		/// <summary>
		/// Gets the name of the endpoint.
		/// </summary>
		public string Name { get; internal set; } = string.Empty;

		/// <summary>
		/// Gets the binding associated with this endpoint.
		/// </summary>
		public WsdlBinding Binding { get; internal set; } = null!;

		/// <summary>
		/// Gets the address location URL.
		/// </summary>
		public string Address { get; internal set; } = string.Empty;
	}

	/// <summary>
	/// Represents a binding in the WSDL.
	/// </summary>
	public class WsdlBinding
	{
		/// <summary>
		/// Gets the name of the binding.
		/// </summary>
		public string Name { get; internal set; } = string.Empty;

		/// <summary>
		/// Gets the associated interface (portType in WSDL 1.1).
		/// </summary>
		public WsdlInterface Interface { get; internal set; } = null!;

		/// <summary>
		/// Gets the transport protocol URI (e.g. http://schemas.xmlsoap.org/soap/http).
		/// </summary>
		public string Transport { get; internal set; } = string.Empty;

		/// <summary>
		/// Gets the SOAP version of this binding.
		/// </summary>
		public SoapVersion SoapVersion { get; internal set; } = SoapVersion.Soap11;

		/// <summary>
		/// Gets the binding style ("document" or "rpc").
		/// </summary>
		public string Style { get; internal set; } = "document";

		/// <summary>
		/// Gets operation-specific binding details.
		/// </summary>
		public IReadOnlyDictionary<string, WsdlBindingOperation> Operations { get; internal set; } = new Dictionary<string, WsdlBindingOperation>();
	}

	/// <summary>
	/// Represents binding-specific details for an operation.
	/// </summary>
	public class WsdlBindingOperation
	{
		/// <summary>
		/// Gets the operation name.
		/// </summary>
		public string Name { get; internal set; } = string.Empty;

		/// <summary>
		/// Gets the SOAP action URI.
		/// </summary>
		public string SoapAction { get; internal set; } = string.Empty;

		/// <summary>
		/// Gets the soap:body binding style override ("document" or "rpc").
		/// </summary>
		public string Style { get; internal set; } = "document";
	}

	/// <summary>
	/// Represents an interface (portType in WSDL 1.1, interface in WSDL 2.0).
	/// </summary>
	public class WsdlInterface
	{
		/// <summary>
		/// Gets the name of the interface.
		/// </summary>
		public string Name { get; internal set; } = string.Empty;

		/// <summary>
		/// Gets the list of operations exposed by this interface.
		/// </summary>
		public IReadOnlyList<WsdlOperation> Operations { get; internal set; } = Array.Empty<WsdlOperation>();
	}

	/// <summary>
	/// Represents a single service operation.
	/// </summary>
	public class WsdlOperation
	{
		/// <summary>
		/// Gets the name of the operation.
		/// </summary>
		public string Name { get; internal set; } = string.Empty;

		/// <summary>
		/// Gets the associated WsdlInterface.
		/// </summary>
		public WsdlInterface Interface { get; internal set; } = null!;

		/// <summary>
		/// Gets the input message name.
		/// </summary>
		public string? InputMessageName { get; internal set; }

		/// <summary>
		/// Gets the input element local XML name defined in XSD schemas.
		/// </summary>
		public string? InputElementName { get; internal set; }

		/// <summary>
		/// Gets the target namespace of the input XML element.
		/// </summary>
		public string? InputElementNamespace { get; internal set; }

		/// <summary>
		/// Gets the output message name.
		/// </summary>
		public string? OutputMessageName { get; internal set; }

		/// <summary>
		/// Gets the output element local XML name defined in XSD schemas.
		/// </summary>
		public string? OutputElementName { get; internal set; }

		/// <summary>
		/// Gets the target namespace of the output XML element.
		/// </summary>
		public string? OutputElementNamespace { get; internal set; }

		/// <summary>
		/// Gets the soapAction associated with this operation.
		/// </summary>
		public string SoapAction { get; internal set; } = string.Empty;

		/// <summary>
		/// Gets the style of the operation ("document" or "rpc").
		/// </summary>
		public string Style { get; internal set; } = "document";
	}

	/// <summary>
	/// Supported SOAP protocol versions.
	/// </summary>
	public enum SoapVersion
	{
		/// <summary>
		/// SOAP 1.1 Protocol (http://schemas.xmlsoap.org/soap/envelope/)
		/// </summary>
		Soap11,

		/// <summary>
		/// SOAP 1.2 Protocol (http://www.w3.org/2003/05/soap-envelope)
		/// </summary>
		Soap12,

		/// <summary>
		/// Non-SOAP HTTP binding.
		/// </summary>
		Http
	}
}
