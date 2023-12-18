using System.Xml.Linq;
using Moq;
using Newtonsoft.Json;
using Serilog;
using Moq.Protected;
using System.Net;
using Microsoft.Extensions.Configuration;
using System.Text;
using ProxyApiMock.Interfaces;

namespace ProxyApiMock.Test
{
    public class ApiCallHandlerTest
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Dictionary<string, Service> _testServices;
        private readonly Mock<IConfiguration> _mockConfiguration;

        public ApiCallHandlerTest()
        {
            _mockLogger = new Mock<ILogger>();
            _testServices = GetTestServices();
            _mockConfiguration = new Mock<IConfiguration>();

            // Setup IConfiguration to return specific values for specific keys
            _mockConfiguration.Setup(c => c["UseFileCache"]).Returns("false");

            // You can also setup a more complex configuration structure if needed
            var sectionMock = new Mock<IConfigurationSection>();
            sectionMock.Setup(s => s["SubKey"]).Returns("SubKeyValue");
            _mockConfiguration.Setup(c => c.GetSection("SectionKey")).Returns(sectionMock.Object);
            var a = _mockConfiguration.Object["UseFileCache"];
        }

        private Mock<IHttpClientFactory> GetMockHttpFactory(string body)
        {
            var clientFactory = new Mock<IHttpClientFactory>();

            // Create a mock HttpMessageHandler
            var mockMessageHandler = new Mock<HttpMessageHandler>();
            mockMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(body)
                });

            // Create a mock HttpClient using the mock handler
            var mockHttpClient = new HttpClient(mockMessageHandler.Object);

            // Setup the factory to return the mock HttpClient
            clientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(mockHttpClient);

            return clientFactory;
        }

        [Fact]
        public void Constructor_SetsUpDependenciesCorrectly()
        {
            var mockFileReader = GetFileReaderWithContent("");
            var mockHttpFactory = GetMockHttpFactory("");
            var testhandler = new ApiCallHandler(mockHttpFactory.Object, _testServices["json"], _mockLogger.Object, mockFileReader.Object, _mockConfiguration.Object);
            Assert.NotNull(testhandler);
        }

        [Fact]
        public async Task BuildRequestAsync_ReturnsMockedJsonResponse_IfMockExists()
        {
            var mockedRequestJson = JsonConvert.DeserializeObject<Request>(File.ReadAllText("Data/TestRequestMocked.json"));
            var mockRequestMessage = CreateRequestMessageWithBody("application/json", mockedRequestJson.Response.Body, ApiCallHandlerHelpers.GetUri(_testServices["json"].Url, mockedRequestJson.Endpoint), mockedRequestJson.Response.Headers);
            var mockFileReader = GetFileReaderWithContent(
                JsonConvert.SerializeObject(
                    GetTestRequests("MockServiceJSON")));
            var mockHttpFactory = GetMockHttpFactory(File.ReadAllText("Data/TestResponse.json"));
            var testhandler = new ApiCallHandler(mockHttpFactory.Object, _testServices["json"], _mockLogger.Object, mockFileReader.Object, _mockConfiguration.Object);

            var response = await testhandler.SendRequestAsync(mockRequestMessage);
            var retBody = "{\r\n  \"employees\": [\r\n    {\r\n      \"id\": \"E004\",\r\n      \"name\": \"John Doe\",\r\n      \"department\": \"Finance\",\r\n      \"email\": \"johndoe@example.com\"\r\n    },\r\n    {\r\n      \"id\": \"E002\",\r\n      \"name\": \"Jane Smith\",\r\n      \"department\": \"Marketing\",\r\n      \"email\": \"janesmith@example.com\"\r\n    }\r\n  ],\r\n  \"company\": {\r\n    \"name\": \"Tech Solutions\",\r\n    \"location\": \"New York\"\r\n  }\r\n}\r\n";
            Assert.Equal(retBody, await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task BuildRequestAsync_ReturnsMockedXMLResponse_IfMockExists()
        {
            string xmlBodyRequest = File.ReadAllText("Data/TestRequestMocked.xml");
            var xmlObj = XDocument.Parse(xmlBodyRequest);
            var endpoint = xmlObj.Descendants("Endpoint").FirstOrDefault()?.Value;
            Dictionary<string, string> headers = xmlObj.Descendants("Headers")
                .Elements()
                .ToDictionary(x => x.Name.LocalName, x => x.Value);

            var mockRequestMessage = CreateRequestMessageWithBody("application/xml", xmlBodyRequest, ApiCallHandlerHelpers.GetUri(_testServices["xml"].Url, endpoint), headers);
            var mockFileReader = GetFileReaderWithContent(
                JsonConvert.SerializeObject(
                    GetTestRequests("MockServiceXML")));
            var mockHttpFactory = GetMockHttpFactory(File.ReadAllText("Data/TestResponse.xml"));
            var testhandler = new ApiCallHandler(mockHttpFactory.Object, _testServices["json"], _mockLogger.Object, mockFileReader.Object, _mockConfiguration.Object);

            var response = await testhandler.SendRequestAsync(mockRequestMessage);
            var retBody = "<Employees>\r\n\t<Employee>\r\n\t\t<Id>E004</Id>\r\n\t\t<Name>Carmen Doe</Name>\r\n\t\t<Department>Finance</Department>\r\n\t\t<Email>johndoe@example.com</Email>\r\n\t</Employee>\r\n\t<Employee>\r\n\t\t<Id>E002</Id>\r\n\t\t<Name>Jane Smith</Name>\r\n\t\t<Department>Marketing</Department>\r\n\t\t<Email>janesmith@example.com</Email>\r\n\t</Employee>\r\n\t<Company>\r\n\t\t<Name>Tech Solutions</Name>\r\n\t\t<Location>New York</Location>\r\n\t</Company>\r\n</Employees>";
            Assert.Equal(retBody, await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task BuildRequestAsync_ProcessesJsonCorrectly_WithoutMock()
        {
            var jsonBody = File.ReadAllText("Data/TestResponse.json");
            var mockHttpFactory = GetMockHttpFactory(jsonBody);

            jsonBody = File.ReadAllText("Data/TestRequest.json");
            var jsonObject = JsonConvert.DeserializeObject<Request>(jsonBody);
            var mockRequestMessage = CreateRequestMessageWithBody("application/json", jsonBody, ApiCallHandlerHelpers.GetUri(_testServices["json"].Url, jsonObject.Endpoint), jsonObject.Response.Headers);

            var mockFileReader = GetFileReaderWithContent(JsonConvert.SerializeObject(GetTestRequests("MockServiceJSON")));

            var handler = new ApiCallHandler(mockHttpFactory.Object, _testServices["json"], _mockLogger.Object, mockFileReader.Object, _mockConfiguration.Object);
            var response = await handler.SendRequestAsync(mockRequestMessage);

            // Assert that response is processed correctly based on JSON input
            // Replace with your actual expected response details
            Assert.NotNull(response);
            Assert.Equal(File.ReadAllText("Data/TestResponse.json"), await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task BuildRequestAsync_ProcessesXmlCorrectly_WithoutMock()
        {
            var xmlBodyResponse = File.ReadAllText("Data/TestResponse.xml");
            var mockHttpFactory = GetMockHttpFactory(xmlBodyResponse);

            string xmlBodyRequest = File.ReadAllText("Data/TestRequest.xml");
            var xmlObj = XDocument.Parse(xmlBodyRequest);
            var endpoint = xmlObj.Descendants("Endpoint").FirstOrDefault()?.Value;
            Dictionary<string, string> headers = xmlObj.Descendants("Headers")
                .Elements()
                .ToDictionary(x => x.Name.LocalName, x => x.Value);

            var mockRequestMessage = CreateRequestMessageWithBody("application/xml", xmlBodyRequest, ApiCallHandlerHelpers.GetUri(_testServices["xml"].Url, endpoint), headers);
            var mockFileReader = GetFileReaderWithContent(JsonConvert.SerializeObject(GetTestRequests("MockServiceXML")));

            var handler = new ApiCallHandler(mockHttpFactory.Object, _testServices["xml"], _mockLogger.Object, mockFileReader.Object, _mockConfiguration.Object);
            var response = await handler.SendRequestAsync(mockRequestMessage);

            Assert.NotNull(response);
            Assert.Equal(xmlBodyResponse, await response.Content.ReadAsStringAsync());
        }


        private Mock<IFileReader> GetFileReaderWithContent(string text)
        {
            var mockFileReader = new Mock<IFileReader>();
            mockFileReader.Setup(m => m.ReadAllTextAsync(It.IsAny<string>()))
                .ReturnsAsync(text);
            return mockFileReader;
        }

        private HttpRequestMessage CreateRequestMessageWithBody(string contentType, string body, Uri url, Dictionary<string, string> headers)
        {
            var requestMessage = new HttpRequestMessage
            {
                RequestUri = url,
                Content = new StringContent(body, Encoding.UTF8, contentType)
            };

            foreach (var header in headers)
            {
                requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return requestMessage;
        }


        private Dictionary<string, Service> GetTestServices()
        {
            var servicesDict = new Dictionary<string, Service>();
            var services = new
            {
                Services = new[]
                {
                    new { Name = "MockServiceJSON",  Url = @"http:\\json.testapi.com\" },
                    new { Name = "MockServiceXML", Url = @"http:\\xml.testapi.com\" }
                }
            };

            foreach (var service in services.Services)
            {
                var serialized = JsonConvert.SerializeObject(service, Formatting.Indented);
                if (service.Name == "MockServiceXML")
                {
                    servicesDict["xml"] = JsonConvert.DeserializeObject<Service>(serialized);
                    continue;
                }

                servicesDict["json"] = JsonConvert.DeserializeObject<Service>(serialized);
            }

            return servicesDict;
        }

        private List<Request> GetTestRequests(string serviceName)
        {
            var requests = new List<Request>();

            var files = Directory.EnumerateFiles("Data", serviceName + ".json");

            foreach (var file in files)
            {
                string fileExtension = Path.GetExtension(file).ToLower();

                if (fileExtension == ".json")
                {
                    string jsonContent = File.ReadAllText(file);
                    var request = JsonConvert.DeserializeObject<ApiRequests>(jsonContent);
                    if (request != null)
                    {
                        requests.AddRange(request.Requests);
                    }
                }
            }

            return requests;
        }
    }
}