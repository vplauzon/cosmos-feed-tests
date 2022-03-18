using CosmosFeedTestsConsole.Config;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Immutable;
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
            ServicePointManager.DefaultConnectionLimit = 25;

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
                //await ReadContainerAsync(container);
                //await ReadByPartitionAsync(container);
                await RunScaleTestsAsync(config, container);
            }
        }

        private static async Task RunScaleTestsAsync(RootConfiguration config, Container container)
        {
            var sendTask = RunSendScaleTestAsync(config, container);
            var receiveTask = RunReceiveScaleTestAsync(config, container);

            await Task.WhenAll(sendTask, receiveTask);
        }

        private static async Task RunSendScaleTestAsync(RootConfiguration config, Container container)
        {
            if (config.SendPerSecond > 0)
            {
                var totalItemsWritten = (long)0;

                Console.WriteLine($"Sending documents to Cosmos DB:  {config.SendPerSecond} "
                    + $"items per second, reporting every {config.ReportFrequency} seconds");

                //  Warm up
                await container.CreateItemAsync(new TelemetryItem());
                ++totalItemsWritten;
                while (true)
                {
                    var cycleRus = (double)0;
                    var cycleItemsWritten = (long)0;

                    for (int second = 0; second != config.ReportFrequency; ++second)
                    {
                        var secondStart = DateTime.Now;
                        var itemTasks = Enumerable.Range(0, config.SendPerSecond)
                            .Select(i => new TelemetryItem())
                            .Select(item => container.CreateItemAsync(item))
                            .ToImmutableArray();

                        await Task.WhenAll(itemTasks);

                        var rus = itemTasks
                            .Select(t => t.Result.Headers.RequestCharge)
                            .Sum();

                        cycleRus += rus;
                        cycleItemsWritten += itemTasks.Length;
                        totalItemsWritten += itemTasks.Length;

                        var elapsed = DateTime.Now.Subtract(secondStart);
                        var pauseTime = TimeSpan.FromSeconds(1).Subtract(elapsed);

                        if (pauseTime < TimeSpan.Zero)
                        {
                            throw new InvalidOperationException("Can't send documents fast enough!");
                        }
                        await Task.Delay(pauseTime);
                    }

                    Console.WriteLine($"Sent {cycleItemsWritten} documents in {config.ReportFrequency} seconds ; "
                        + $"total {cycleRus} RUs => {cycleRus / config.ReportFrequency} RUs / s, "
                        + $"{cycleRus / cycleItemsWritten} RUs / document");
                }
            }
        }

        private static async Task RunReceiveScaleTestAsync(RootConfiguration config, Container container)
        {
            if (config.ReceivePerSecond > 0)
            {
                var totalItemsRead = (long)0;
                var ranges = await container.GetFeedRangesAsync();
                var partitionIterators = ranges
                    .Select(r => container.GetChangeFeedStreamIterator(
                        ChangeFeedStartFrom.Beginning(r),
                        ChangeFeedMode.Incremental))
                    .ToImmutableArray();

                Console.WriteLine($"Receiving documents to Cosmos DB:  {config.ReceivePerSecond} "
                    + $"items per second, reporting every {config.ReportFrequency} seconds "
                    + $"{ranges.Count} physical partitions");

                //  Warm up
                await Task.WhenAll(partitionIterators.Select(pi => pi.ReadNextAsync()));
                while (true)
                {
                    var cycleRus = (double)0;
                    var cycleItemsRead = (long)0;

                    for (int second = 0; second != config.ReportFrequency; ++second)
                    {
                        var secondStart = DateTime.Now;
                        var partitionTasks = partitionIterators
                            .Select(pi => pi.ReadNextAsync())
                            .ToImmutableArray();

                        await Task.WhenAll(partitionTasks);

                        var rus = partitionTasks
                            .Select(t => t.Result.Headers.RequestCharge)
                            .Sum();
                        var documentCountTasks = partitionTasks
                            .Select(t => GetDocumentCountAsync(t.Result))
                            .ToImmutableArray();

                        await Task.WhenAll(documentCountTasks);

                        var documentCount = documentCountTasks
                            .Select(t => t.Result)
                            .Sum();

                        cycleRus += rus;
                        cycleItemsRead += documentCount;
                        totalItemsRead += documentCount;

                        var elapsed = DateTime.Now.Subtract(secondStart);
                        var pauseTime = TimeSpan.FromSeconds(1).Subtract(elapsed);

                        if (pauseTime < TimeSpan.Zero)
                        {
                            throw new InvalidOperationException("Can't receive documents fast enough!");
                        }
                        await Task.Delay(pauseTime);
                    }

                    Console.WriteLine($"Received {cycleItemsRead} documents in {config.ReportFrequency} seconds ; "
                        + $"total {cycleRus} RUs => {cycleRus / config.ReportFrequency} RUs / s, "
                        + $"{cycleRus / cycleItemsRead} RUs / document");
                }
            }
        }

        private static async Task<int> GetDocumentCountAsync(ResponseMessage response)
        {
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return 0;
            }
            else
            {
                using (var reader = new StreamReader(response.Content))
                {
                    var text = await reader.ReadToEndAsync();
                    var batch = JsonSerializer.Deserialize<DocumentBatch>(text);

                    return batch!._count;
                }
            }
        }

        private static async Task ReadByPartitionAsync(Container container)
        {
            var ranges = await container.GetFeedRangesAsync();

            Console.WriteLine($"{ranges.Count} physical partitions");

            foreach (var range in ranges)
            {
                var partitionIterator = container.GetChangeFeedStreamIterator(
                    ChangeFeedStartFrom.Beginning(range),
                    ChangeFeedMode.Incremental);
                var response = await partitionIterator.ReadNextAsync();

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