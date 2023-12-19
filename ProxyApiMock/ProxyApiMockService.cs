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

    public class ProxyApiMockService : BackgroundService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IFileReader _fileReader;
        private List<IHost> _hosts = new List<IHost>();
        private string _directory;

        public ProxyApiMockService(IHttpClientFactory httpClientFactory, IFileReader fileReader, IConfiguration configuration)
        {
            Log.Information("Setting the service up.");
            _directory = Path.GetDirectoryName(AppContext.BaseDirectory);
            _httpClientFactory = httpClientFactory;
            _fileReader = fileReader;
            _configuration = configuration;
            Log.Information("Finnished setting the service up.");
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
                var handler = new ApiCallHandler(_httpClientFactory, service, logger, _fileReader, _configuration);

                var host = CreateHandlerHost(service, handler);
                _hosts.Add(host);
                await host.StartAsync(stoppingToken);
                Log.Information("Started host for Config: {Name} proxying to {Url} on port: {Port} - logging into {directory}", service.Name, service.Url, handler.Port, _directory); ;
            }
        }

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
                            var requestContent = await new StreamReader(context.Request.Body).ReadToEndAsync();
                            var requestMessage = new HttpRequestMessage
                            {
                                Method = new HttpMethod(context.Request.Method),
                                RequestUri = new Uri($"{service.Url}{context.Request.Path}"),
                                Content = new StringContent(requestContent, Encoding.UTF8, context.Request.ContentType)
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