using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Retry.Simple;

namespace EventSub
{
    class PubSub
    {
        internal static ConcurrentDictionary<string, (Subscriber, List<(string, IBus)>)> Subscribers = new ConcurrentDictionary<string, (Subscriber, List<(string, IBus)>)>();

        internal delegate Task Publish(string topic, object eventMessage, IDictionary<string, string>? optionalHeaders = null);

        internal static async Task DeleteSubscriber(string name)
        {
            if (Subscribers.Remove(name, out var subscriber))
            {
                var (_, subscriptions) = subscriber;
                foreach (var (type, bus) in subscriptions)
                {
                    await bus.Advanced.Topics.Unsubscribe(type);
                    bus.Dispose();
                }
            }
        }

        internal static Publish CreatePublisher(Database database)
        {
            var configurer = Configure.With(new BuiltinHandlerActivator());
            switch (database.Type)
            {
                case DatabaseType.MySql:
                    configurer =
                        configurer
                            .Transport(config => config.UseMySql(new MySqlTransportOptions(database.ConnectionString), "Publisher"))
                            .Subscriptions(config => config.StoreInMySql(database.ConnectionString, "Subscriptions", true));
                    break;
                case DatabaseType.SqlServer:
                    configurer =
                        configurer
                            .Transport(config => config.UseSqlServer(new SqlServerTransportOptions(database.ConnectionString), "Publisher"))
                            .Subscriptions(config => config.StoreInSqlServer(database.ConnectionString, "Subscriptions", true));
                    break;
                case DatabaseType.PostgreSql:
                    configurer =
                        configurer
                            .Transport(config => config.UsePostgreSql(database.ConnectionString, "Publisher", "Publisher"))
                            .Subscriptions(config => config.StoreInPostgres(database.ConnectionString, "Subscriptions", true));
                    break;
                default:
                    throw new System.ArgumentException("Unhandled DatabaseConfig");
            }
            return configurer.Start().Advanced.Topics.Publish;
        }

        internal static async Task<bool> CreateSubscriber(Database database, Subscriber subscriber)
        {
            if (!Subscribers.Keys.Contains(subscriber.Name))
            {
                var subscriptions = new List<(string, IBus)>();
                foreach (var type in subscriber.Types)
                {
                    var activator = new BuiltinHandlerActivator();
                    var configurer = Configure.With(activator);
                    switch (database.Type)
                    {
                        case DatabaseType.MySql:
                            configurer =
                                configurer
                                    .Transport(config => config.UseMySql(new MySqlTransportOptions(database.ConnectionString), subscriber.Name))
                                    .Subscriptions(config => config.StoreInMySql(database.ConnectionString, "Subscriptions", true));
                            break;
                        case DatabaseType.SqlServer:
                            configurer =
                                configurer
                                    .Transport(config => config.UseSqlServer(new SqlServerTransportOptions(database.ConnectionString), subscriber.Name))
                                    .Subscriptions(config => config.StoreInSqlServer(database.ConnectionString, "Subscriptions", true));
                            break;
                        case DatabaseType.PostgreSql:
                            configurer =
                                configurer
                                    .Transport(config => config.UsePostgreSql(database.ConnectionString, subscriber.Name, subscriber.Name))
                                    .Subscriptions(config => config.StoreInPostgres(database.ConnectionString, "Subscriptions", true));
                            break;
                        default:
                            throw new System.ArgumentException("Unhandled DatabaseConfig");
                    }
                    var bus =
                        configurer
                            .Options(config => config.SimpleRetryStrategy($"{subscriber.Name}_deadletter", 1, secondLevelRetriesEnabled: true))
                            .Options(config => config.SetMaxParallelism(subscriber.MaxParallelism ?? Options.DefaultMaxParallelism))
                            .Start();
                    bus.Advanced.Workers.SetNumberOfWorkers(0);
                    var handler = new MessageHandler(subscriber.RetryIntervals, subscriber.Name, new Uri(subscriber.Uri), bus);
                    activator.Register(() => handler);
                    bus.Advanced.Workers.SetNumberOfWorkers(subscriber.NumberOfWorkers ?? Options.DefaultNumberOfWorkers);
                    await bus.Advanced.Topics.Subscribe(type);
                    subscriptions.Add((type, bus));
                }
                Subscribers.TryAdd(subscriber.Name, (subscriber, subscriptions));
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
