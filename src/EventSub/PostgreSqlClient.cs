using System.Collections.Generic;
using System.Linq;
using Dapper;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Npgsql;

namespace EventSub
{
    class PostgreSqlClient : ISqlClient
    {
        string connectionString;

        public PostgreSqlClient(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task CreateSubscribersTable()
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS Subscribers (Name VARCHAR(128), Subscriber TEXT, CONSTRAINT PK_Subscribers PRIMARY KEY (Name))");
            }
        }

        public async Task DeleteSubscriber(string name)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.ExecuteAsync($"DROP TABLE IF EXISTS \"{name.ToLower()}\";DROP TABLE IF EXISTS \"{name.ToLower()}_deadletter\";DELETE FROM Subscribers WHERE Name='{name}'");
            }
        }

        public async Task<(int, int)> GetMessageCount(string name)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                var subscriberExists = await connection.ExecuteAsync($"SELECT COUNT(*) FROM Subscribers WHERE Name='{name}'");
                if (subscriberExists != 0)
                {
                    var messageCountSql = $"SELECT COUNT(*) AS Count FROM \"{name.ToLower()}\"";
                    var deadLetterCountSql = $"SELECT COUNT(*) AS Count FROM \"{name.ToLower()}_deadletter\";";
                    var messageCount = await connection.ExecuteAsync(messageCountSql);
                    var deadLetterCount = await connection.ExecuteAsync(deadLetterCountSql);
                    return (messageCount, deadLetterCount);
                }
                else { return (0, 0); }
            }
        }

        public async Task<Dictionary<string, (int, int)>> GetMessageCounts()
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                var subscriberNames = await connection.QueryAsync<string>("SELECT Name FROM Subscribers");
                if (subscriberNames.Count() > 0)
                {
                    var messageCountSql = subscriberNames.Aggregate("", (sql, name) => sql + $"SELECT '{name}' AS Name, COUNT(*) AS Count FROM \"{name.ToLower()}\";");
                    var deadLetterCountSql = subscriberNames.Aggregate("", (sql, name) => sql + $"SELECT '{name}' AS Name, COUNT(*) AS Count FROM \"{name.ToLower()}_deadletter\";");
                    var messageCountResults = await connection.QueryAsync(messageCountSql);
                    var deadLetterCountResults = await connection.QueryAsync(deadLetterCountSql);
                    var deadLetterCounts = deadLetterCountResults.ToDictionary(result => result.Name, result => result.Count);
                    return messageCountResults.ToDictionary(messageCount => (string)messageCount.Name, messageCount => ((int)messageCount.Count, (int)deadLetterCounts[messageCount.Name]));
                }
                else { return new Dictionary<string, (int, int)>(); }
            }
        }

        public async Task<IEnumerable<Subscriber>> ReadSubscribers()
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                var json = await connection.QueryAsync<string>("SELECT Subscriber FROM Subscribers");
                return json.Select(json => JsonConvert.DeserializeObject<Subscriber>(json));
            }
        }

        public async Task CreateSubscriber(Subscriber subscriber)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                var json = JsonConvert.SerializeObject(subscriber);
                await connection.ExecuteAsync($"INSERT INTO Subscribers (Name, Subscriber) VALUES ('{subscriber.Name}','{json}') ON CONFLICT (Name) DO NOTHING");
            }
        }

        public async Task<Subscriber?> ReadSubscriber(string name)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                var json = await connection.QueryFirstOrDefaultAsync<string>($"SELECT Subscriber FROM Subscribers WHERE Name='{name}'");
                return json is null ? null : JsonConvert.DeserializeObject<Subscriber>(json);
            }
        }
    }
}