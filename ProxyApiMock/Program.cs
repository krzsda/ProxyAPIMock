using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using ProxyApiMock;
using ProxyApiMock.Interfaces;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Async(x => x.Console())
            .WriteTo.Async(x => x.File(Path.Combine(AppContext.BaseDirectory, "Logs", "ProxyApiMock.log")))
            .CreateLogger();

        //var files = Directory.GetFiles(AppContext.BaseDirectory, "appsettings.json");
        //Log.Information(AppContext.BaseDirectory.ToString());
        //Log.Information("Found jsons {c}", files.Length);

        //var configBuilder = new ConfigurationBuilder()
        //    .SetBasePath(AppContext.BaseDirectory)
        //    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        //var config = configBuilder.Build();
        //var configData = JsonConvert.SerializeObject(config.AsEnumerable().ToDictionary(c => c.Key, c => c.Value));
        //Log.Information($"Configuration Data: {configData}");
        //var services = config.GetSection("Services").Get<List<Service>>();
        //Log.Information("From {object}", JsonConvert.SerializeObject(services));
        //Log.Information($"Services count: {services?.Count}");
        var hostBuilder = CreateHostBuilder(args);

        var host = hostBuilder.Build();

        try
        {
            Log.Information("Starting up");
            host.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application start-up failed");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseWindowsService()
            .ConfigureAppConfiguration((hostContext, configBuilder) =>
            {
                configBuilder.SetBasePath(AppContext.BaseDirectory);
                configBuilder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                configBuilder.AddEnvironmentVariables();
                if (args != null)
                {
                    configBuilder.AddCommandLine(args);
                }
            })
            .ConfigureServices((hostContext, services) =>
            {
                // Add your services and configuration here
                services.AddSingleton<IFileReader, FileReader>();
                services.AddHostedService<ProxyApiMockService>();
                services.AddHttpClient("InsecureClient")
                    .ConfigurePrimaryHttpMessageHandler(() => GetInsecureHandler());

                // Configure Logging
                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(hostContext.Configuration)
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "Logs", "ProxyApiMock.log"))
                    .CreateLogger();

                services.AddLogging(loggingBuilder =>
                    loggingBuilder.AddSerilog(dispose: true));
            });
    }


    private static HttpClientHandler GetInsecureHandler()
        {
            return new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
        }
    }
