using CosmosFeedTestsConsole.Config;
using Microsoft.Azure.Cosmos;
using System;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CosmosFeedTestsConsole
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("You have to call this exec with a parameter pointing to the config file");
            }
            else
            {
                var configPath = args[0];
                var configContent = File.ReadAllText(configPath);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                var config = deserializer.Deserialize<RootConfiguration>(configContent);
                var cosmosClient = new CosmosClient(config.Endpoint!, config.AccessKey!);

                Console.WriteLine("Hello World!");
            }
        }
    }
}