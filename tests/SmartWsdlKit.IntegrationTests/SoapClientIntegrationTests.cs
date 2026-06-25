using System.Net;
using System.Text;
using Xunit;

namespace SmartWsdlKit.IntegrationTests
{
	public class SoapClientIntegrationTests
	{
		private class MockHttpMessageHandler : HttpMessageHandler
		{
			private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

			public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
			{
				_handler = handler;
			}

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
			{
				return _handler(request);
			}
		}

		[Fact]
		public async Task ExecuteAsync_SuccessResponse_DeserializesCorrectly()
		{
			// Arrange
			var responseXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <AddResponse xmlns=""http://tempuri.org/"">
      <AddResult>15</AddResult>
    </AddResponse>
  </soap:Body>
</soap:Envelope>";

			var mockHandler = new MockHttpMessageHandler(async req =>
			{
				// Verify request content
				var reqContent = await req.Content!.ReadAsStringAsync();
				Assert.Contains("<tns:intA>5</tns:intA>", reqContent);
				Assert.Contains("<tns:intB>10</tns:intB>", reqContent);

				return new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(responseXml, Encoding.UTF8, "text/xml")
				};
			});

			var options = new SoapClientOptions
			{
				BaseAddress = new Uri("http://mock-service.local/calc"),
				BackchannelHandler = mockHandler
			};

			using var client = new SoapClient(options);

			// Act
			var response = await client.Operation("Add")
				.With("intA", 5)
				.With("intB", 10)
				.ExecuteAsync();

			// Assert
			Assert.NotNull(response);
			var dict = response.ToDictionary();
			Assert.Equal("15", dict["AddResult"]);
		}

		[Fact]
		public async Task ExecuteAsync_SoapFaultHttp500_ThrowsSoapFaultException()
		{
			// Arrange
			var faultXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <soap:Fault>
      <faultcode>soap:Client</faultcode>
      <faultstring>Invalid ID provided.</faultstring>
      <detail>
        <ErrorCode>ERROR_CODE_99</ErrorCode>
      </detail>
    </soap:Fault>
  </soap:Body>
</soap:Envelope>";

			var mockHandler = new MockHttpMessageHandler(async req =>
			{
				return new HttpResponseMessage(HttpStatusCode.InternalServerError) // HTTP 500
				{
					Content = new StringContent(faultXml, Encoding.UTF8, "text/xml")
				};
			});

			var options = new SoapClientOptions
			{
				BaseAddress = new Uri("http://mock-service.local/customers"),
				BackchannelHandler = mockHandler
			};

			using var client = new SoapClient(options);

			// Act & Assert
			var ex = await Assert.ThrowsAsync<SoapFaultException>(async () =>
			{
				await client.Operation("GetCustomer")
					.With("CustomerId", 999)
					.ExecuteAsync();
			});

			Assert.Equal("soap:Client", ex.FaultCode);
			Assert.Equal("Invalid ID provided.", ex.FaultString);
			Assert.Contains("ERROR_CODE_99", ex.DetailXml);
		}

		[Fact]
		public async Task ExecuteAsync_WrappedArraySerialization_MapsRequestAndResponse()
		{
			// Arrange
			var responseXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <GetItemsResponse xmlns=""http://tempuri.org/"">
      <Items>
        <Item>
          <Id>1</Id>
          <Name>ItemOne</Name>
        </Item>
        <Item>
          <Id>2</Id>
          <Name>ItemTwo</Name>
        </Item>
      </Items>
    </GetItemsResponse>
  </soap:Body>
</soap:Envelope>";

			var mockHandler = new MockHttpMessageHandler(async req =>
			{
				var reqContent = await req.Content!.ReadAsStringAsync();
				Assert.Contains("<tns:Ids>101</tns:Ids>", reqContent);
				Assert.Contains("<tns:Ids>102</tns:Ids>", reqContent);

				return new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(responseXml, Encoding.UTF8, "text/xml")
				};
			});

			var options = new SoapClientOptions
			{
				BaseAddress = new Uri("http://mock-service.local/items"),
				BackchannelHandler = mockHandler
			};

			using var client = new SoapClient(options);

			// Act
			var response = await client.Operation("GetItems")
				.With("Ids", new List<int> { 101, 102 })
				.ExecuteAsync();

			// Assert
			Assert.NotNull(response);
			var dict = response.ToDictionary();

			var itemsList = dict["Items"] as List<object?>;
			Assert.NotNull(itemsList);
			Assert.Equal(2, itemsList.Count);

			var firstItem = itemsList[0] as Dictionary<string, object?>;
			Assert.NotNull(firstItem);
			Assert.Equal("1", firstItem["Id"]);
			Assert.Equal("ItemOne", firstItem["Name"]);
		}

		[Fact]
		public async Task ExecuteAsync_WithSoapAttachments_SendsMultipartRelatedRequest()
		{
			// Arrange
			var responseXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <UploadFileResponse xmlns=""http://tempuri.org/"">
      <Status>Success</Status>
    </UploadFileResponse>
  </soap:Body>
</soap:Envelope>";

			var mockHandler = new MockHttpMessageHandler(async req =>
			{
				// Verify it is a multipart request
				Assert.NotNull(req.Content);
				Assert.Contains("multipart/related", req.Content.Headers.ContentType!.MediaType);

				// Read multipart content
				var contentString = await req.Content.ReadAsStringAsync();
				Assert.Contains("Content-ID: <my-attachment-1>", contentString);
				Assert.Contains("Content-Type: text/plain", contentString);
				Assert.Contains("Hello WSDL Attachments!", contentString);

				return new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(responseXml, Encoding.UTF8, "text/xml")
				};
			});

			var options = new SoapClientOptions
			{
				BaseAddress = new Uri("http://mock-service.local/upload"),
				BackchannelHandler = mockHandler
			};

			using var client = new SoapClient(options);

			// Act
			var response = await client.Operation("UploadFile")
				.WithAttachment("my-attachment-1", Encoding.UTF8.GetBytes("Hello WSDL Attachments!"), "text/plain", "test.txt")
				.ExecuteAsync();

			// Assert
			Assert.NotNull(response);
			var dict = response.ToDictionary();
			Assert.Equal("Success", dict["Status"]);
		}

		[Fact]
		public async Task ExecuteAsync_Non200NotValidXml_ThrowsSoapExceptionWithRawText()
		{
			// Arrange
			var rawHtmlError = "<html><body><h1>404 Not Found</h1></body></html>";

			var mockHandler = new MockHttpMessageHandler(async req =>
			{
				return new HttpResponseMessage(HttpStatusCode.NotFound) // HTTP 404
				{
					Content = new StringContent(rawHtmlError, Encoding.UTF8, "text/html")
				};
			});

			var options = new SoapClientOptions
			{
				BaseAddress = new Uri("http://mock-service.local/notfound"),
				BackchannelHandler = mockHandler
			};

			using var client = new SoapClient(options);

			// Act & Assert
			var ex = await Assert.ThrowsAsync<SoapException>(async () =>
			{
				await client.Operation("GetInfo").ExecuteAsync();
			});

			Assert.Contains("404", ex.Message);
			Assert.Contains(rawHtmlError, ex.Message);
		}

		[Fact]
		public async Task ExecuteAsync_WithMtomAttachmentParameter_SerializesAsXopInclude()
		{
			// Arrange
			var responseXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <UploadFileResponse xmlns=""http://tempuri.org/"">
      <Status>Success</Status>
    </UploadFileResponse>
  </soap:Body>
</soap:Envelope>";

			var mockHandler = new MockHttpMessageHandler(async req =>
			{
				Assert.NotNull(req.Content);
				var contentString = await req.Content.ReadAsStringAsync();

				// Verify MTOM-specific content-type parameters on request and parts
				Assert.Contains("multipart/related", req.Content.Headers.ContentType!.MediaType);
				Assert.Contains("type=\"application/xop+xml\"", req.Content.Headers.ContentType.ToString());
				Assert.Contains("start=\"<rootpart@smartwsdlkit.org>\"", req.Content.Headers.ContentType.ToString());

				// Verify XOP Include element is serialized in the XML body
				Assert.Contains("<xop:Include", contentString);
				Assert.Contains("href=\"cid:my-file-id\"", contentString);
				Assert.Contains("xmlns:xop=\"http://www.w3.org/2004/08/xop/include\"", contentString);

				// Verify binary part exists
				Assert.Contains("Content-ID: <my-file-id>", contentString);
				Assert.Contains("Hello MTOM XML!", contentString);

				return new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(responseXml, Encoding.UTF8, "text/xml")
				};
			});

			var options = new SoapClientOptions
			{
				BaseAddress = new Uri("http://mock-service.local/mtom"),
				AttachmentMode = SoapAttachmentMode.Mtom,
				BackchannelHandler = mockHandler
			};

			using var client = new SoapClient(options);
			var fileAttachment = new SoapAttachment("my-file-id", Encoding.UTF8.GetBytes("Hello MTOM XML!"), "text/plain");

			// Act
			var response = await client.Operation("UploadFile")
				.With("FileData", fileAttachment)
				.ExecuteAsync();

			// Assert
			Assert.NotNull(response);
			Assert.Equal("Success", response.ToDictionary()["Status"]);
		}

		[Fact]
		public async Task ExecuteAsync_WithDiagnosticsEnabled_LogsFullTrafficDetails()
		{
			// Arrange
			var responseXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <PingResponse xmlns=""http://tempuri.org/"">
      <Message>Pong</Message>
    </PingResponse>
  </soap:Body>
</soap:Envelope>";

			var mockHandler = new MockHttpMessageHandler(async req =>
			{
				return new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(responseXml, Encoding.UTF8, "text/xml")
				};
			});

			var options = new SoapClientOptions
			{
				BaseAddress = new Uri("http://mock-service.local/ping"),
				BackchannelHandler = mockHandler
			};

			using var client = new SoapClient(options).EnableDiagnostics();

			// Act
			var response = await client.Operation("Ping")
				.WithHttpHeader("X-Custom-Req", "ReqVal")
				.ExecuteAsync();

			// Assert
			var log = client.GetLastDiagnostic();
			Assert.NotNull(log);
			Assert.True(log.IsSuccess);
			Assert.Equal("Ping", log.OperationName);
			Assert.Equal(200, log.HttpStatusCode);
			Assert.Contains("<tns:Ping", log.RequestXml);
			Assert.Contains("Pong", log.ResponseXml);
			Assert.True(log.ExecutionTimeMs > 0);
			Assert.True(log.RequestHeaders.ContainsKey("X-Custom-Req"));
			Assert.True(log.ResponseHeaders.ContainsKey("Content-Type"));
		}

		[Fact]
		public async Task ExecuteAsync_WithResilienceEnabled_FailsFastWhenCircuitOpens()
		{
			// Arrange
			int callCount = 0;
			Func<HttpRequestMessage, Task<HttpResponseMessage>> responder = req =>
			{
				callCount++;
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)); // Failure
			};

			var mockHandler = new MockHttpMessageHandler(async req => await responder(req));

			var options = new SoapClientOptions
			{
				BaseAddress = new Uri("http://mock-service.local/resilience"),
				BackchannelHandler = mockHandler,
				EnableResilience = true,
				RetryCount = 1, // 1 retry = 2 total attempts per execute
				RetryDelay = TimeSpan.FromMilliseconds(5),
				CircuitBreakerFailureThreshold = 2, // open circuit after 2 failures
				CircuitBreakerResetTimeout = TimeSpan.FromMilliseconds(200)
			};

			using var client = new SoapClient(options);

			// Act & Assert - Execute 1: will fail. Total attempts should be 2 (original + 1 retry).
			await Assert.ThrowsAnyAsync<Exception>(async () =>
			{
				await client.Operation("TestResilience").ExecuteAsync();
			});

			Assert.Equal(2, callCount); // Verified retry count worked

			// Now failure count is 2, which matches threshold. Circuit should be OPEN.
			// Execute 2: Should immediately throw CircuitBreakerOpenException without calling backend.
			var cbEx = await Assert.ThrowsAsync<CircuitBreakerOpenException>(async () =>
			{
				await client.Operation("TestResilience").ExecuteAsync();
			});
			Assert.Contains("OPEN", cbEx.Message);
			Assert.Equal(2, callCount); // Backend not called because circuit is open!

			// Wait reset timeout (200ms) for state to transition to Half-Open
			await Task.Delay(250);

			// Now, a call should go through (Half-Open). Let's make it succeed this time.
			var responseXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <TestResponse xmlns=""http://tempuri.org/""><Status>OK</Status></TestResponse>
  </soap:Body>
</soap:Envelope>";

			responder = req =>
			{
				callCount++;
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(responseXml, Encoding.UTF8, "text/xml")
				});
			};

			var successResp = await client.Operation("TestResilience").ExecuteAsync();
			Assert.Equal("OK", successResp.ToDictionary()["Status"]);
			Assert.Equal(3, callCount); // Backend was called once and succeeded. Circuit is now CLOSED.
		}

		public class CalcResponse
		{
			public int AddResult { get; set; }
		}

		[Fact]
		public async Task ExecuteAsync_WithGenericAndTypeCasting_DeserializesSuccessfully()
		{
			// Arrange
			var responseXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <AddResponse xmlns=""http://tempuri.org/"">
      <AddResult>15</AddResult>
    </AddResponse>
  </soap:Body>
</soap:Envelope>";

			var mockHandler = new MockHttpMessageHandler(async req =>
			{
				return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(responseXml, Encoding.UTF8, "text/xml")
				});
			});

			var options = new SoapClientOptions
			{
				BaseAddress = new Uri("http://mock-service.local/calc"),
				BackchannelHandler = mockHandler
			};

			using var client = new SoapClient(options);

			// Act 1: Generic ExecuteAsync<T>
			var resultGeneric = await client.Operation("Add")
				.With("intA", 5)
				.With("intB", 10)
				.ExecuteAsync<CalcResponse>();

			// Act 2: Non-generic ExecuteAsync(Type)
			var resultType = await client.Operation("Add")
				.With("intA", 5)
				.With("intB", 10)
				.ExecuteAsync(typeof(CalcResponse));

			// Assert
			Assert.NotNull(resultGeneric);
			Assert.Equal(15, resultGeneric.AddResult);

			Assert.NotNull(resultType);
			Assert.IsType<CalcResponse>(resultType);
			Assert.Equal(15, ((CalcResponse)resultType).AddResult);
		}
	}
}
