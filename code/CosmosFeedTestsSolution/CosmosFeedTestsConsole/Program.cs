using CosmosFeedTestsConsole.Config;
using Microsoft.Azure.Cosmos;
using System;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CosmosFeedTestsConsole
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("You have to call this exec with a parameter pointing to the config file");
            }
            else
            {
                var config = await LoadConfigAsync(args);
                var cosmosClient = new CosmosClient(config.Endpoint!, config.AccessKey!);
                var container = cosmosClient.GetDatabase(config.Database!).GetContainer(config.Container);
                var item = new TelemetryItem();
                var r = await container.CreateItemAsync(item);

                Console.WriteLine("Hello World!");
            }
        }

        private static async Task<RootConfiguration> LoadConfigAsync (string[] args)
        {
            var configPath = args[0];
            var configContent = await File.ReadAllTextAsync(configPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            var config = deserializer.Deserialize<RootConfiguration>(configContent);

            return config;
        }
    }
}