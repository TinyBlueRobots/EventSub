using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EventSub;

public static class WebHostBuilderExtensions
{
    static IDbClient CreateSqlClient(Database database)
    {
        return database.Type switch
        {
            DatabaseType.MySql => new MySqlClient(database.ConnectionString),
            DatabaseType.SqlServer => new SqlServerClient(database.ConnectionString),
            DatabaseType.PostgreSql => new PostgreSqlClient(database.ConnectionString),
            _ => throw new ArgumentException("Unhandled Database")
        };
    }

    static async Task GetSubscribers(Database database, HttpContext ctx)
    {
        var sqlClient = CreateSqlClient(database);
        var messageCounts = await sqlClient.GetMessageCounts();
        var subscribers = await sqlClient.ReadSubscribers();
        var subscriberDetails = new List<dynamic>();
        foreach (var subscriber in subscribers)
        {
            var (activeMessageCount, deadLetterMessageCount) = messageCounts[subscriber.Name];
            subscriberDetails.Add(new
            {
                subscriber.ApiKey,
                subscriber.RetryIntervals,
                subscriber.MaxParallelism,
                subscriber.Name,
                subscriber.NumberOfWorkers,
                subscriber.Types,
                subscriber.Url,
                MessageCount = activeMessageCount,
                DeadLetterCount = deadLetterMessageCount
            });
        }

        await ctx.Response.WriteAsJsonAsync(subscriberDetails);
    }

    static async Task GetSubscriber(Database database, HttpContext ctx)
    {
        var name = ctx.Request.RouteValues["name"]?.ToString();
        if (name is not null)
        {
            var sqlClient = CreateSqlClient(database);
            var subscriber = await sqlClient.ReadSubscriber(name);
            if (subscriber is not null)
            {
                var (activeMessageCount, deadLetterMessageCount) = await sqlClient.GetMessageCount(name);
                var subscriberDetails = new
                {
                    subscriber.ApiKey,
                    subscriber.RetryIntervals,
                    subscriber.MaxParallelism,
                    subscriber.Name,
                    subscriber.NumberOfWorkers,
                    subscriber.Types,
                    subscriber.Url,
                    MessageCount = activeMessageCount,
                    DeadLetterCount = deadLetterMessageCount
                };
                await ctx.Response.WriteAsJsonAsync(subscriberDetails);
            }
            else
            {
                ctx.Response.StatusCode = 404;
            }
        }
        else
        {
            ctx.Response.StatusCode = 404;
        }
    }

    static async Task PublishMessage(PubSub.Publish publish, HttpContext ctx)
    {
        try
        {
            var json = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
            var message = Json.Deserialize<Message>(json) ?? throw new JsonException();
            await publish(message.Type, message);
        }
        catch (JsonException)
        {
            ctx.Response.StatusCode = 400;
        }
    }

    static async Task<bool> TryCreateSubscriber(Database database, Subscriber subscriber)
    {
        if (subscriber.Name is null || !Uri.IsWellFormedUriString(subscriber.Url, UriKind.Absolute))
            throw new JsonException();
        var created = await PubSub.CreateSubscriber(database, subscriber);
        if (created) await CreateSqlClient(database).CreateSubscriber(subscriber);
        return created;
    }

    static bool ValidateSubscriber(Subscriber subscriber)
    {
        return Regex.IsMatch(subscriber.Name, "^[a-z0-9]{1,128}$")
               && subscriber.Types.Length > 0
               && Uri.IsWellFormedUriString(subscriber.Url, UriKind.Absolute);
    }

    static async Task CreateSubscriber(Database database, HttpContext ctx)
    {
        try
        {
            var json = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
            var subscriber = Json.Deserialize<Subscriber>(json) ?? throw new JsonException();
            if (ValidateSubscriber(subscriber))
            {
                if (!await TryCreateSubscriber(database, subscriber)) ctx.Response.StatusCode = 409;
            }
            else
            {
                throw new JsonException();
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

    static async Task DeleteSubscriber(Database database, HttpContext ctx)
    {
        var name = ctx.Request.RouteValues["name"]?.ToString();
        if (name is not null)
        {
            await PubSub.DeleteSubscriber(name);
            var sqlClient = CreateSqlClient(database);
            await sqlClient.DeleteSubscriber(name);
        }
    }

    public static IWebHostBuilder UseEventSub(this IWebHostBuilder builder, Database database, string apiKey)
    {
        var publish = PubSub.CreatePublisher(database);
        var sqlClient = CreateSqlClient(database);
        sqlClient.CreateSubscribersTable().Wait();
        var subscribers = sqlClient.ReadSubscribers().Result;
        builder.ConfigureServices(services => services.AddRouting());
        builder.Configure(app =>
        {
            foreach (var subscriber in subscribers) TryCreateSubscriber(database, subscriber).Wait();
            app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
            {
                ctx.Response.StatusCode = 500;
                var exceptionHandlerPathFeature = ctx.Features.Get<IExceptionHandlerPathFeature>();
                await ctx.Response.WriteAsJsonAsync(new
                {
                    exceptionHandlerPathFeature!.Error.Message, exceptionHandlerPathFeature.Error.StackTrace
                });
            }));
            app.Use(async (ctx, next) =>
            {
                var apiKeyHeader = ctx.Request.Headers["X-API-KEY"].ToString();
                var apiKeyQuery = ctx.Request.Query["apikey"].ToString();
                var providedApiKey = string.IsNullOrEmpty(apiKeyHeader) ? apiKeyQuery : apiKeyHeader;
                var authorized = !string.IsNullOrEmpty(apiKey) && apiKey == providedApiKey;
                if (authorized)
                    await next();
                else
                    ctx.Response.StatusCode = 401;
            });

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapPost("/", ctx => PublishMessage(publish, ctx));
                endpoints.MapPost("/subscribers", ctx => CreateSubscriber(database, ctx));
                endpoints.MapDelete("/subscribers/{name:required}", ctx => DeleteSubscriber(database, ctx));
                endpoints.MapGet("/subscribers/{name:required}", ctx => GetSubscriber(database, ctx));
                endpoints.MapGet("/subscribers", ctx => GetSubscribers(database, ctx));
            });
        });
        return builder;
    }
}