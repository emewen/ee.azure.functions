using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace azure.functions
{
    public class StockTicker
    {
        private readonly ILogger<StockTicker> _logger;

        public StockTicker(ILogger<StockTicker> logger)
        {
            _logger = logger;
        }

        [Function(nameof(StockTicker))]
        public async Task Run(
            [ServiceBusTrigger("graphQueue", Connection = "ServiceBusQueue")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            _logger.LogInformation("Message ID: {id}", message.MessageId);
            _logger.LogInformation("Message Body: {body}", message.Body);
            _logger.LogInformation("Message Content-Type: {contentType}", message.ContentType);

            // Complete the message
            await messageActions.CompleteMessageAsync(message);
        }
    }
}
