using CosmosFeedTestsConsole.Config;
using Microsoft.Azure.Cosmos;
using System;
using System.Net;
using System.Text.Json;
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

                //await WriteOneItemAsync(container);
                //await WriteManyItemsAsync(container);
                //await WriteManyItemsInSamePartitionAsync(container);
                await ReadContainerAsync(container);
            }
        }

        private static async Task ReadContainerAsync(Container container)
        {
            var iteratorForTheEntireContainer = container.GetChangeFeedStreamIterator(
                ChangeFeedStartFrom.Beginning(),
                ChangeFeedMode.Incremental);
            var response = await iteratorForTheEntireContainer.ReadNextAsync();

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                Console.WriteLine($"No new changes");
            }
            else
            {
                using (var reader = new StreamReader(response.Content))
                {
                    var text = await reader.ReadToEndAsync();
                    var batch = JsonSerializer.Deserialize<DocumentBatch>(text);
                    var rus = response.Headers.RequestCharge;
                    var avgRus = rus / batch!._count;

                    Console.WriteLine($"{batch!._count} documents read, {rus} total RUs, {avgRus} Average RUs");
                }
            }
        }

        private static async Task WriteManyItemsInSamePartitionAsync(Container container)
        {
            var partition = new TelemetryItem().Part;
            var items = Enumerable.Range(0, 25)
                .Select(i => new TelemetryItem { Part = partition });
            var batch = container.CreateTransactionalBatch(new PartitionKey(partition));

            foreach (var item in items)
            {
                batch = batch.CreateItem(item);
            }

            var batchResponse = await batch.ExecuteAsync();

            if (!batchResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException("Failed");
            }

            var rus = batchResponse.Headers.RequestCharge;
            var avgRus = rus / items.Count();

            Console.WriteLine($"Average RUs:  {avgRus}");
        }

        private static async Task WriteManyItemsAsync(Container container)
        {
            var items = Enumerable.Range(0, 25)
                .Select(i => new TelemetryItem());
            var createTasks = items
                .Select(i => container.CreateItemAsync(i))
                .ToArray();

            await Task.WhenAll(createTasks);

            var avgRus = createTasks
                .Select(t => t.Result.Headers.RequestCharge)
                .Average();

            Console.WriteLine($"Average RUs:  {avgRus}");
        }

        private static async Task WriteOneItemAsync(Container container)
        {
            var item = new TelemetryItem();
            var itemResponse = await container.CreateItemAsync(item);
            var ru = itemResponse.Headers.RequestCharge;

            Console.WriteLine($"RU:  {ru}");
        }

        private static async Task<RootConfiguration> LoadConfigAsync(string[] args)
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