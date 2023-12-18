using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using ProxyApiMock;
using ProxyApiMock.Interfaces;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Async(x => x.Console())
            .WriteTo.Async(x => x.File("Logs/ProxyApiMock.log"))
            .CreateLogger();

        try
        {
            Log.Information("Starting up");
            CreateHostBuilder(args).Build().Run();
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

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService() // This line is added to make the app run as a Windows service.
            .UseSerilog() // Use Serilog as the logging framework.
            .ConfigureAppConfiguration((config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
            })
            .ConfigureServices((hostingContext, services) =>
            {
                services.AddSingleton<IFileReader, FileReader>();
                services.AddHostedService<ProxyApiMockService>();
                services.AddHttpClient("InsecureClient")
                .ConfigurePrimaryHttpMessageHandler(() => GetInsecureHandler());
            });

    private static HttpClientHandler GetInsecureHandler()
    {
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
    }
}
