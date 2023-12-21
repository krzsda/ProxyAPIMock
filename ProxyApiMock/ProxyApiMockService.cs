namespace ProxyApiMock
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Hosting; // This is needed for web-related extensions
    using Microsoft.Extensions.Hosting;
    using Serilog;
    using ProxyApiMock.Interfaces;
    using Microsoft.AspNetCore.Builder;
    using System.IO;
    using Newtonsoft.Json;
    using System.Net.Mime;
    using System.Net.Sockets;

    public class ProxyApiMockService : BackgroundService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IFileReader _fileReader;
        private List<IHost> _hosts = new List<IHost>();
        private string _directory;
        private readonly int _handlerRetryLimit;
        private static int _port = 57000;

        public ProxyApiMockService(IHttpClientFactory httpClientFactory, IFileReader fileReader, IConfiguration configuration)
        {
            Log.Information("Setting the service up.");
            _directory = Path.GetDirectoryName(AppContext.BaseDirectory);
            _httpClientFactory = httpClientFactory;
            _fileReader = fileReader;
            _configuration = configuration;
            Log.Information("Finnished setting the service up.");
            _handlerRetryLimit = configuration.GetValue<int>("HandlerRetryLimit");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log.Information("Starting search for services in appsettings.json");
            var apiServices = _configuration.GetSection("Services").Get<List<Service>>();

            Log.Information("Found {count}", apiServices.Count);

            foreach (var service in apiServices)
            {
                Log.Information("Adding service {service}", service.Name);

                var logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .WriteTo.File(Path.Combine(_directory, "Logs", service.Name, "ProxyApiHandler.log"))
                    .CreateLogger();
                var handler = new ApiCallHandler(_httpClientFactory, service, logger, _fileReader, _configuration, _port);

                bool hostStarted = false;
                int retryCount = 0;

                while (!hostStarted && retryCount < _handlerRetryLimit)
                {
                    try
                    {
                        var host = CreateHandlerHost(service, handler);
                        _hosts.Add(host);
                        await host.StartAsync(stoppingToken);
                        Log.Information("Started host for Config: {Name} proxying to {Url} on port: {Port} - logging into {directory}", service.Name, service.Url, handler.Port, _directory);
                        hostStarted = true;
                    }
                    catch (Exception ex) when (ex is IOException || ex is SocketException)
                    {
                        Log.Warning("Port {Port} is in use. Retrying with a different port.", handler.Port);

                        //_hosts.Remove // A method to increment the port in ApiCallHandler
                        //retryCount++;
                    }
                }

                if (!hostStarted)
                {
                    Log.Error("Failed to start host for {Name} after {RetryLimit} attempts.", service.Name, _handlerRetryLimit);
                }
            }
        }


        //TODO: refactor
        private IHost CreateHandlerHost(Service service,IMockApi handler)
        {
            Log.Information("Starting to build handler for {name} to {url}", service.Name, service.Url);
            var builder = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel(options => options.Listen(IPAddress.Any, handler.Port))
                    .Configure(app =>
                    {
                        app.Run(async context =>
                        {
                            var a = ApiCallHandlerHelpers.FindTextInstance(context.Request.ContentType);

                            var requestContent = await new StreamReader(context.Request.Body).ReadToEndAsync();
                            var requestMessage = new HttpRequestMessage
                            {
                                Method = new HttpMethod(context.Request.Method),
                                RequestUri = new Uri($"{service.Url}{context.Request.Path}"),
                                Content = new StringContent(requestContent, Encoding.UTF8, a)
                            };

                            // Copy headers from the incoming request to the outgoing request
                            foreach (var header in context.Request.Headers)
                            {
                                requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                            }

                            // Assuming handler is available here; if not, it should be handled differently
                            var response = await handler.SendRequestAsync(requestMessage);

                            // Set the status code and headers before writing to the response body
                            context.Response.StatusCode = (int)response.StatusCode;
                            foreach (var header in response.Content.Headers)
                            {
                                context.Response.Headers[header.Key] = header.Value.ToArray();
                            }
                            foreach (var header in response.Headers)
                            {
                                context.Response.Headers[header.Key] = header.Value.ToArray();
                            }

                            if (response.Content != null)
                            {
                                var responseContent = await response.Content.ReadAsByteArrayAsync();
                                await context.Response.Body.WriteAsync(responseContent);
                            }
                        });
                    });
                });

            return builder.Build();
        }

        public async override Task StopAsync(CancellationToken cancellationToken)
        {

            Log.Information("Stopping ProxyAPIMock hosts...");
            _hosts.ForEach(x => x.StopAsync());

            foreach (var host in _hosts)
            {
                await host.StopAsync(cancellationToken);
            }

            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            Log.Information("Disposing ProxyAPIMock hosts...");
            _hosts.ForEach(x => x.Dispose());

            base.Dispose();
        }
    }
}