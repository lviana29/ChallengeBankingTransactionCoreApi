using System.Text.Json;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Infrastructure.Workers {
    public class OutboxRelayHostedServiceTests {
        private readonly Mock<IOutboxRepository> _outboxRepoMock;
        private readonly Mock<IMessagePublisherPort> _publisherMock;
        private readonly Mock<IAppLogger<OutboxRelayHostedService>> _loggerMock;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;

        public OutboxRelayHostedServiceTests() {
            _outboxRepoMock = new Mock<IOutboxRepository>();
            _publisherMock = new Mock<IMessagePublisherPort>();
            _loggerMock = new Mock<IAppLogger<OutboxRelayHostedService>>();

            var services = new ServiceCollection();
            services.AddScoped(_ => _outboxRepoMock.Object);
            services.AddScoped(_ => _publisherMock.Object);
            var provider = services.BuildServiceProvider();
            _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> {
                    ["Outbox:PollingIntervalMs"] = "20",
                    ["Outbox:BatchSize"] = "25",
                    ["Outbox:MaxRetries"] = "5"
                })
                .Build();
        }

        [Fact]
        public async Task ProcessPendingBatchAsync_ShouldPublishAndMarkAsProcessed_WhenBrokerIsOnline() {
            // Arrange
            var txEvent = new TransactionCreatedEventEntity(
                TransactionId: Guid.NewGuid(),
                AccountId: Guid.NewGuid(),
                AccountNumber: "12345-6",
                Amount: 100m,
                BalanceAfter: 1100m,
                TransactionType: "Crédito em conta",
                IdempotencyKey: Guid.NewGuid(),
                CreatedAt: DateTime.UtcNow
            );

            var msg = new OutboxMessageEntity {
                Id = Guid.NewGuid(),
                EventType = nameof(TransactionCreatedEventEntity),
                Payload = JsonSerializer.Serialize(txEvent),
                CreatedAt = DateTime.UtcNow
            };

            _outboxRepoMock
                .Setup(r => r.GetPendingMessagesAsync(25, 5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OutboxMessageEntity> { msg });

            var worker = new OutboxRelayHostedService(_scopeFactory, _configuration, _loggerMock.Object);

            // Act
            var processed = await worker.ProcessPendingBatchAsync();

            // Assert
            Assert.Equal(1, processed);
            _publisherMock.Verify(p => p.PublishTransactionCreatedAsync(It.Is<TransactionCreatedEventEntity>(e =>
                e.TransactionId == txEvent.TransactionId &&
                e.AccountNumber == "12345-6"
            )), Times.Once);
            _outboxRepoMock.Verify(r => r.MarkAsProcessedAsync(msg.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
            _outboxRepoMock.Verify(r => r.MarkAsFailedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessPendingBatchAsync_ShouldMarkAsFailedAndLogWarning_WhenBrokerThrowsException() {
            // Arrange
            var txEvent = new TransactionCreatedEventEntity(
                TransactionId: Guid.NewGuid(),
                AccountId: Guid.NewGuid(),
                AccountNumber: "12345-6",
                Amount: 50m,
                BalanceAfter: 950m,
                TransactionType: "Débito em conta",
                IdempotencyKey: Guid.NewGuid(),
                CreatedAt: DateTime.UtcNow
            );

            var msg = new OutboxMessageEntity {
                Id = Guid.NewGuid(),
                EventType = nameof(TransactionCreatedEventEntity),
                Payload = JsonSerializer.Serialize(txEvent),
                CreatedAt = DateTime.UtcNow,
                RetryCount = 1
            };

            _outboxRepoMock
                .Setup(r => r.GetPendingMessagesAsync(25, 5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OutboxMessageEntity> { msg });

            _publisherMock
                .Setup(p => p.PublishTransactionCreatedAsync(It.IsAny<TransactionCreatedEventEntity>()))
                .ThrowsAsync(new InvalidOperationException("RabbitMQ Broker unreachable"));

            var worker = new OutboxRelayHostedService(_scopeFactory, _configuration, _loggerMock.Object);

            // Act
            var processed = await worker.ProcessPendingBatchAsync();

            // Assert
            Assert.Equal(0, processed);
            _outboxRepoMock.Verify(r => r.MarkAsFailedAsync(msg.Id, "RabbitMQ Broker unreachable", It.IsAny<CancellationToken>()), Times.Once);
            _outboxRepoMock.Verify(r => r.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessPendingBatchAsync_ShouldMarkAsFailed_WhenPayloadDeserializesToNull() {
            // Arrange
            var msg = new OutboxMessageEntity {
                Id = Guid.NewGuid(),
                EventType = nameof(TransactionCreatedEventEntity),
                Payload = "null",
                CreatedAt = DateTime.UtcNow
            };

            _outboxRepoMock
                .Setup(r => r.GetPendingMessagesAsync(25, 5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OutboxMessageEntity> { msg });

            var worker = new OutboxRelayHostedService(_scopeFactory, _configuration, _loggerMock.Object);

            // Act
            var processed = await worker.ProcessPendingBatchAsync();

            // Assert
            Assert.Equal(0, processed);
            _outboxRepoMock.Verify(r => r.MarkAsFailedAsync(msg.Id, It.Is<string>(s => s.Contains("deserialize")), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StartAndStopAsync_ShouldRunBackgroundLoopCleanly() {
            // Arrange
            _outboxRepoMock
                .Setup(r => r.GetPendingMessagesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OutboxMessageEntity>());

            var worker = new OutboxRelayHostedService(_scopeFactory, _configuration, _loggerMock.Object);
            using var cts = new CancellationTokenSource();

            // Act
            await worker.StartAsync(cts.Token);
            await Task.Delay(35);
            await worker.StopAsync(CancellationToken.None);

            // Assert
            _outboxRepoMock.Verify(r => r.GetPendingMessagesAsync(25, 5, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }
    }
}
