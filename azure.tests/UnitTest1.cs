using azure.functions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace azure.tests
{
    public class Tests
    {
        private Mock<ILogger<StockPriceGenerator>> mockLogger = new Mock<ILogger<StockPriceGenerator>>();
        private TimerInfo mockTimerInfo;
        Mock<TimeProvider> mockTimeProvider = new Mock<TimeProvider>();

        private StockPriceGenerator? stockPriceGenerator;

        [SetUp]
        public void Setup()
        {
            mockTimerInfo = new TimerInfo();
            mockTimerInfo.IsPastDue = false;
            mockTimerInfo.ScheduleStatus = new ScheduleStatus();
            mockTimerInfo.ScheduleStatus.Last = DateTime.Parse("2024-11-10T10:00:05+00:00");
            mockTimerInfo.ScheduleStatus.LastUpdated = DateTime.Parse("2024-11-10T10:00:06+00:00");
            mockTimerInfo.ScheduleStatus.Next = DateTime.Parse("2024-11-10T10:00:10+00:00");
            mockTimeProvider.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(2024, 10, 10, 0, 0, 0, TimeSpan.Zero));
        }

        [Test]
        public void StockPriceGenerator_run()
        {
            stockPriceGenerator = new StockPriceGenerator(mockLogger.Object, mockTimeProvider.Object);

            stockPriceGenerator.Run(mockTimerInfo);

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => string.Equals("C# Timer trigger function executed at: 10/10/2024 12:00:00 AM +00:00", o.ToString(), StringComparison.InvariantCultureIgnoreCase)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => string.Equals("Next timer schedule at: 10/11/2024 8:00:10 PM", o.ToString(), StringComparison.InvariantCultureIgnoreCase)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            Assert.Pass();
        }

        [Test]
        public void StockPriceGenerator_run100Times()
        {
            stockPriceGenerator = new StockPriceGenerator(mockLogger.Object, mockTimeProvider.Object);

            for (int i = 0; i < 100; i++)
            {
                stockPriceGenerator.Run(mockTimerInfo);
            }

            Assert.Pass();
        }
    }
}