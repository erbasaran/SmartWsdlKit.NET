using System.Xml.Linq;
using System.Xml.Schema;

namespace SmartWsdlKit
{
	/// <summary>
	/// Parses WSDL 1.1 and 2.0 documents into a WsdlDocument structure.
	/// </summary>
	public static class WsdlParser
	{
		private static readonly HttpClient SharedHttpClient = new HttpClient();

		private static readonly XNamespace Wsdl11Ns = "http://schemas.xmlsoap.org/wsdl/";
		private static readonly XNamespace Soap11Ns = "http://schemas.xmlsoap.org/wsdl/soap/";
		private static readonly XNamespace Soap12Ns = "http://schemas.xmlsoap.org/wsdl/soap12/";
		private static readonly XNamespace XsdNs = "http://www.w3.org/2001/XMLSchema";

		private static readonly XNamespace Wsdl20Ns = "http://www.w3.org/ns/wsdl";
		private static readonly XNamespace Wsoap20Ns = "http://www.w3.org/ns/wsdl/soap";

		/// <summary>
		/// Loads and parses a WSDL document from a URI.
		/// </summary>
		public static WsdlDocument Load(string url)
		{
			return Task.Run(() => LoadAsync(url)).GetAwaiter().GetResult();
		}

		/// <summary>
		/// Loads and parses a WSDL document asynchronously from a URI.
		/// </summary>
		public static async Task<WsdlDocument> LoadAsync(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
				throw new ArgumentException("URL or path cannot be null or empty", nameof(url));

			var loadedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var schemas = new XmlSchemaSet();

			XDocument mainDoc;
			string baseUri = url;

			if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
				url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				var content = await SharedHttpClient.GetStringAsync(url).ConfigureAwait(false);
				mainDoc = XDocument.Parse(content);
				loadedUrls.Add(url);
			}
			else
			{
				var fullPath = Path.GetFullPath(url);
				baseUri = fullPath;
				using var stream = File.OpenRead(fullPath);
				mainDoc = XDocument.Load(stream);
				loadedUrls.Add(fullPath);
			}

			var imports = new List<string> { baseUri };

			// Recursively load WSDL and Schema imports
			await LoadImportsRecursiveAsync(mainDoc, baseUri, loadedUrls, schemas, imports).ConfigureAwait(false);

			// Parse Schema Types declared directly inside WSDL
			ParseInlineSchemas(mainDoc, schemas);

			// Compile the schema set
			schemas.Compile();

			// Check WSDL format (1.1 vs 2.0)
			var rootName = mainDoc.Root?.Name;
			if (rootName == null)
				throw new WsdlParseException("Empty or invalid XML document root.");

			var doc = new WsdlDocument { Schemas = schemas, Imports = imports };

			if (rootName.Namespace == Wsdl11Ns || rootName.LocalName == "definitions")
			{
				ParseWsdl11(mainDoc, doc);
			}
			else if (rootName.Namespace == Wsdl20Ns || rootName.LocalName == "description")
			{
				ParseWsdl20(mainDoc, doc);
			}
			else
			{
				// Try parsing as WSDL 1.1 fallback if namespace is missing but structures match
				ParseWsdl11(mainDoc, doc);
			}

			return doc;
		}

		private static async Task LoadImportsRecursiveAsync(
			XDocument doc,
			string baseUri,
			HashSet<string> loadedUrls,
			XmlSchemaSet schemas,
			List<string> imports)
		{
			if (doc.Root == null) return;

			// Find all imports in WSDL (<import> elements)
			var wsdlImports = doc.Root.Elements().Where(e => e.Name.LocalName == "import");
			foreach (var importEl in wsdlImports)
			{
				var location = importEl.Attribute("location")?.Value;
				if (string.IsNullOrEmpty(location)) continue;

				var resolvedUrl = ResolveUrl(baseUri, location);
				if (loadedUrls.Contains(resolvedUrl)) continue;

				try
				{
					XDocument importDoc;
					if (resolvedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
						resolvedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
					{
						var content = await SharedHttpClient.GetStringAsync(resolvedUrl).ConfigureAwait(false);
						importDoc = XDocument.Parse(content);
					}
					else
					{
						using var stream = File.OpenRead(resolvedUrl);
						importDoc = XDocument.Load(stream);
					}

					loadedUrls.Add(resolvedUrl);
					imports.Add(resolvedUrl);

					// Parse schemas in the imported WSDL
					ParseInlineSchemas(importDoc, schemas);

					// Recursively load imports of the imported WSDL
					await LoadImportsRecursiveAsync(importDoc, resolvedUrl, loadedUrls, schemas, imports).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					throw new WsdlParseException($"Failed to load imported WSDL from '{resolvedUrl}': {ex.Message}", ex);
				}
			}

			// Find schema imports inside <types> (<xsd:import> or <xsd:include>)
			var typesEl = doc.Root.Elements().FirstOrDefault(e => e.Name.LocalName == "types");
			if (typesEl != null)
			{
				var schemaElements = typesEl.Elements().Where(e => e.Name.LocalName == "schema");
				foreach (var schemaEl in schemaElements)
				{
					var xsdImports = schemaEl.Elements().Where(e => e.Name.LocalName == "import" || e.Name.LocalName == "include");
					foreach (var xsdImport in xsdImports)
					{
						var schemaLocation = xsdImport.Attribute("schemaLocation")?.Value;
						if (string.IsNullOrEmpty(schemaLocation)) continue;

						var resolvedUrl = ResolveUrl(baseUri, schemaLocation);
						if (loadedUrls.Contains(resolvedUrl)) continue;

						try
						{
							string content;
							if (resolvedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
								resolvedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
							{
								content = await SharedHttpClient.GetStringAsync(resolvedUrl).ConfigureAwait(false);
							}
							else
							{
								using (var reader = File.OpenText(resolvedUrl))
								{
									content = await reader.ReadToEndAsync().ConfigureAwait(false);
								}
							}

							loadedUrls.Add(resolvedUrl);
							imports.Add(resolvedUrl);

							using var textReader = new StringReader(content);
							var schema = XmlSchema.Read(textReader, null);
							if (schema != null)
							{
								schemas.Add(schema);
							}

							// Recursively load imports of the schema itself
							var xsdDoc = XDocument.Parse(content);
							await LoadImportsRecursiveAsync(xsdDoc, resolvedUrl, loadedUrls, schemas, imports).ConfigureAwait(false);
						}
						catch (Exception ex)
						{
							throw new WsdlParseException($"Failed to load imported schema from '{resolvedUrl}': {ex.Message}", ex);
						}
					}
				}
			}
		}

		private static void ParseInlineSchemas(XDocument doc, XmlSchemaSet schemas)
		{
			if (doc.Root == null) return;

			var typesEl = doc.Root.Elements().FirstOrDefault(e => e.Name.LocalName == "types");
			if (typesEl == null) return;

			var schemaElements = typesEl.Elements().Where(e => e.Name.LocalName == "schema");
			foreach (var schemaEl in schemaElements)
			{
				using var reader = schemaEl.CreateReader();
				var schema = XmlSchema.Read(reader, null);
				if (schema != null)
				{
					schemas.Add(schema);
				}
			}
		}

		private static string ResolveUrl(string baseUri, string relativeUrl)
		{
			if (Uri.TryCreate(relativeUrl, UriKind.Absolute, out var absUri))
			{
				return absUri.OriginalString;
			}

			if (baseUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
				baseUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				var baseAbsoluteUri = new Uri(baseUri);
				var resolvedUri = new Uri(baseAbsoluteUri, relativeUrl);
				return resolvedUri.AbsoluteUri;
			}

			var baseDir = Path.GetDirectoryName(baseUri) ?? string.Empty;
			return Path.GetFullPath(Path.Combine(baseDir, relativeUrl));
		}

		private static void ParseWsdl11(XDocument doc, WsdlDocument wsdl)
		{
			if (doc.Root == null) return;

			wsdl.TargetNamespace = doc.Root.Attribute("targetNamespace")?.Value ?? string.Empty;

			var messages = new Dictionary<string, XElement>();
			foreach (var msgEl in doc.Root.Elements().Where(e => e.Name.LocalName == "message"))
			{
				var name = msgEl.Attribute("name")?.Value;
				if (!string.IsNullOrEmpty(name))
				{
					messages[name] = msgEl;
				}
			}

			// 1. Interfaces (portTypes)
			var interfaces = new List<WsdlInterface>();
			var interfaceMap = new Dictionary<string, WsdlInterface>();

			foreach (var ptEl in doc.Root.Elements().Where(e => e.Name.LocalName == "portType"))
			{
				var ptName = ptEl.Attribute("name")?.Value ?? string.Empty;
				var wsdlInterface = new WsdlInterface { Name = ptName };

				var operations = new List<WsdlOperation>();
				foreach (var opEl in ptEl.Elements().Where(e => e.Name.LocalName == "operation"))
				{
					var opName = opEl.Attribute("name")?.Value ?? string.Empty;
					var operation = new WsdlOperation
					{
						Name = opName,
						Interface = wsdlInterface
					};

					var inputEl = opEl.Element(opEl.Name.Namespace + "input");
					if (inputEl != null)
					{
						var msgAttr = inputEl.Attribute("message")?.Value;
						if (!string.IsNullOrEmpty(msgAttr))
						{
							var msgLocal = GetLocalName(msgAttr);
							operation.InputMessageName = msgLocal;

							// Resolve input element mapping from message part
							if (messages.TryGetValue(msgLocal, out var msgXml))
							{
								var partEl = msgXml.Elements().FirstOrDefault(e => e.Name.LocalName == "part");
								if (partEl != null)
								{
									var elementAttr = partEl.Attribute("element")?.Value;
									if (!string.IsNullOrEmpty(elementAttr))
									{
										operation.InputElementName = GetLocalName(elementAttr);
										operation.InputElementNamespace = ResolveNamespace(partEl, elementAttr);
									}
								}
							}
						}
					}

					var outputEl = opEl.Element(opEl.Name.Namespace + "output");
					if (outputEl != null)
					{
						var msgAttr = outputEl.Attribute("message")?.Value;
						if (!string.IsNullOrEmpty(msgAttr))
						{
							var msgLocal = GetLocalName(msgAttr);
							operation.OutputMessageName = msgLocal;

							if (messages.TryGetValue(msgLocal, out var msgXml))
							{
								var partEl = msgXml.Elements().FirstOrDefault(e => e.Name.LocalName == "part");
								if (partEl != null)
								{
									var elementAttr = partEl.Attribute("element")?.Value;
									if (!string.IsNullOrEmpty(elementAttr))
									{
										operation.OutputElementName = GetLocalName(elementAttr);
										operation.OutputElementNamespace = ResolveNamespace(partEl, elementAttr);
									}
								}
							}
						}
					}

					operations.Add(operation);
				}

				wsdlInterface.Operations = operations;
				interfaces.Add(wsdlInterface);
				interfaceMap[ptName] = wsdlInterface;
			}
			wsdl.Interfaces = interfaces;

			// 2. Bindings
			var bindings = new List<WsdlBinding>();
			var bindingMap = new Dictionary<string, WsdlBinding>();

			foreach (var bindEl in doc.Root.Elements().Where(e => e.Name.LocalName == "binding"))
			{
				var bName = bindEl.Attribute("name")?.Value ?? string.Empty;
				var ptAttr = bindEl.Attribute("type")?.Value ?? string.Empty;
				var ptLocalName = GetLocalName(ptAttr);

				if (!interfaceMap.TryGetValue(ptLocalName, out var wsdlInterface))
				{
					wsdlInterface = new WsdlInterface { Name = ptLocalName };
				}

				var binding = new WsdlBinding
				{
					Name = bName,
					Interface = wsdlInterface
				};

				// SOAP binding style & version
				var soapBind = bindEl.Element(Soap11Ns + "binding") ?? bindEl.Element(Soap12Ns + "binding");
				if (soapBind != null)
				{
					binding.SoapVersion = soapBind.Name.Namespace == Soap12Ns ? SoapVersion.Soap12 : SoapVersion.Soap11;
					binding.Transport = soapBind.Attribute("transport")?.Value ?? string.Empty;
					binding.Style = soapBind.Attribute("style")?.Value ?? "document";
				}
				else
				{
					// Fallback namespaces check
					var anySoapBind = bindEl.Elements().FirstOrDefault(e => e.Name.LocalName == "binding");
					if (anySoapBind != null && anySoapBind.Name.LocalName == "binding")
					{
						binding.SoapVersion = anySoapBind.Name.Namespace.NamespaceName.Contains("soap12") ? SoapVersion.Soap12 : SoapVersion.Soap11;
						binding.Transport = anySoapBind.Attribute("transport")?.Value ?? string.Empty;
						binding.Style = anySoapBind.Attribute("style")?.Value ?? "document";
					}
				}

				var bindingOps = new Dictionary<string, WsdlBindingOperation>();
				foreach (var bOpEl in bindEl.Elements().Where(e => e.Name.LocalName == "operation"))
				{
					var opName = bOpEl.Attribute("name")?.Value ?? string.Empty;
					var bOp = new WsdlBindingOperation { Name = opName };

					var soapOp = bOpEl.Element(Soap11Ns + "operation") ?? bOpEl.Element(Soap12Ns + "operation");
					if (soapOp == null)
					{
						soapOp = bOpEl.Elements().FirstOrDefault(e => e.Name.LocalName == "operation");
					}

					if (soapOp != null)
					{
						bOp.SoapAction = soapOp.Attribute("soapAction")?.Value ?? string.Empty;
						bOp.Style = soapOp.Attribute("style")?.Value ?? binding.Style;
					}

					bindingOps[opName] = bOp;

					// Sync SOAP actions back to WsdlOperation references inside Interface
					var targetOp = wsdlInterface.Operations.FirstOrDefault(o => o.Name.Equals(opName, StringComparison.OrdinalIgnoreCase));
					if (targetOp != null)
					{
						targetOp.SoapAction = bOp.SoapAction;
						targetOp.Style = bOp.Style;
					}
				}

				binding.Operations = bindingOps;
				bindings.Add(binding);
				bindingMap[bName] = binding;
			}
			wsdl.Bindings = bindings;

			// 3. Services & Endpoints
			var services = new List<WsdlService>();
			foreach (var svcEl in doc.Root.Elements().Where(e => e.Name.LocalName == "service"))
			{
				var sName = svcEl.Attribute("name")?.Value ?? string.Empty;
				var service = new WsdlService { Name = sName };

				var endpoints = new List<WsdlEndpoint>();
				foreach (var portEl in svcEl.Elements().Where(e => e.Name.LocalName == "port"))
				{
					var pName = portEl.Attribute("name")?.Value ?? string.Empty;
					var bAttr = portEl.Attribute("binding")?.Value ?? string.Empty;
					var bLocal = GetLocalName(bAttr);

					if (bindingMap.TryGetValue(bLocal, out var wsdlBinding))
					{
						var addressEl = portEl.Element(Soap11Ns + "address") ??
										portEl.Element(Soap12Ns + "address") ??
										portEl.Elements().FirstOrDefault(e => e.Name.LocalName == "address");

						var addressLocation = addressEl?.Attribute("location")?.Value ?? string.Empty;

						endpoints.Add(new WsdlEndpoint
						{
							Name = pName,
							Binding = wsdlBinding,
							Address = addressLocation
						});
					}
				}
				service.Endpoints = endpoints;
				services.Add(service);
			}
			wsdl.Services = services;

			// Flatten operations
			wsdl.Operations = wsdl.Interfaces.SelectMany(i => i.Operations).ToList();
		}

		private static void ParseWsdl20(XDocument doc, WsdlDocument wsdl)
		{
			if (doc.Root == null) return;

			wsdl.TargetNamespace = doc.Root.Attribute("targetNamespace")?.Value ?? string.Empty;

			// 1. Interfaces
			var interfaces = new List<WsdlInterface>();
			var interfaceMap = new Dictionary<string, WsdlInterface>();

			foreach (var ptEl in doc.Root.Elements(Wsdl20Ns + "interface"))
			{
				var ptName = ptEl.Attribute("name")?.Value ?? string.Empty;
				var wsdlInterface = new WsdlInterface { Name = ptName };

				var operations = new List<WsdlOperation>();
				foreach (var opEl in ptEl.Elements(Wsdl20Ns + "operation"))
				{
					var opName = opEl.Attribute("name")?.Value ?? string.Empty;
					var operation = new WsdlOperation
					{
						Name = opName,
						Interface = wsdlInterface
					};

					var inputEl = opEl.Element(Wsdl20Ns + "input");
					if (inputEl != null)
					{
						var elementAttr = inputEl.Attribute("element")?.Value;
						if (!string.IsNullOrEmpty(elementAttr))
						{
							operation.InputElementName = GetLocalName(elementAttr);
							operation.InputElementNamespace = ResolveNamespace(inputEl, elementAttr);
						}
					}

					var outputEl = opEl.Element(Wsdl20Ns + "output");
					if (outputEl != null)
					{
						var elementAttr = outputEl.Attribute("element")?.Value;
						if (!string.IsNullOrEmpty(elementAttr))
						{
							operation.OutputElementName = GetLocalName(elementAttr);
							operation.OutputElementNamespace = ResolveNamespace(outputEl, elementAttr);
						}
					}

					operations.Add(operation);
				}

				wsdlInterface.Operations = operations;
				interfaces.Add(wsdlInterface);
				interfaceMap[ptName] = wsdlInterface;
			}
			wsdl.Interfaces = interfaces;

			// 2. Bindings
			var bindings = new List<WsdlBinding>();
			var bindingMap = new Dictionary<string, WsdlBinding>();

			foreach (var bindEl in doc.Root.Elements(Wsdl20Ns + "binding"))
			{
				var bName = bindEl.Attribute("name")?.Value ?? string.Empty;
				var ptAttr = bindEl.Attribute("interface")?.Value ?? string.Empty;
				var ptLocalName = GetLocalName(ptAttr);

				if (!interfaceMap.TryGetValue(ptLocalName, out var wsdlInterface))
				{
					wsdlInterface = new WsdlInterface { Name = ptLocalName };
				}

				var binding = new WsdlBinding
				{
					Name = bName,
					Interface = wsdlInterface
				};

				// SOAP version
				var versionAttr = bindEl.Attribute(Wsoap20Ns + "version")?.Value;
				binding.SoapVersion = versionAttr == "1.2" ? SoapVersion.Soap12 : SoapVersion.Soap11;

				var bindingOps = new Dictionary<string, WsdlBindingOperation>();
				foreach (var bOpEl in bindEl.Elements(Wsdl20Ns + "operation"))
				{
					var opName = bOpEl.Attribute("ref")?.Value;
					if (string.IsNullOrEmpty(opName)) continue;
					opName = GetLocalName(opName);

					var bOp = new WsdlBindingOperation { Name = opName };
					bOp.SoapAction = bOpEl.Attribute(Wsoap20Ns + "action")?.Value ?? string.Empty;

					bindingOps[opName] = bOp;

					var targetOp = wsdlInterface.Operations.FirstOrDefault(o => o.Name.Equals(opName, StringComparison.OrdinalIgnoreCase));
					if (targetOp != null)
					{
						targetOp.SoapAction = bOp.SoapAction;
					}
				}

				binding.Operations = bindingOps;
				bindings.Add(binding);
				bindingMap[bName] = binding;
			}
			wsdl.Bindings = bindings;

			// 3. Services
			var services = new List<WsdlService>();
			foreach (var svcEl in doc.Root.Elements(Wsdl20Ns + "service"))
			{
				var sName = svcEl.Attribute("name")?.Value ?? string.Empty;
				var service = new WsdlService { Name = sName };

				var endpoints = new List<WsdlEndpoint>();
				foreach (var endEl in svcEl.Elements(Wsdl20Ns + "endpoint"))
				{
					var pName = endEl.Attribute("name")?.Value ?? string.Empty;
					var bAttr = endEl.Attribute("binding")?.Value ?? string.Empty;
					var bLocal = GetLocalName(bAttr);

					if (bindingMap.TryGetValue(bLocal, out var wsdlBinding))
					{
						var addressLocation = endEl.Attribute("address")?.Value ?? string.Empty;

						endpoints.Add(new WsdlEndpoint
						{
							Name = pName,
							Binding = wsdlBinding,
							Address = addressLocation
						});
					}
				}
				service.Endpoints = endpoints;
				services.Add(service);
			}
			wsdl.Services = services;

			// Flatten operations
			wsdl.Operations = wsdl.Interfaces.SelectMany(i => i.Operations).ToList();
		}

		private static string GetLocalName(string qName)
		{
			var idx = qName.IndexOf(':');
			return idx >= 0 ? qName.Substring(idx + 1) : qName;
		}

		private static string ResolveNamespace(XElement element, string qName)
		{
			var idx = qName.IndexOf(':');
			if (idx >= 0)
			{
				var prefix = qName.Substring(0, idx);
				var ns = element.GetNamespaceOfPrefix(prefix);
				if (ns != null) return ns.NamespaceName;
			}
			else
			{
				var defaultNs = element.GetDefaultNamespace();
				if (defaultNs != null) return defaultNs.NamespaceName;
			}
			return string.Empty;
		}
	}
}
