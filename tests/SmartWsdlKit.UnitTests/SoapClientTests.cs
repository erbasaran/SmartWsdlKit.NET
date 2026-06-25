using System.Xml.Linq;
using Xunit;

namespace SmartWsdlKit.UnitTests
{
	public class SoapClientTests
	{
		[Fact]
		public void BuildSoapEnvelope_Soap11WithWsSecurityPlaintext_GeneratesCorrectXml()
		{
			// Arrange
			var options = new SoapClientOptions
			{
				BaseAddress = new Uri("http://localhost/test")
			};
			using var client = new SoapClient(options);
			client.WithWsSecurity("myUser", "myPass", WsSecurityPasswordType.Plaintext);

			var parameters = new Dictionary<string, object?>
			{
				["CustomerId"] = 123
			};
			var customHeaders = new List<XElement>();

			// Act
			var xml = client.BuildSoapEnvelope("GetCustomer", "http://tempuri.org/", SoapVersion.Soap11, parameters, customHeaders);

			// Assert
			Assert.Contains("http://schemas.xmlsoap.org/soap/envelope/", xml);
			Assert.Contains("<wsse:Security", xml);
			Assert.Contains("<wsse:Username>myUser</wsse:Username>", xml);
			Assert.Contains("Type=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText\"", xml);
			Assert.Contains("myPass", xml);
			Assert.Contains("<tns:CustomerId>123</tns:CustomerId>", xml);
		}

		[Fact]
		public void BuildSoapEnvelope_Soap12WithWsSecurityDigest_GeneratesCorrectXml()
		{
			// Arrange
			var options = new SoapClientOptions();
			using var client = new SoapClient(options);
			client.WithWsSecurity("digestUser", "secret", WsSecurityPasswordType.PasswordDigest);

			var parameters = new Dictionary<string, object?>
			{
				["Name"] = "Alice"
			};
			var customHeaders = new List<XElement>();

			// Act
			var xml = client.BuildSoapEnvelope("CreateUser", "http://tempuri.org/", SoapVersion.Soap12, parameters, customHeaders);

			// Assert
			Assert.Contains("http://www.w3.org/2003/05/soap-envelope", xml);
			Assert.Contains("<wsse:Security", xml);
			Assert.Contains("Type=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest\"", xml);
			Assert.Contains("<wsse:Nonce", xml);
			Assert.Contains("<wsu:Created", xml);
			Assert.Contains("<tns:Name>Alice</tns:Name>", xml);
		}

		[Fact]
		public void BuildSoapEnvelope_CultureInvariantFormatting_SerializesCorrectly()
		{
			// Arrange
			var options = new SoapClientOptions();
			using var client = new SoapClient(options);

			// Set culture to Turkish (which uses comma for decimal separator and has dotting issues)
			var originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
			System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");

			try
			{
				var parameters = new Dictionary<string, object?>
				{
					["Price"] = 12.34m,
					["Date"] = new DateTime(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc)
				};

				// Act
				var xml = client.BuildSoapEnvelope("SaveProduct", "http://tempuri.org/", SoapVersion.Soap11, parameters, new List<XElement>());

				// Assert
				Assert.Contains("<tns:Price>12.34</tns:Price>", xml); // must use dot, not comma
				Assert.Contains("2026-06-25T12:00:00", xml); // ISO format
			}
			finally
			{
				System.Threading.Thread.CurrentThread.CurrentCulture = originalCulture;
			}
		}

		[Fact]
		public void BuildSoapEnvelope_WithCustomSoapHeaders_GeneratesCorrectXml()
		{
			// Arrange
			var options = new SoapClientOptions();
			using var client = new SoapClient(options);

			var parameters = new Dictionary<string, object?>();
			var customHeaders = new List<XElement>
			{
				new XElement(XNamespace.Get("http://mycustomns.org/") + "AuthHeader", "SecretTokenValue"),
				new XElement(XNamespace.Get("http://mycustomns.org/") + "SessionId", 9999)
			};

			// Act
			var xml = client.BuildSoapEnvelope("Ping", "http://tempuri.org/", SoapVersion.Soap11, parameters, customHeaders);

			// Assert
			Assert.Contains("http://mycustomns.org/", xml);
			Assert.Contains("<AuthHeader xmlns=\"http://mycustomns.org/\">SecretTokenValue</AuthHeader>", xml);
			Assert.Contains("<SessionId xmlns=\"http://mycustomns.org/\">9999</SessionId>", xml);
		}
	}
}
