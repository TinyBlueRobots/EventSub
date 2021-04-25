using System;
using EventSub;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var url = $"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT")}";
            var apiKey = Environment.GetEnvironmentVariable("APIKEY");
            if (String.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("Empty EnvVar APIKEY");
            }

            var connectionString = Environment.GetEnvironmentVariable("CONNECTIONSTRING");
            Database database;
            switch (Environment.GetEnvironmentVariable("DATABASE"))
            {
                case nameof(Database.MySql):
                    database = Database.MySql(connectionString);
                    break;
                case nameof(Database.SqlServer):
                    database = Database.SqlServer(connectionString);
                    break;
                case nameof(Database.PostgreSql):
                    database = Database.PostgreSql(connectionString);
                    break;
                default:
                    throw new ArgumentException("Unknown EnvVar DATABASE");
            }
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseEventSub(database, apiKey);
                    webBuilder.UseUrls(url);
                })
                .Build()
                .Run();
        }
    }
}
