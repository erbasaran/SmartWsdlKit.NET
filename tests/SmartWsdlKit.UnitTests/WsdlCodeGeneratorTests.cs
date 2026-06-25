using Xunit;

namespace SmartWsdlKit.UnitTests
{
	public class WsdlCodeGeneratorTests
	{
		[Fact]
		public async Task GenerateCode_ValidWsdl_ReturnsExpectedClasses()
		{
			// Arrange
			var tempFile = Path.GetTempFileName() + ".wsdl";
			await File.WriteAllTextAsync(tempFile, WsdlParserTests.CalculatorWsdl);

			try
			{
				var wsdl = await WsdlDocument.LoadAsync(tempFile);

				// Act
				var code = WsdlCodeGenerator.Generate(wsdl, new CodeGeneratorOptions
				{
					Namespace = "MyCalculatorService",
					GenerateRecords = true
				});

				// Assert
				Assert.NotNull(code);
				Assert.Contains("namespace MyCalculatorService", code);
				Assert.Contains("public record Add", code);
				Assert.Contains("public record AddResponse", code);
				Assert.Contains("public interface ICalculatorSoap", code);
				Assert.Contains("public class CalculatorSoapClient", code);
				Assert.Contains("public int IntA { get; set; }", code);
				Assert.Contains("public int AddResult { get; set; }", code);
			}
			finally
			{
				if (File.Exists(tempFile)) File.Delete(tempFile);
			}
		}
	}
}
