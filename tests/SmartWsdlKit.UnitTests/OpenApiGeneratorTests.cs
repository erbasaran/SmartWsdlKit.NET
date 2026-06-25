using System.Text.Json;
using Xunit;

namespace SmartWsdlKit.UnitTests
{
	public class OpenApiGeneratorTests
	{
		[Fact]
		public async Task GenerateOpenApi_ValidWsdl_ProducesCorrectJson()
		{
			// Arrange
			var tempFile = Path.GetTempFileName() + ".wsdl";
			await File.WriteAllTextAsync(tempFile, WsdlParserTests.CalculatorWsdl);

			try
			{
				var wsdl = await WsdlDocument.LoadAsync(tempFile);

				// Act
				var openApiJson = OpenApiGenerator.Generate(wsdl);

				// Assert
				Assert.NotNull(openApiJson);

				// Parse to check if it's valid JSON
				using var jsonDoc = JsonDocument.Parse(openApiJson);
				var root = jsonDoc.RootElement;

				Assert.Equal("3.0.3", root.GetProperty("openapi").GetString());

				var info = root.GetProperty("info");
				Assert.Equal("Calculator", info.GetProperty("title").GetString());

				var paths = root.GetProperty("paths");
				Assert.True(paths.TryGetProperty("/operations/Add", out _));

				var components = root.GetProperty("components");
				var schemas = components.GetProperty("schemas");
				Assert.True(schemas.TryGetProperty("Add", out _));
				Assert.True(schemas.TryGetProperty("AddResponse", out _));
			}
			finally
			{
				if (File.Exists(tempFile)) File.Delete(tempFile);
			}
		}
	}
}
