using System;
using System.Data.SqlClient;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EventSub;
using Hornbill;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using MySqlConnector;
using Npgsql;

namespace Tests;

record Subscriber(string Name, string[] Types, string Url, string? ApiKey, int[]? RetryIntervals, int? MaxParallelism,
    int? NumberOfWorkers);

public class TestApi : IDisposable
{
    const string MySqlConnectionString =
        "Server=localhost;Database=test;Uid=root;Pwd=password;IgnoreCommandTransaction=true;Allow User Variables=true";

    const string SqlServerConnectionString =
        "Server=localhost;Initial Catalog=test;Persist Security Info=False;User ID=sa;Password=P@55w0rd;TrustServerCertificate=True";

    const string PostgreSqlConnectionString =
        "Server=localhost;Port=5432;Database=test;User Id=postgres;Password=password";

    readonly AutoResetEvent _autoResetEvent1 = new(false);
    readonly AutoResetEvent _autoResetEvent2 = new(false);

    readonly HttpClient _httpClient;
    public readonly FakeService Handler1;
    public readonly FakeService Handler2;

    TestApi(string databaseType)
    {
        Handler1 = new FakeService();
        Handler1.AddResponse(".*", Method.POST, Response.WithDelegate(request =>
        {
            _autoResetEvent1.Set();
            return request.Headers["X-API-KEY"][0] == "apikey"
                ? Response.WithStatusCode(200)
                : Response.WithStatusCode(401);
        }));
        Handler1.Start();
        Handler2 = new FakeService();
        Handler2.AddResponse(".*", Method.POST, Response.WithDelegate(request =>
        {
            _autoResetEvent2.Set();
            return request.Headers["X-API-KEY"][0] == "apikey"
                ? Response.WithStatusCode(200)
                : Response.WithStatusCode(401);
        }));
        Handler2.Start();
        Database? database = null;
        switch (databaseType)
        {
            case nameof(Database.MySql):
                using (var connection = new MySqlConnection(MySqlConnectionString))
                {
                    connection.Execute("DROP TABLE IF EXISTS test.Subscriptions;DROP TABLE IF EXISTS test.Subscribers");
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

        var webHost = new WebHostBuilder().UseEventSub(database!, "apikey");
        var testServer = new TestServer(webHost);
        _httpClient = testServer.CreateClient();
        _httpClient.DefaultRequestHeaders.Add("X-API-KEY", "apikey");
    }

    public void Dispose()
    {
        Handler1.Dispose();
        Handler2.Dispose();
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
        return _httpClient.PostAsync("/subscribers", new StringContent(json));
    }

    public Task<string> GetSubscribers()
    {
        return _httpClient.GetStringAsync("/subscribers");
    }

    public Task<HttpResponseMessage> GetSubscriber(string name)
    {
        return _httpClient.GetAsync($"/subscribers/{name}");
    }

    public Task<HttpResponseMessage> DeleteSubscriber(string name)
    {
        return _httpClient.DeleteAsync($"/subscribers/{name}");
    }

    public async Task<HttpResponseMessage> PublishMessage(string json)
    {
        var response = await _httpClient.PostAsync("/", new StringContent(json));
        _autoResetEvent1.WaitOne(5000);
        _autoResetEvent2.WaitOne(5000);
        return response;
    }
}