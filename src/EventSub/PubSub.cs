using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Retry.Simple;

namespace EventSub;

static class PubSub
{
    static readonly ConcurrentDictionary<string, (Subscriber, ConcurrentBag<(string, IBus)>)> subscribers =
        new();

    internal static async Task DeleteSubscriber(string name)
    {
        if (subscribers.Remove(name, out var subscriber))
        {
            var (_, subscriptions) = subscriber;
            foreach (var (type, bus) in subscriptions)
            {
                await bus.Advanced.Topics.Unsubscribe(type);
                bus.Dispose();
            }
        }
    }

    internal static Publish CreatePublisher(Database database, Action<RebusLoggingConfigurer>? logging )
    {
        var configurer = Configure.With(new BuiltinHandlerActivator()).Logging(l => logging?.Invoke(l));
        configurer =
            database.Type switch
            {
                DatabaseType.MySql =>
                    configurer
                        .Transport(config =>
                            config.UseMySql(new MySqlTransportOptions(database.ConnectionString), "Publisher"))
                        .Subscriptions(config =>
                            config.StoreInMySql(database.ConnectionString, "Subscriptions", true)),
                DatabaseType.SqlServer =>
                    configurer
                        .Transport(config =>
                            config.UseSqlServer(new SqlServerTransportOptions(database.ConnectionString),
                                "Publisher"))
                        .Subscriptions(config =>
                            config.StoreInSqlServer(database.ConnectionString, "Subscriptions", true)),
                DatabaseType.PostgreSql =>
                    configurer
                        .Transport(config =>
                            config.UsePostgreSql(database.ConnectionString, "Messages", "Publisher"))
                        .Subscriptions(config =>
                            config.StoreInPostgres(database.ConnectionString, "Subscriptions", true)),
                _ =>
                    throw new ArgumentException("Unhandled Database")
            };
        return configurer.Start().Advanced.Topics.Publish;
    }

    internal static async Task<bool> CreateSubscriber(Database database, Subscriber subscriber,
        Action<RebusLoggingConfigurer>? logging )
    {
        switch (subscribers.Keys.Contains(subscriber.Name))
        {
            case false:
            {
                var subscriptions = new ConcurrentBag<(string, IBus)>();
                foreach (var type in subscriber.Types)
                {
                    var activator = new BuiltinHandlerActivator();
                    var configurer = Configure.With(activator).Logging(l => logging?.Invoke(l));
                    configurer =
                        database.Type switch
                        {
                            DatabaseType.MySql =>
                                configurer
                                    .Transport(config =>
                                        config.UseMySql(new MySqlTransportOptions(database.ConnectionString),
                                            subscriber.Name))
                                    .Subscriptions(config =>
                                        config.StoreInMySql(database.ConnectionString, "Subscriptions", true)),
                            DatabaseType.SqlServer =>
                                configurer
                                    .Transport(config =>
                                        config.UseSqlServer(new SqlServerTransportOptions(database.ConnectionString),
                                            subscriber.Name))
                                    .Subscriptions(config =>
                                        config.StoreInSqlServer(database.ConnectionString, "Subscriptions", true)),
                            DatabaseType.PostgreSql =>
                                configurer
                                    .Transport(config =>
                                        config.UsePostgreSql(database.ConnectionString, "Messages", subscriber.Name))
                                    .Subscriptions(config =>
                                        config.StoreInPostgres(database.ConnectionString, "Subscriptions", true)),
                            _ =>
                                throw new ArgumentException("Unhandled Database")
                        };
                    var bus =
                        configurer
                            .Options(config => config.SimpleRetryStrategy($"{subscriber.Name}_deadletter", 1,
                                true))
                            .Options(config =>
                                config.SetMaxParallelism(subscriber.MaxParallelism ?? Options.DefaultMaxParallelism))
                            .Start();
                    bus.Advanced.Workers.SetNumberOfWorkers(0);
                    var handler = new MessageHandler(subscriber.RetryIntervals,
                        new Uri(subscriber.Url), subscriber.ApiKey, bus);
                    activator.Register(() => handler);
                    bus.Advanced.Workers.SetNumberOfWorkers(
                        subscriber.NumberOfWorkers ?? Options.DefaultNumberOfWorkers);
                    await bus.Advanced.Topics.Subscribe(type);
                    subscriptions.Add((type, bus));
                }

                subscribers.TryAdd(subscriber.Name, (subscriber, subscriptions));
                return true;
            }
            default:
                return false;
        }
    }

    internal delegate Task Publish(string topic, object eventMessage,
        IDictionary<string, string>? optionalHeaders = null);
}