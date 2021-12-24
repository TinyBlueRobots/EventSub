using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json;
using Npgsql;

namespace EventSub;

class PostgreSqlClient : IDbClient
{
    readonly string _connectionString;

    public PostgreSqlClient(string connectionString)
    {
        this._connectionString = connectionString;
    }

    public async Task CreateSubscribersTable()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync(
            "CREATE TABLE IF NOT EXISTS Subscribers (Name VARCHAR(128), Subscriber TEXT, CONSTRAINT PK_Subscribers PRIMARY KEY (Name))");
    }

    public async Task DeleteSubscriber(string name)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync(
            $"DELETE FROM messages WHERE recipient='{name.ToLower()}' or recipient='{name.ToLower()}_deadletter';DELETE FROM Subscribers WHERE Name='{name}'");
    }

    public async Task<(int, int)> GetMessageCount(string name)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var messageCountSql = $"SELECT COUNT(*) FROM messages WHERE recipient='{name.ToLower()}'";
        var deadLetterCountSql = $"SELECT COUNT(*) FROM messages WHERE recipient='{name.ToLower()}_deadletter'";
        var messageCount = await connection.ExecuteScalarAsync<int>(messageCountSql);
        var deadLetterCount = await connection.ExecuteScalarAsync<int>(deadLetterCountSql);
        return (Math.Max(messageCount, 0), Math.Max(deadLetterCount, 0));
    }

    public async Task<Dictionary<string, (int, int)>> GetMessageCounts()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var subscriberNames = await connection.QueryAsync<string>("SELECT Name FROM Subscribers")
            .ContinueWith(t => t.Result.ToArray());
        switch (subscriberNames)
        {
            case { Length: 0 }:
                return new Dictionary<string, (int, int)>();
            default:
                var messageCountSql = subscriberNames.Aggregate("",
                    (sql, name) =>
                        sql +
                        $"SELECT '{name}' AS Name, COUNT(*) AS Count FROM messages WHERE recipient='{name.ToLower()}';");
                var deadLetterCountSql = subscriberNames.Aggregate("",
                    (sql, name) =>
                        sql +
                        $"SELECT '{name}' AS Name, COUNT(*) AS Count FROM messages WHERE recipient='{name.ToLower()}_deadletter';");
                var messageCountResults = await connection.QueryAsync(messageCountSql);
                var deadLetterCountResults = await connection.QueryAsync(deadLetterCountSql);
                var deadLetterCounts =
                    deadLetterCountResults.ToDictionary(result => result.name, result => result.count);
                return messageCountResults.ToDictionary(messageCount => (string)messageCount.name,
                    messageCount => (Math.Max((int)messageCount.count, 0),
                        Math.Max((int)deadLetterCounts[messageCount.name], 0)));
        }
    }

    public async Task<IEnumerable<Subscriber>> ReadSubscribers()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var json = await connection.QueryAsync<string>("SELECT Subscriber FROM Subscribers");
        return json.Select(JsonConvert.DeserializeObject<Subscriber>);
    }

    public async Task CreateSubscriber(Subscriber subscriber)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var json = JsonConvert.SerializeObject(subscriber);
        await connection.ExecuteAsync(
            $"INSERT INTO Subscribers (Name, Subscriber) VALUES ('{subscriber.Name}','{json}') ON CONFLICT (Name) DO NOTHING");
    }

    public async Task<Subscriber?> ReadSubscriber(string name)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var json = await connection.QueryFirstOrDefaultAsync<string>(
            $"SELECT Subscriber FROM Subscribers WHERE Name='{name}'");
        return json is null ? null : JsonConvert.DeserializeObject<Subscriber>(json);
    }
}