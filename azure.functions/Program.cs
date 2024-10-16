using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using azure.functions;
using System;
using Microsoft.Extensions.Configuration;
using azure.functions.Poco;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        var stockRules = new List<StockRules>();
        stockRules.Add(new StockRules() { symbol = "MSFT", minPrice = 250, maxPrice = 500 });
        stockRules.Add(new StockRules() { symbol = "GOOG", minPrice = 100, maxPrice = 250 });
        stockRules.Add(new StockRules() { symbol = "AMNZ", minPrice = 50, maxPrice = 250 });
        stockRules.Add(new StockRules() { symbol = "TSLA", minPrice = 75, maxPrice = 350 });
        stockRules.Add(new StockRules() { symbol = "META", minPrice = 400, maxPrice = 750 });
        stockRules.Add(new StockRules() { symbol = "APPL", minPrice = 400, maxPrice = 750 });
        stockRules.Add(new StockRules() { symbol = "NVDA", minPrice = 50, maxPrice = 200 });
        services.AddSingleton(stockRules);
    })
    //.ConfigureAppConfiguration(builder =>
    //{
    //    builder.SetBasePath(Directory.GetCurrentDirectory())
    //    .AddJsonFile("prod.settings.json", optional: true, reloadOnChange: true);
    //})
    .Build();

host.Run();
