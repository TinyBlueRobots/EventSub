using System.Collections.Generic;
using System.Linq;
using MySqlConnector;
using Dapper;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace EventSub
{
    class MySqlClient : ISqlClient
    {
        readonly string connectionString;

        public MySqlClient(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task CreateSubscribersTable()
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS Subscribers (Name VARCHAR(128), Subscriber TEXT, CONSTRAINT PK_Subscribers PRIMARY KEY (Name))");
            }
        }

        public async Task DeleteSubscriber(string name)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.ExecuteAsync($"DROP TABLE `{name}`");
                await connection.ExecuteAsync($"DROP TABLE `{name}_deadletter`");
                await connection.ExecuteAsync($"DELETE FROM Subscribers WHERE Name='{name}'");
            }
        }

        public async Task<Dictionary<string, (int, int)>> GetMessageCounts()
        {
            if (PubSub.Subscribers.Count > 0)
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    var messageCountSql = PubSub.Subscribers.Aggregate("", (sql, kvp) => sql + $"SELECT '{kvp.Key}' AS Name, COUNT(*) AS Count FROM `{kvp.Key}`;");
                    var deadLetterCountSql = PubSub.Subscribers.Aggregate("", (sql, kvp) => sql + $"SELECT '{kvp.Key}' AS Name, COUNT(*) AS Count FROM `{kvp.Key}_deadletter`;");
                    var messageCountResults = await connection.QueryAsync(messageCountSql);
                    var deadLetterCountResults = await connection.QueryAsync(deadLetterCountSql);
                    var deadLetterCounts = deadLetterCountResults.ToDictionary(result => result.Name, result => result.Count);
                    return messageCountResults.ToDictionary(messageCount => (string)messageCount.Name, messageCount => ((int)messageCount.Count, (int)deadLetterCounts[messageCount.Name]));
                }
            }
            else { return new Dictionary<string, (int, int)>(); }
        }

        public async Task<IEnumerable<Subscriber>> ReadSubscribers()
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                var json = await connection.QueryAsync<string>("SELECT Subscriber FROM Subscribers");
                return json.Select(json => JsonConvert.DeserializeObject<Subscriber>(json));
            }
        }

        public async Task CreateSubscriber(Subscriber subscriber)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                var json = JsonConvert.SerializeObject(subscriber);
                await connection.ExecuteAsync($"INSERT IGNORE INTO Subscribers (Name, Subscriber) VALUES ('{subscriber.Name}','{json}')");
            }
        }
    }
}