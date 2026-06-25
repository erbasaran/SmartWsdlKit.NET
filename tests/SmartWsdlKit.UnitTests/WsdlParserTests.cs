using Xunit;

namespace SmartWsdlKit.UnitTests
{
	public class WsdlParserTests
	{
		public static readonly string CalculatorWsdl = @"<?xml version=""1.0"" encoding=""utf-8""?>
<wsdl:definitions xmlns:soap=""http://schemas.xmlsoap.org/wsdl/soap/"" xmlns:tns=""http://tempuri.org/"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:wsdl=""http://schemas.xmlsoap.org/wsdl/"" targetNamespace=""http://tempuri.org/"">
  <wsdl:types>
    <xsd:schema targetNamespace=""http://tempuri.org/"" elementFormDefault=""qualified"">
      <xsd:element name=""Add"">
        <xsd:complexType>
          <xsd:sequence>
            <xsd:element name=""intA"" type=""xsd:int"" />
            <xsd:element name=""intB"" type=""xsd:int"" />
          </xsd:sequence>
        </xsd:complexType>
      </xsd:element>
      <xsd:element name=""AddResponse"">
        <xsd:complexType>
          <xsd:sequence>
            <xsd:element name=""AddResult"" type=""xsd:int"" />
          </xsd:sequence>
        </xsd:complexType>
      </xsd:element>
    </xsd:schema>
  </wsdl:types>
  <wsdl:message name=""AddSoapIn"">
    <wsdl:part name=""parameters"" element=""tns:Add"" />
  </wsdl:message>
  <wsdl:message name=""AddSoapOut"">
    <wsdl:part name=""parameters"" element=""tns:AddResponse"" />
  </wsdl:message>
  <wsdl:portType name=""CalculatorSoap"">
    <wsdl:operation name=""Add"">
      <wsdl:input message=""tns:AddSoapIn"" />
      <wsdl:output message=""tns:AddSoapOut"" />
    </wsdl:operation>
  </wsdl:portType>
  <wsdl:binding name=""CalculatorSoap"" type=""tns:CalculatorSoap"">
    <soap:binding transport=""http://schemas.xmlsoap.org/soap/http"" />
    <wsdl:operation name=""Add"">
      <soap:operation soapAction=""http://tempuri.org/Add"" style=""document"" />
      <wsdl:input>
        <soap:body use=""literal"" />
      </wsdl:input>
      <wsdl:output>
        <soap:body use=""literal"" />
      </wsdl:output>
    </wsdl:operation>
  </wsdl:binding>
  <wsdl:service name=""Calculator"">
    <wsdl:port name=""CalculatorSoap"" binding=""tns:CalculatorSoap"">
      <soap:address location=""http://www.dneonline.com/calculator.asmx"" />
    </wsdl:port>
  </wsdl:service>
</wsdl:definitions>";

		[Fact]
		public async Task Parse_ValidWsdl_Succeeds()
		{
			// Arrange
			var tempFile = Path.GetTempFileName() + ".wsdl";
			await File.WriteAllTextAsync(tempFile, CalculatorWsdl);

			try
			{
				// Act
				var wsdl = await WsdlDocument.LoadAsync(tempFile);

				// Assert
				Assert.NotNull(wsdl);
				Assert.Equal("http://tempuri.org/", wsdl.TargetNamespace);
				Assert.Single(wsdl.Services);
				Assert.Equal("Calculator", wsdl.Services[0].Name);
				Assert.Single(wsdl.Services[0].Endpoints);
				Assert.Equal("http://www.dneonline.com/calculator.asmx", wsdl.Services[0].Endpoints[0].Address);

				Assert.Single(wsdl.Interfaces);
				Assert.Equal("CalculatorSoap", wsdl.Interfaces[0].Name);

				Assert.Single(wsdl.Operations);
				var op = wsdl.Operations[0];
				Assert.Equal("Add", op.Name);
				Assert.Equal("http://tempuri.org/Add", op.SoapAction);
				Assert.Equal("Add", op.InputElementName);
				Assert.Equal("AddResponse", op.OutputElementName);
			}
			finally
			{
				if (File.Exists(tempFile)) File.Delete(tempFile);
			}
		}

		[Fact]
		public async Task Explorer_ValidWsdl_ReturnsServiceInfo()
		{
			var tempFile = Path.GetTempFileName() + ".wsdl";
			await File.WriteAllTextAsync(tempFile, CalculatorWsdl);

			try
			{
				var info = await WsdlExplorer.ExploreAsync(tempFile);

				Assert.NotNull(info);
				Assert.Equal("Calculator", info.ServiceName);
				Assert.Contains("http://www.dneonline.com/calculator.asmx", info.EndpointUrls);
				Assert.Contains("Soap11", info.SoapVersions);
				Assert.Contains("Add", info.SupportedOperations);
			}
			finally
			{
				if (File.Exists(tempFile)) File.Delete(tempFile);
			}
		}

		[Fact]
		public async Task Analyzer_ValidWsdl_ReturnsCorrectReport()
		{
			var tempFile = Path.GetTempFileName() + ".wsdl";
			await File.WriteAllTextAsync(tempFile, CalculatorWsdl);

			try
			{
				var wsdl = await WsdlDocument.LoadAsync(tempFile);
				var report = WsdlAnalyzer.Analyze(wsdl);

				Assert.NotNull(report);
				Assert.Contains("Calculator", report.Services);
				Assert.Contains("Add", report.Operations);
				Assert.True(report.ComplexityScore > 0);
				Assert.NotEmpty(report.Warnings); // HTTP endpoint warning
			}
			finally
			{
				if (File.Exists(tempFile)) File.Delete(tempFile);
			}
		}
	}
}
