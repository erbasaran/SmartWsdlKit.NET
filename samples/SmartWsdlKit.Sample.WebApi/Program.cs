using Microsoft.OpenApi.Models;
using SmartWsdlKit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new OpenApiInfo { Title = "SmartWsdlKit SOAP API Wrapper", Version = "v1" });
});

// Register SoapClient dynamically loaded from the live WSDL URL
builder.Services.AddSingleton<SoapClient>(sp =>
{
	var options = new SoapClientOptions
	{
		EnableDiagnostics = true,
		EnableResilience = true
	};
	return SoapClient.FromWsdl("http://webservices.oorsprong.org/websamples.countryinfo/CountryInfoService.wso?WSDL", options);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

// 1. Get Capital City of a Country
app.MapGet("/api/country/{isoCode}/capital", async (string isoCode, SoapClient client) =>
{
	var response = await client.Operation("CapitalCity")
		.With("sCountryISOCode", isoCode)
		.ExecuteAsync();

	var result = response.ToDictionary();
	if (result.TryGetValue("CapitalCityResult", out var capital))
	{
		return Results.Ok(new { Country = isoCode.ToUpper(), Capital = capital });
	}
	return Results.NotFound();
});

// 2. Get Flag URL of a Country
app.MapGet("/api/country/{isoCode}/flag", async (string isoCode, SoapClient client) =>
{
	var response = await client.Operation("CountryFlag")
		.With("sCountryISOCode", isoCode)
		.ExecuteAsync();

	var result = response.ToDictionary();
	if (result.TryGetValue("CountryFlagResult", out var flagUrl))
	{
		return Results.Ok(new { Country = isoCode.ToUpper(), FlagUrl = flagUrl });
	}
	return Results.NotFound();
});

// 3. Inspect Last SOAP Call Diagnostics
app.MapGet("/api/country/diagnostics", (SoapClient client) =>
{
	var lastDiag = client.GetLastDiagnostic();
	if (lastDiag == null) return Results.NotFound();

	return Results.Ok(new
	{
		lastDiag.OperationName,
		lastDiag.HttpStatusCode,
		lastDiag.ExecutionTimeMs,
		Request = lastDiag.RequestXml,
		Response = lastDiag.ResponseXml
	});
});

app.Run();
