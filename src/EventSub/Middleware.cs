using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace EventSub
{
    public static class IWebHostBuilderExtensions
    {
        static ISqlClient CreateSqlClient(DatabaseConfig databaseConfig)
        {
            switch (databaseConfig)
            {
                case MySqlConfig config:
                    return new MySqlClient(config.ConnectionString);
                default:
                    throw new System.ArgumentException("Unhandled DatabaseConfig");
            }
        }

        static async Task GetSubscribers(DatabaseConfig databaseConfig, HttpContext ctx)
        {
            var messageCount = PubSub.Subscribers.Aggregate("", (sql, kvp) => sql + $"SELECT ${kvp.Key} AS Name, COUNT(*) FROM ${kvp.Key};");
            var deadLetterCount = PubSub.Subscribers.Aggregate("", (sql, kvp) => sql + $"SELECT ${kvp.Key} AS Name, COUNT(*) FROM ${kvp.Key}-deadletter;");
            var sqlClient = CreateSqlClient(databaseConfig);
            var messageCounts = await sqlClient.GetMessageCounts();
            var subscriberDetails = new List<dynamic>();
            foreach (var (name, (subscriber, _)) in PubSub.Subscribers)
            {
                var (activeMessageCount, deadLetterMessageCount) = messageCounts[name];
                subscriberDetails.Add(new
                {
                    subscriber.RetryIntervals,
                    subscriber.MaxParallelism,
                    subscriber.Name,
                    subscriber.NumberOfWorkers,
                    subscriber.Types,
                    subscriber.Uri,
                    MessageCount = activeMessageCount,
                    DeadLetterCount = deadLetterMessageCount
                });
            }
            await ctx.Response.WriteAsJsonAsync(subscriberDetails);
        }

        static async Task PublishMessage(PubSub.Publish publish, HttpContext ctx)
        {
            try
            {
                var json = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
                var message = JsonConvert.DeserializeObject<Message>(json);
                await publish(message.Type, message);
            }
            catch (JsonException)
            {
                ctx.Response.StatusCode = 400;
            }
            catch (Exception)
            {
                throw;
            }
        }

        static async Task<bool> TryCreateSubscriber(DatabaseConfig databaseConfig, HttpClient httpClient, Subscriber subscriber)
        {
            if (subscriber.Name is null || !Uri.IsWellFormedUriString(subscriber.Uri, UriKind.Absolute))
            {
                throw new JsonException();
            }
            var created = await PubSub.CreateSubscriberAsync(databaseConfig, subscriber, httpClient);
            if (created)
            {
                await CreateSqlClient(databaseConfig).CreateSubscriber(subscriber);
            }
            return created;
        }

        static bool ValidateName(string name) => Regex.IsMatch(name, "^[A-Za-z0-9]{1,128}$");

        static async Task CreateSubscriber(Func<Subscriber, Task<bool>> tryCreateSubscriber, HttpContext ctx)
        {
            try
            {
                var json = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
                var subscriber = JsonConvert.DeserializeObject<Subscriber>(json);
                if (subscriber.Name is null || !ValidateName(subscriber.Name) || !Uri.IsWellFormedUriString(subscriber.Uri, UriKind.Absolute))
                {
                    throw new JsonException();
                }
                var created = await tryCreateSubscriber(subscriber);
                if (!created)
                {
                    ctx.Response.StatusCode = 409;
                }
            }
            catch (Exception ex)
            {
                switch (ex)
                {
                    case JsonException:
                        ctx.Response.StatusCode = 400;
                        break;

                    default:
                        throw;
                }
            }
        }

        static async Task DeleteSubscriber(DatabaseConfig databaseConfig, HttpContext ctx)
        {
            string name = ctx.Request.RouteValues["name"].ToString();
            await PubSub.DeleteSubscriber(name);
            var sqlClient = CreateSqlClient(databaseConfig);
            await sqlClient.DeleteSubscriber(name);
        }

        public static IWebHostBuilder UseEventSub(this IWebHostBuilder builder, DatabaseConfig databaseConfig, string apiKey)
        {
            var publish = PubSub.CreatePublisher(databaseConfig);
            ISqlClient sqlClient = CreateSqlClient(databaseConfig);
            sqlClient.CreateSubscribersTable().Wait();
            var subscribers = sqlClient.ReadSubscribers().Result;
            builder.ConfigureServices(services => { services.AddHttpClient(); services.AddRouting(); });
            builder.Configure(app =>
           {
               var httpClient = app.ApplicationServices.GetService<IHttpClientFactory>().CreateClient();
               Func<Subscriber, Task<bool>> tryCreateSubscriber = subscriber => TryCreateSubscriber(databaseConfig, httpClient, subscriber);
               foreach (var subscriber in subscribers)
               {
                   tryCreateSubscriber(subscriber).Wait();
               }
               app.UseDeveloperExceptionPage();
               app.Use(async (ctx, next) =>
               {
                   var apiKeyHeader = ctx.Request.Headers["X-API-KEY"].ToString();
                   var apiKeyQuery = ctx.Request.Query["apikey"].ToString();
                   var providedApiKey = string.IsNullOrEmpty(apiKeyHeader) ? apiKeyQuery : apiKeyHeader;
                   var authorized = string.IsNullOrEmpty(apiKey) ? false : apiKey == providedApiKey;
                   if (authorized)
                   {
                       await next();
                   }
                   else
                   {
                       ctx.Response.StatusCode = 401;
                   }
               });

               app.UseRouting();
               app.UseEndpoints(endpoints =>
               {
                   endpoints.MapPost("/", ctx => PublishMessage(publish, ctx));
                   endpoints.MapPost("/subscribers", ctx => CreateSubscriber(tryCreateSubscriber, ctx));
                   endpoints.MapDelete("/subscribers/{name:required}", ctx => DeleteSubscriber(databaseConfig, ctx));
                   endpoints.MapGet("/subscribers", ctx => GetSubscribers(databaseConfig, ctx));
               });
           });
            return builder;
        }
    }
}
