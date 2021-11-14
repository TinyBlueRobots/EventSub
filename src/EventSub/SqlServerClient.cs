using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;

namespace EventSub;

class SqlServerClient : IDbClient
{
    readonly string connectionString;

    public SqlServerClient(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public async Task CreateSubscribersTable()
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.ExecuteAsync(
            "IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Subscribers') CREATE TABLE Subscribers (Name VARCHAR(128), Subscriber TEXT, CONSTRAINT PK_Subscribers PRIMARY KEY (Name))");
    }

    public async Task DeleteSubscriber(string name)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.ExecuteAsync(
            $"DROP TABLE [{name}];DROP TABLE [{name}_deadletter];DELETE FROM Subscribers WHERE Name='{name}'");
    }

    public async Task<(int, int)> GetMessageCount(string name)
    {
        await using var connection = new SqlConnection(connectionString);
        var subscriberExists =
            await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM Subscribers WHERE Name='{name}'");
        if (subscriberExists != 0)
        {
            var messageCountSql = $"SELECT COUNT(*) FROM [{name}]";
            var deadLetterCountSql = $"SELECT COUNT(*) FROM [{name}_deadletter];";
            var messageCount = await connection.ExecuteScalarAsync<int>(messageCountSql);
            var deadLetterCount = await connection.ExecuteScalarAsync<int>(deadLetterCountSql);
            return (messageCount, deadLetterCount);
        }

        return (0, 0);
    }

    public async Task<Dictionary<string, (int, int)>> GetMessageCounts()
    {
        await using var connection = new SqlConnection(connectionString);
        var subscriberNames = await connection.QueryAsync<string>("SELECT Name FROM Subscribers")
            .ContinueWith(t => t.Result.ToArray());
        switch (subscriberNames)
        {
            case { Length: 0 }:
                return new Dictionary<string, (int, int)>();
            default:
                if (!subscriberNames.Any()) return new Dictionary<string, (int, int)>();
                var messageCountSql = subscriberNames.Aggregate("",
                    (sql, name) => sql + $"SELECT '{name}' AS Name, COUNT(*) AS Count FROM [{name}];");
                var deadLetterCountSql = subscriberNames.Aggregate("",
                    (sql, name) => sql + $"SELECT '{name}' AS Name, COUNT(*) AS Count FROM [{name}_deadletter];");
                var messageCountResults = await connection.QueryAsync(messageCountSql);
                var deadLetterCountResults = await connection.QueryAsync(deadLetterCountSql);
                var deadLetterCounts =
                    deadLetterCountResults.ToDictionary(result => result.Name, result => result.Count);
                return messageCountResults.ToDictionary(messageCount => (string)messageCount.Name,
                    messageCount => ((int)messageCount.Count, (int)deadLetterCounts[messageCount.Name]));
        }
    }

    public async Task<IEnumerable<Subscriber>> ReadSubscribers()
    {
        await using var connection = new SqlConnection(connectionString);
        var json = await connection.QueryAsync<string>("SELECT Subscriber FROM Subscribers");
        return json.Select(JsonConvert.DeserializeObject<Subscriber>);
    }

    public async Task CreateSubscriber(Subscriber subscriber)
    {
        await using var connection = new SqlConnection(connectionString);
        var json = JsonConvert.SerializeObject(subscriber);
        await connection.ExecuteAsync(
            $"IF NOT EXISTS (SELECT * FROM Subscribers WHERE Name='{subscriber.Name}') INSERT INTO Subscribers (Name, Subscriber) VALUES ('{subscriber.Name}','{json}')");
    }

    public async Task<Subscriber?> ReadSubscriber(string name)
    {
        await using var connection = new SqlConnection(connectionString);
        var json = await connection.QueryFirstOrDefaultAsync<string>(
            $"SELECT Subscriber FROM Subscribers WHERE Name='{name}'");
        return json is null ? null : JsonConvert.DeserializeObject<Subscriber>(json);
    }
}