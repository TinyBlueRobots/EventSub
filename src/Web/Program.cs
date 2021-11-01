using System;
using EventSub;
using Microsoft.Extensions.Hosting;

namespace Web
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var apiKey = Environment.GetEnvironmentVariable("APIKEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("Empty EnvVar APIKEY");
            }

            var connectionString = Environment.GetEnvironmentVariable("CONNECTIONSTRING");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Empty EnvVar CONNECTIONSTRING");
            }

            var database =
                Environment.GetEnvironmentVariable("DATABASE") switch
                {
                    nameof(Database.MySql) => Database.MySql(connectionString),
                    nameof(Database.SqlServer) => Database.SqlServer(connectionString),
                    nameof(Database.PostgreSql) => Database.PostgreSql(connectionString),
                    _ => throw new ArgumentException("Unknown EnvVar DATABASE")
                };
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => webBuilder.UseEventSub(database, apiKey))
                .Build()
                .Run();
        }
    }
}