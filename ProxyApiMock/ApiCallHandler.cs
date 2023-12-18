namespace ProxyApiMock
{
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using Serilog;
    using Serilog.Sinks.Async;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Dynamic;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Reflection.PortableExecutable;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using ProxyApiMock.Interfaces;
    using System.Net.Mime;
    using System.Net.Http.Headers;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;
    using System.Runtime.Serialization;
    using System.Collections.Concurrent;

    public class ApiCallHandler : IMockApi
    {
        private const string FinishedLogSrting = " finished after \n (milliseconds): ";
        private readonly IConfiguration _configuration;
        private string _baseDirectory;
        private Service _service;
        private string _file;
        private IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;
        private readonly IFileReader _fileReader;
        private Dictionary<string, DateTime> _fileTimestamps = new Dictionary<string, DateTime>();

        private ConcurrentDictionary<Guid, Request> _requests;
        private static int _port = 57000;

        public int Port { get; }




        public ApiCallHandler(IHttpClientFactory httpClientFactory, Service service, ILogger logger, IFileReader fileReader, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _service = service;
            _baseDirectory = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _requests = new ConcurrentDictionary<Guid, Request>();
            Port = _port;
            _port++;
            _logger = logger;
            _logger.Information("Starting endpoint at port: {ServicePort}", Port);
            _fileReader = fileReader;
            _configuration = configuration;
        }

        public async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage httpRequestMessage)
        {
            _logger.Information("Finished Receiving {Method} request at {Url}.", httpRequestMessage.Method, httpRequestMessage.RequestUri.AbsoluteUri);

            var mockedRequests = await GetMockedRequests();

            foreach (var mockedRequest in mockedRequests)
            {
                if (await HasAllParams(mockedRequest, httpRequestMessage))
                {
                    var response = new HttpResponseMessage();
                    response.StatusCode = System.Net.HttpStatusCode.OK;

                    _logger.Information("Found a mocked request! Returning mocked response.");
                    var BodyAsString = mockedRequest.Value.Response.Body;
                    var contentType = mockedRequest.Value.Response.Headers.FirstOrDefault(x => x.Key.Equals("Content-type", StringComparison.InvariantCultureIgnoreCase));
                    if (contentType.Value != null)
                    {
                        var a = FindTextInstance(contentType.Value);
                        response.Content = new StringContent(BodyAsString, Encoding.UTF8, a);
                    }
                    else
                    {
                        response.Content = new StringContent(BodyAsString, Encoding.UTF8);
                    }


                    mockedRequest.Value.Response.Headers.ToList().ForEach(x => {
                        if (!x.Key.StartsWith("Content-", StringComparison.InvariantCultureIgnoreCase))
                        {
                            response.Headers.Add(x.Key, x.Value);
                         }});
                    return await Task.FromResult(response);
                }
            }

            _logger.Information("No mocked request found. Calling real API.");
            return await Task.FromResult(await GetRealApiData(httpRequestMessage));
        }

        private async Task<HttpResponseMessage> GetRealApiData(HttpRequestMessage request)
        {
            var requestBodyByte = await request.Content.ReadAsByteArrayAsync();
            var requestBody = Encoding.UTF8.GetString(requestBodyByte);
            _logger.Debug("Received {Method} request at {Url}. Call value: {CallValue}", request.Method, request.RequestUri.AbsoluteUri.ToString(), requestBody is null ? string.Empty : requestBody);
            var requestHeaders = new Dictionary<string, string>();

            try
            {
                _logger.Debug("Sending Real API call to " + request.RequestUri);

                await ApiCallHandlerHelpers.RemoveRestrictedHeaders(request);

                var stopwatch = Stopwatch.StartNew();
                var httpClient = _httpClientFactory.CreateClient("InsecureClient");
                _ = ApiCallHandlerHelpers.RemoveRestrictedHeaders(request);
                HttpResponseMessage realApiResponse = await httpClient.SendAsync(request);

                var bodyString = await realApiResponse.Content.ReadAsStringAsync();

                var contentHeaders = realApiResponse.Content.Headers.ToDictionary(x => x.Key, x => x.Value.FirstOrDefault());

                var mappedRequest = new Request
                {
                    Endpoint = request.RequestUri.AbsoluteUri,
                    Response = new Response
                    {
                        Body = bodyString,
                        Headers = realApiResponse.Headers.ToDictionary(x => x.Key,
                                                                       x => x.Value.FirstOrDefault()),
                        StatusCode = (int)realApiResponse.StatusCode,
                    },
                };
                foreach (var header in contentHeaders)
                {
                    mappedRequest.Response.Headers[header.Key] = header.Value;
                }
                // Add Content-Type header if it doesn't exist, otherwise the response will be returned as empty
                _logger.Debug("Sending Real API call to " + request.RequestUri.AbsoluteUri + FinishedLogSrting + stopwatch.ElapsedMilliseconds);

                try
                {
                    await SaveCallToFile(mappedRequest, _service.Name);
                }
                catch (Exception ex)
                {
                    _logger.Error("Error while saving call to file"+ex);
                    Task.FromException(ex);
                }
                return realApiResponse;
            }
            catch (Exception ex)
            {
                _logger.Error("Error while sending request to real API", ex);

                await Task.FromException(ex);
            }

            return new HttpResponseMessage();
        }

        public async Task<ConcurrentDictionary<Guid, Request>> GetMockedRequests()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            var path = Path.Combine(_baseDirectory, "MockedRequests", $"{_service.Name}.json");

            try
            {

                if (!Path.Exists(path))
                {
                    _logger.Information("Did not found any mocked requests for service {serviceName} in {path}", _service.Name, path);
                }

                if (HasFileChanged(path))
                {
                    var fileData = await _fileReader.ReadAllTextAsync(path);

                    var requestInFile = new List<Request>();
                    try
                    {
                        requestInFile = JsonConvert.DeserializeObject<ApiRequests>(fileData.ToString()).Requests.ToList();
                    }
                    catch (JsonException ex)
                    {
                        _logger.Error("Error while deserializing mocked requests", ex);
                    }

                    foreach (var item in _requests)
                    {
                        _requests.TryRemove(item);
                    }

                    foreach (var request in requestInFile)
                    {
                        _requests.TryAdd(request.Id, request);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error when reading file {ex}", ex.Message);
                await Task.FromException(ex);
            }



            Log.Debug("GetMockedRequests took (milliseconds)" + FinishedLogSrting + stopwatch.ElapsedMilliseconds);
            stopwatch.Stop();
            return _requests;
        }

        private static async Task<bool> HasAllParams(KeyValuePair<Guid, Request> mockedRequest, HttpRequestMessage request)
        {
            var requestContent = await request.Content.ReadAsStringAsync();
            var endpoint = request.RequestUri.AbsolutePath == "/" ? string.Empty : request.RequestUri.AbsolutePath;
            if (mockedRequest.Value.Endpoint != endpoint)
            {
                return false;
            }

            foreach (var parameter in mockedRequest.Value.Params)
            {

                var value = ApiCallHandlerHelpers.FindValueInBody(requestContent, parameter.Key);
                if (value != parameter.Value)
                {
                    return false;
                }
            }

            return true;
        }

        public bool HasFileChanged(string filePath)
        {
            var useFileCache = bool.Parse(_configuration["UseFileCache"]);
            if (!useFileCache)
            {
                return true;
            }
            var lastWriteTime = File.GetLastWriteTimeUtc(filePath);

            if (_fileTimestamps.TryGetValue(filePath, out var previousWriteTime))
            {
                return lastWriteTime != previousWriteTime;
            }

            _fileTimestamps[filePath] = lastWriteTime;
            return true; // File is new or being tracked for the first time
        }

        public static string FindTextInstance(string input)
        {
            var pattern = @"text\/([^;]+)";
            var match = Regex.Match(input, pattern);

            if (match.Success && match.Groups.Count > 1)
            {
                return "text/"+match.Groups[1].Value.Trim();
            }

            return null;
        }


        public async Task SaveCallToFile(Request request, string serviceName)
        {
            // Save the log into a new file with the current timestamp and call value in the name
            var logDirectory = Path.Combine($"{_baseDirectory}/Logs", $"{serviceName}");
            string baseFileName = $"{logDirectory}/{DateTime.UtcNow:yyyy-MM-dd_hh-mm-ss-fff}";
            string fileName;
            try
            {
                Directory.CreateDirectory(logDirectory);
            }
            catch (Exception)
            {
            }
            try
            {
                fileName = $"{baseFileName}.json";
                int count = 1;
                while (File.Exists(fileName))
                {
                    fileName = $"{baseFileName}_{count}.json";
                    count++;
                }
                await File.WriteAllTextAsync(fileName,
                    String.Concat(
                        JsonConvert.SerializeObject(request, Newtonsoft.Json.Formatting.Indented)
                        ));
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to create a log file {ex}", ex);
            }
        }
    }
}