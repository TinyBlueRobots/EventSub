using System.Net.Http;
using System.Threading.Tasks;
using EventSub;
using Hornbill;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Dapper;
using MySqlConnector;
using System.Threading;
using System;

namespace Tests
{
    record Subscriber(string Name, string[] Types, string Uri, int[] RetryIntervals, int? MaxParallelism, int? NumberOfWorkers);

    class TestApi : IDisposable
    {
        const string ConnectionString = "Server=localhost;Database=test;Uid=root;Pwd=password;IgnoreCommandTransaction=true;Allow User Variables=true";
        HttpClient httpClient;
        public FakeService Handler;
        AutoResetEvent autoResetEvent = new AutoResetEvent(false);

        public TestApi()
        {
            Handler = new FakeService();
            Handler.AddResponse(".*", Method.POST, Response.WithDelegate(_ => { autoResetEvent.Set(); return Response.WithStatusCode(200); }));
            Handler.Start();
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Execute("DROP TABLE test.Subscriptions;DROP TABLE test.Subscribers;");
            }
            var webHost = new WebHostBuilder().UseEventSub(new MySqlConfig(ConnectionString), "apikey");
            var testServer = new TestServer(webHost);
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

        public Task<string> GetSubscriber(string name)
        {
            return httpClient.GetStringAsync($"/subscribers/{name}");
        }

        public async Task<HttpResponseMessage> PublishMessage(string json)
        {
            var response = await httpClient.PostAsync("/", new StringContent(json));
            autoResetEvent.WaitOne(5000);
            return response;
        }

        public void Dispose()
        {
            Handler.Dispose();
        }
    }
}