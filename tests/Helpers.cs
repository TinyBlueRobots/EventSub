using System.Net.Http;
using System.Threading.Tasks;
using EventSub;
using Hornbill;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Dapper;
using MySqlConnector;

namespace Tests
{
    record Subscriber(string Name, string[] Types, string Uri, int[] RetryIntervals, int? MaxParallelism, int? NumberOfWorkers);

    class TestApi
    {
        const string ConnectionString = "Server=localhost;Database=test;Uid=root;Pwd=password;IgnoreCommandTransaction=true;Allow User Variables=true";
        HttpClient httpClient;
        public FakeService Handler { get; }

        public TestApi()
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Execute("DELETE FROM test.Subscriptions");
                connection.Execute("DELETE FROM test.Subscribers");
            }
            var webHost = new WebHostBuilder().UseEventSub(new MySqlConfig(ConnectionString), "apikey");
            var testServer = new TestServer(webHost);
            Handler = new FakeService();
            Handler.AddResponse(".*", Method.GET, Response.WithStatusCode(200));
            Handler.Start();
            httpClient = testServer.CreateClient();
            httpClient.DefaultRequestHeaders.Add("X-API-KEY", "apikey");
        }

        public Task<HttpResponseMessage> RegisterSubscriber(string json)
        {
            return httpClient.PostAsync("/subscribers", new StringContent(json));
        }

        public Task<string> GetSubscribers()
        {
            return httpClient.GetStringAsync("/subscribers");
        }
    }
}