using System;
using System.Collections.Immutable;
using System.Configuration;
using System.Text;
using System.Text.Json;
using azure.functions.Poco;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace azure.functions
{
    public class StockPriceGenerator
    {
        private readonly ILogger _logger;
        private readonly TimeProvider _timeProvider;
        private readonly IConfiguration _configuration;

        public StockPriceGenerator(ILogger<StockPriceGenerator> logger, TimeProvider timeProvider, IConfiguration configuration)
        {
            _logger = logger;
            _timeProvider = timeProvider;
            _configuration = configuration;
        }

        [Function("StockPriceGenerator")]
        [CosmosDBOutput("stock-price-database-89680", "stock-price-container-89680", Connection = "CosmosDBConnection", CreateIfNotExists = true, PartitionKey = "/id")]

        public async Task<object> Run([TimerTrigger("*/10 * * * * *")] TimerInfo myTimer,
            [CosmosDBInput("stock-price-database-89680", "stock-price-container-89680", Connection = "CosmosDBConnection", SqlQuery = "Select * From c")] List<Stock> stockList)
        {
            //_logger.LogInformation($"C# Timer trigger function executed at: {_timeProvider.GetUtcNow()}");

            if (myTimer.ScheduleStatus is not null)
            {
                //_logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }

            List<Stock> stocksWithUpdatedPrice = updateStockPrices(stockList);

            if (!await sendToStockLogicApp(stocksWithUpdatedPrice))
            {
                //alert error to "Error Queue" in service bus or log
            }

            return stocksWithUpdatedPrice;
        }

        private async Task<bool> sendToStockLogicApp(List<Stock> stocksWithUpdatedPrice)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.PostAsync("https://prod-13.northcentralus.logic.azure.com:443/workflows/6072695beb794ab4b0fdadeb01e7a489/triggers/When_a_HTTP_request_is_received/paths/invoke?api-version=2016-10-01&sp=%2Ftriggers%2FWhen_a_HTTP_request_is_received%2Frun&sv=1.0&sig=nlzltlq52uNyZPpaWvnBvokfwV9Vc9L6_53sjPBCBFM",
                        new StringContent(
                            JsonConvert.SerializeObject(
                                stocksWithUpdatedPrice,
                                new JsonSerializerSettings
                                {
                                    DateFormatString = "yyyy-MM-ddTH:mm:ss.fffZ",
                                    DateTimeZoneHandling = DateTimeZoneHandling.Utc
                                }),
                            Encoding.UTF8,
                            "application/json"));
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError($"Unsuccessful call to Logic App.  Status Code: {response.StatusCode.ToString()}");
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception encountered during HTTP call to logic app: {ex}");
                return false;
            }
        }

        private List<Stock> updateStockPrices(List<Stock> stocks)
        {
            List<StockRules> stockRules = _configuration.GetSection("stockRules").Get<List<StockRules>>();
            return stocks.Select(s => {
                Decimal minPrice = stockRules.FirstOrDefault(sr => sr.symbol == s.symbol).minPrice;
                Decimal maxPrice = stockRules.FirstOrDefault(sr => sr.symbol == s.symbol).maxPrice;
                s.price = Math.Round(generateStockData(s.symbol, s.price, minPrice, maxPrice), 2);
                s.symbol = s.symbol;
                s.timestamp = _timeProvider.GetUtcNow().ToString("yyyy-MM-ddTH:mm:ss.fffZ");
                s.range = minPrice.ToString() + "-" + maxPrice.ToString();
                return s;
            }).ToList();
        }
        private decimal generateStockData(string symbol, decimal old_price, decimal minPrice, decimal maxPrice)
        {
            Random rnd = new Random();
            var random = rnd.NextDouble() - 0.5; // generate number, 0 <= x < 1.0
            var volatility = rnd.NextDouble() * 5 + 2;
            var change_percent = volatility * random;
            var change_amount = old_price * (decimal)(change_percent / 100);
            var new_price = old_price + change_amount;
            if (new_price < minPrice)
            {
                new_price += Math.Abs(change_amount) * 2;
            }
            else if (new_price > maxPrice)
            {
                new_price -= Math.Abs(change_amount) * 2;
            }
            Console.WriteLine(String.Format("{0}: {1}", symbol, new_price.ToString()));
            return new_price;
        }

        //Direct Query of table (instead of CosmosDBInput binding):
        private async Task<List<Stock>> QueryItemsAsync(List<StockRules> stockRules)
        {
            CosmosClient cosmosClient = new CosmosClient("AccountEndpoint=https://cosmosdb-default-account.documents.azure.com:443/;AccountKey=P17fZ77OjGhvjRyuMHXh6R98wXwp5Co8SLnHt6eb6vaufxsL2HipXVaj6jwJzUfRTtx6G5172XnKACDbwbRqMw==;");
            var sqlQueryText = "SELECT * FROM c";
            Container container = cosmosClient.GetContainer("StocksDB", "StocksContainer");
            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
            List<Stock> stocks = new List<Stock>();
            using (FeedIterator<Stock> setIterator = container.GetItemLinqQueryable<Stock>()
                     //.Where(b => b.StockId == "MSFT")
                     .ToFeedIterator<Stock>())
            {
                //Asynchronous query execution
                while (setIterator.HasMoreResults)
                {
                    foreach (var item in await setIterator.ReadNextAsync())
                    {
                        Decimal minPrice = stockRules.FirstOrDefault(sr => sr.symbol == item.symbol).minPrice;
                        Decimal maxPrice = stockRules.FirstOrDefault(sr => sr.symbol == item.symbol).maxPrice;
                        stocks.Add(new Stock() { id = item.id, symbol = item.symbol, price = Math.Round(generateStockData(item.symbol, item.price, minPrice, maxPrice), 2) });
                    }
                }
            }
            return stocks;
        }
    }
}
