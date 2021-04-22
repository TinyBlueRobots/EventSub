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
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseEventSub(new MySqlConfig(Environment.GetEnvironmentVariable("CONNECTIONSTRING")), Environment.GetEnvironmentVariable("APIKEY"));
                    webBuilder.UseUrls(url);
                })
                .Build()
                .Run();
        }
    }
}
