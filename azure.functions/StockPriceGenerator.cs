using System.Text;
using azure.functions.Poco;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace azure.functions
{
    public class StockPriceGenerator
    {
        private readonly ILogger _logger;
        private readonly TimeProvider _timeProvider;
        private readonly IConfiguration _configuration;
        private readonly List<StockRules> _stockRules;

        public StockPriceGenerator(ILogger<StockPriceGenerator> logger, TimeProvider timeProvider, IConfiguration configuration, List<StockRules> stockRules)
        {
            _logger = logger;
            _timeProvider = timeProvider;
            _configuration = configuration;
            _stockRules = stockRules;
        }

        [Function("StockPriceGenerator")]
        [CosmosDBOutput(databaseName: "%CosmosDb%", containerName: "%CosmosContainerOut%", Connection = "CosmosDBConnection", CreateIfNotExists = true, PartitionKey = "/id")]

        public async Task<object> Run([TimerTrigger("*/10 * * * * *")] TimerInfo myTimer,
            [CosmosDBInput(databaseName: "%CosmosDb%", containerName: "%CosmosContainerIn%", Connection = "CosmosDBConnection", SqlQuery = "Select * From c")] List<Stock> stockList)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {_timeProvider.GetUtcNow()}");

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }

            List<Stock> stocksWithUpdatedPrice = updateStockPrices(stockList);

            if (!await sendToStockLogicApp(stocksWithUpdatedPrice))
            {
                _logger.LogError($"Failure sending to Logic App");
            }

            return stocksWithUpdatedPrice;
        }

        private async Task<bool> sendToStockLogicApp(List<Stock> stocksWithUpdatedPrice)
        {
            try
            {
                using (var httpClient = new HttpClient()) // ultimately httpClient should be dependency injected...or at least httpClientFactory and httpClientFactory.Create in constructor
                {
                    var response = await httpClient.PostAsync(_configuration["StockLogicAppEndpoint"],
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
            _logger.LogInformation(String.Format("{0}: {1}", symbol, new_price.ToString()));
            return new_price;
        }

        private List<Stock> updateStockPrices(List<Stock> stocks)
        {
            return stocks.Select(s => {
                Decimal minPrice = _stockRules.FirstOrDefault(sr => sr.symbol == s.symbol).minPrice;
                Decimal maxPrice = _stockRules.FirstOrDefault(sr => sr.symbol == s.symbol).maxPrice;
                s.price = Math.Round(generateStockData(s.symbol, s.price, minPrice, maxPrice), 2);
                s.symbol = s.symbol;
                s.timestamp = _timeProvider.GetUtcNow().ToString("yyyy-MM-ddTH:mm:ss.fffZ");
                s.range = minPrice.ToString() + "-" + maxPrice.ToString();
                return s;
            }).ToList();
        }

        //Direct Query of table (instead of CosmosDBInput binding):
        private async Task<List<Stock>> QueryItemsAsync()
        {
            CosmosClient cosmosClient = new CosmosClient(_configuration["CosmosDBConnection"]);
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
                        Decimal minPrice = _stockRules.FirstOrDefault(sr => sr.symbol == item.symbol).minPrice;
                        Decimal maxPrice = _stockRules.FirstOrDefault(sr => sr.symbol == item.symbol).maxPrice;
                        stocks.Add(new Stock() { id = item.id, symbol = item.symbol, price = Math.Round(generateStockData(item.symbol, item.price, minPrice, maxPrice), 2) });
                    }
                }
            }
            return stocks;
        }
    }
}
