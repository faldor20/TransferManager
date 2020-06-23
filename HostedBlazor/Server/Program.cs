using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HostedBlazor.Server.Controllers;
public class Config
{
    public string transferServerUrl{get;set;}
}
namespace HostedBlazor.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var jsonFile= File.ReadAllText("./Config.json");
            var url= System.Text.Json.JsonSerializer.Deserialize<Config>(jsonFile);
            Console.WriteLine("TransferServerUrl is:"+url.transferServerUrl);
            WeatherForecastController.transferServerUrl=url.transferServerUrl;
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {   
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls("http://*:5050");
                });
    }
}
