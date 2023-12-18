using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

public class ApiLoadTester
{
    private readonly string _endpointUrl;
    private readonly int _durationInSeconds;
    private readonly HttpClient _httpClient;
    private readonly int _userCount;
    private readonly Random _random;

    public ApiLoadTester(string endpointUrl, int durationInSeconds, int userCount)
    {
        _endpointUrl = endpointUrl;
        _durationInSeconds = durationInSeconds;
        _userCount = userCount;
        _httpClient = new HttpClient();
        _random = new Random();
    }

    public async Task RunTests()
    {
        var tasks = new List<Task>();
        CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(_durationInSeconds));

        try
        {
            Console.WriteLine($"Starting load test for {_durationInSeconds} seconds with {_userCount} users...");

            for (int i = 0; i < _userCount; i++)
            {
                tasks.Add(SimulateUser(cts.Token));
            }

            await Task.WhenAll(tasks);

            Console.WriteLine("Load test completed.");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Load test cancelled.");
        }
        finally
        {
            cts.Dispose();
        }
    }

    private async Task SimulateUser(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await CallEndpointAsync();
            await Task.Delay(_random.Next(1000, 3000), cancellationToken); // Delay between 1-3 seconds
        }
    }

    private async Task CallEndpointAsync()
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(_endpointUrl);
            string responseBody = await response.Content.ReadAsStringAsync();

            // Optional: Log the response or status code
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Request failed: {e.Message}");
        }
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        string endpointUrl = "https://your-api-endpoint.com";
        int durationInSeconds = 60; // Run the test for 60 seconds
        int userCount = 100; // Simulate 100 users

        ApiLoadTester tester = new ApiLoadTester(endpointUrl, durationInSeconds, userCount);
        await tester.RunTests();
    }
}
