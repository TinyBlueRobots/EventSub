using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Retry.Simple;
using System.Linq;
using System.Net.Http;

namespace EventSub
{
    class PubSub
    {
        internal static ConcurrentDictionary<string, (Subscriber, List<(string, IBus)>)> Subscribers = new ConcurrentDictionary<string, (Subscriber, List<(string, IBus)>)>();

        internal delegate Task Publish(string topic, object eventMessage, IDictionary<string, string> optionalHeaders = null);

        internal static async Task DeleteSubscriber(string name)
        {
            (Subscriber, List<(string, IBus)>) subscriber;
            if (Subscribers.Remove(name, out subscriber))
            {
                var (_, subscriptions) = subscriber;
                foreach (var (type, bus) in subscriptions)
                {
                    await bus.Advanced.Topics.Unsubscribe(type);
                    bus.Dispose();
                }
            }
        }

        internal static Publish CreatePublisher(DatabaseConfig databaseConfig)
        {
            var configurer = Configure.With(new BuiltinHandlerActivator());
            switch (databaseConfig)
            {
                case MySqlConfig sqlConfig:
                    configurer =
                        configurer
                            .Transport(config => config.UseMySql(new MySqlTransportOptions(sqlConfig.ConnectionString), "Publisher"))
                            .Subscriptions(config => config.StoreInMySql(sqlConfig.ConnectionString, "Subscriptions", true));
                    break;
                default:
                    throw new System.ArgumentException("Unhandled DatabaseConfig");
            }
            return configurer.Start().Advanced.Topics.Publish;
        }

        internal static async Task<bool> CreateSubscriberAsync(DatabaseConfig databaseConfig, Subscriber subscriber, HttpClient httpClient)
        {
            if (!Subscribers.Keys.Contains(subscriber.Name))
            {
                var subscriptions = new List<(string, IBus)>();
                foreach (var type in subscriber.Types)
                {
                    var activator = new BuiltinHandlerActivator();
                    var configurer = Configure.With(activator);
                    switch (databaseConfig)
                    {
                        case MySqlConfig sqlConfig:
                            configurer =
                                configurer
                                    .Transport(config => config.UseMySql(new MySqlTransportOptions(sqlConfig.ConnectionString), subscriber.Name))
                                    .Subscriptions(config => config.StoreInMySql(sqlConfig.ConnectionString, "Subscriptions", true));
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
                    var handler = new MessageHandler(httpClient, subscriber.RetryIntervals, subscriber.Name, new Uri(subscriber.Uri), bus);
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
