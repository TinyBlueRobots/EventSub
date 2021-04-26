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
using System.Data.SqlClient;
using Npgsql;

namespace Tests
{
    record Subscriber(string Name, string[] Types, string Uri, int[] RetryIntervals, int? MaxParallelism, int? NumberOfWorkers);

    public class TestApi : IDisposable
    {
        const string MySqlConnectionString = "Server=localhost;Database=test;Uid=root;Pwd=password;IgnoreCommandTransaction=true;Allow User Variables=true";
        const string SqlServerConnectionString = "Server=localhost;Initial Catalog=test;Persist Security Info=False;User ID=sa;Password=P@55w0rd";
        const string PostgreSqlConnectionString = "Server=localhost;Port=5432;Database=test;User Id=postgres;Password=password";

        HttpClient httpClient;
        public FakeService Handler;
        AutoResetEvent autoResetEvent = new AutoResetEvent(false);

        TestApi(string databaseType)
        {
            Handler = new FakeService();
            Handler.AddResponse(".*", Method.POST, Response.WithDelegate(_ => { autoResetEvent.Set(); return Response.WithStatusCode(200); }));
            Handler.Start();
            Database database = null;
            switch (databaseType)
            {
                case nameof(Database.MySql):
                    using (var connection = new MySqlConnection(MySqlConnectionString))
                    {
                        connection.Execute("DROP TABLE test.Subscriptions;DROP TABLE test.Subscribers");
                    }
                    database = Database.MySql(MySqlConnectionString);
                    break;
                case nameof(Database.SqlServer):
                    using (var connection = new SqlConnection(SqlServerConnectionString))
                    {
                        connection.Execute("DROP TABLE IF EXISTS Subscriptions;DROP TABLE IF EXISTS Subscribers");
                    }
                    database = Database.SqlServer(SqlServerConnectionString);
                    break;
                case nameof(Database.PostgreSql):
                    using (var connection = new NpgsqlConnection(PostgreSqlConnectionString))
                    {
                        connection.Execute("DROP TABLE IF EXISTS Subscriptions;DROP TABLE IF EXISTS Subscribers");
                    }
                    database = Database.PostgreSql(PostgreSqlConnectionString);
                    break;
            }
            var webHost = new WebHostBuilder().UseEventSub(database, "apikey");
            var testServer = new TestServer(webHost);
            httpClient = testServer.CreateClient();
            httpClient.DefaultRequestHeaders.Add("X-API-KEY", "apikey");
        }

        public static TestApi MySql()
        {
            return new TestApi(nameof(Database.MySql));
        }

        public static TestApi SqlServer()
        {
            return new TestApi(nameof(Database.SqlServer));
        }

        public static TestApi PostgreSql()
        {
            return new TestApi(nameof(Database.PostgreSql));
        }

        public Task<HttpResponseMessage> CreateSubscriber(string json)
        {
            return httpClient.PostAsync("/subscribers", new StringContent(json));
        }

        public Task<string> GetSubscribers()
        {
            return httpClient.GetStringAsync("/subscribers");
        }

        public Task<HttpResponseMessage> GetSubscriber(string name)
        {
            return httpClient.GetAsync($"/subscribers/{name}");
        }

        public Task<HttpResponseMessage> DeleteSubscriber(string name)
        {
            return httpClient.DeleteAsync($"/subscribers/{name}");
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