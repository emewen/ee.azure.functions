using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using azure.functions;
using System;
using Microsoft.Extensions.Configuration;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
    })
    .ConfigureAppConfiguration(builder =>
    {
        builder.AddJsonFile("prod.settings.json", optional: true, reloadOnChange: true);
    })
    .Build();

host.Run();
