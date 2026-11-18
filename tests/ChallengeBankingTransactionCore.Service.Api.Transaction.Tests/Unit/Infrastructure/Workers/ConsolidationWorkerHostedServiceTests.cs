using System.Text.Json;
using ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.ProcessTransactionEventUseCase;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Adapter;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RabbitMQ.Client;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Infrastructure.Workers {
    public class ConsolidationWorkerHostedServiceTests {
        private readonly Mock<IProcessTransactionEventUseCase> _useCaseMock;
        private readonly Mock<IAppLogger<ConsolidationWorkerHostedService>> _loggerMock;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;

        public ConsolidationWorkerHostedServiceTests() {
            _useCaseMock = new Mock<IProcessTransactionEventUseCase>();
            _loggerMock = new Mock<IAppLogger<ConsolidationWorkerHostedService>>();

            var services = new ServiceCollection();
            services.AddScoped(_ => _useCaseMock.Object);
            var provider = services.BuildServiceProvider();
            _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> {
                    ["Hosting:EnableConsolidationWorker"] = "false",
                    ["RabbitMQ:ReconnectDelayMs"] = "20"
                })
                .Build();
        }

        [Fact]
        public async Task HandleMessagePayloadAsync_ShouldExecuteUseCase_WhenPayloadIsValidJson() {
            // Arrange
            var txEvent = new TransactionCreatedEventEntity(
                TransactionId: Guid.NewGuid(),
                AccountId: Guid.NewGuid(),
                AccountNumber: "12345-6",
                Amount: 150m,
                BalanceAfter: 1150m,
                TransactionType: "Crédito em conta",
                IdempotencyKey: Guid.NewGuid(),
                CreatedAt: DateTime.UtcNow
            );

            _useCaseMock
                .Setup(u => u.ExecuteAsync(It.IsAny<TransactionCreatedEventEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var worker = new ConsolidationWorkerHostedService(_scopeFactory, _configuration, _loggerMock.Object);
            var json = JsonSerializer.Serialize(txEvent);

            // Act
            var result = await worker.HandleMessagePayloadAsync(json);

            // Assert
            Assert.True(result);
            _useCaseMock.Verify(u => u.ExecuteAsync(It.Is<TransactionCreatedEventEntity>(e => e.TransactionId == txEvent.TransactionId), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleMessagePayloadAsync_ShouldThrowJsonException_WhenPayloadIsCorruptedJson() {
            // Arrange
            var worker = new ConsolidationWorkerHostedService(_scopeFactory, _configuration, _loggerMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<JsonException>(() => worker.HandleMessagePayloadAsync("{invalid-json-payload"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("null")]
        public async Task HandleMessagePayloadAsync_ShouldThrowConsolidateStatementError_WhenPayloadIsEmptyOrNullLiteral(string payload) {
            // Arrange
            var worker = new ConsolidationWorkerHostedService(_scopeFactory, _configuration, _loggerMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ConsolidateStatementError>(() => worker.HandleMessagePayloadAsync(payload));
        }

        [Fact]
        public async Task ProcessDeliveryAsync_ShouldAckMessage_WhenProcessingSucceeds() {
            // Arrange
            var channelMock = new Mock<IChannel>();
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

            _useCaseMock
                .Setup(u => u.ExecuteAsync(It.IsAny<TransactionCreatedEventEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var worker = new ConsolidationWorkerHostedService(_scopeFactory, _configuration, _loggerMock.Object);
            var json = JsonSerializer.Serialize(txEvent);

            // Act
            await worker.ProcessDeliveryAsync(channelMock.Object, 42UL, json);

            // Assert
            channelMock.Verify(c => c.BasicAckAsync(42UL, false, It.IsAny<CancellationToken>()), Times.Once);
            channelMock.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessDeliveryAsync_ShouldNackWithoutRequeueToDLQ_WhenPoisonMessageOccurs() {
            // Arrange
            var channelMock = new Mock<IChannel>();
            var worker = new ConsolidationWorkerHostedService(_scopeFactory, _configuration, _loggerMock.Object);

            // Act
            await worker.ProcessDeliveryAsync(channelMock.Object, 99UL, "{broken-json");

            // Assert
            channelMock.Verify(c => c.BasicNackAsync(99UL, false, false, It.IsAny<CancellationToken>()), Times.Once);
            channelMock.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessDeliveryAsync_ShouldNackWithRequeue_WhenTransientExceptionOccurs() {
            // Arrange
            var channelMock = new Mock<IChannel>();
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

            _useCaseMock
                .Setup(u => u.ExecuteAsync(It.IsAny<TransactionCreatedEventEntity>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Database transient timeout"));

            var worker = new ConsolidationWorkerHostedService(_scopeFactory, _configuration, _loggerMock.Object);
            var json = JsonSerializer.Serialize(txEvent);

            // Act
            await worker.ProcessDeliveryAsync(channelMock.Object, 77UL, json);

            // Assert
            channelMock.Verify(c => c.BasicNackAsync(77UL, false, true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task EnsureTopologyDeclaredAsync_ShouldDeclareExchangesQueuesAndBindingsWithDLQ() {
            // Arrange
            var channelMock = new Mock<IChannel>();

            // Act
            await RabbitMQMessagePublisherAdapter.EnsureTopologyDeclaredAsync(channelMock.Object);

            // Assert
            channelMock.Verify(c => c.ExchangeDeclareAsync(
                RabbitMQMessagePublisherAdapter.DeadLetterExchangeName,
                ExchangeType.Direct,
                true,
                false,
                null,
                false,
                false,
                It.IsAny<CancellationToken>()), Times.Once);

            channelMock.Verify(c => c.QueueDeclareAsync(
                RabbitMQMessagePublisherAdapter.DeadLetterQueueName,
                true,
                false,
                false,
                null,
                false,
                false,
                It.IsAny<CancellationToken>()), Times.Once);

            channelMock.Verify(c => c.ExchangeDeclareAsync(
                RabbitMQMessagePublisherAdapter.ExchangeName,
                ExchangeType.Direct,
                true,
                false,
                null,
                false,
                false,
                It.IsAny<CancellationToken>()), Times.Once);

            channelMock.Verify(c => c.QueueDeclareAsync(
                RabbitMQMessagePublisherAdapter.QueueName,
                true,
                false,
                false,
                It.Is<IDictionary<string, object?>>(args =>
                    args.ContainsKey("x-dead-letter-exchange") &&
                    Equals(args["x-dead-letter-exchange"], RabbitMQMessagePublisherAdapter.DeadLetterExchangeName) &&
                    args.ContainsKey("x-dead-letter-routing-key") &&
                    Equals(args["x-dead-letter-routing-key"], RabbitMQMessagePublisherAdapter.DeadLetterQueueName)),
                false,
                false,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StartAndStopAsync_ShouldExitImmediately_WhenDisabledByConfiguration() {
            // Arrange
            var worker = new ConsolidationWorkerHostedService(_scopeFactory, _configuration, _loggerMock.Object);

            // Act
            await worker.StartAsync(CancellationToken.None);
            await Task.Delay(25);
            await worker.StopAsync(CancellationToken.None);

            // Assert
            _loggerMock.Verify(l => l.LogInformation(It.Is<string>(s => s.Contains("disabled")), It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public async Task PublishTransactionCreatedAsync_ShouldDeclareTopologyAndPublishWithPersistentProperties() {
            // Arrange
            var connectionMock = new Mock<IConnection>();
            var channelMock = new Mock<IChannel>();
            connectionMock.Setup(c => c.IsOpen).Returns(true);
            connectionMock.Setup(c => c.CreateChannelAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(channelMock.Object);

            var adapter = new TestableRabbitMQMessagePublisherAdapter(_configuration, connectionMock.Object);
            var txEvent = new TransactionCreatedEventEntity(
                TransactionId: Guid.NewGuid(),
                AccountId: Guid.NewGuid(),
                AccountNumber: "12345-6",
                Amount: 200m,
                BalanceAfter: 1200m,
                TransactionType: "Crédito em conta",
                IdempotencyKey: Guid.NewGuid(),
                CreatedAt: DateTime.UtcNow
            );

            // Act
            await adapter.PublishTransactionCreatedAsync(txEvent);
            await adapter.DisposeAsync();

            // Assert
            channelMock.Verify(c => c.BasicPublishAsync(
                RabbitMQMessagePublisherAdapter.ExchangeName,
                RabbitMQMessagePublisherAdapter.QueueName,
                true,
                It.Is<BasicProperties>(p => p.Persistent && p.MessageId == txEvent.TransactionId.ToString() && p.ContentType == "application/json"),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        private sealed class TestableRabbitMQMessagePublisherAdapter : RabbitMQMessagePublisherAdapter {
            private readonly IConnection _mockConnection;

            public TestableRabbitMQMessagePublisherAdapter(IConfiguration configuration, IConnection mockConnection)
                : base(configuration) {
                _mockConnection = mockConnection;
            }

            protected override Task<IConnection> GetOrCreateConnectionAsync(CancellationToken cancellationToken = default) {
                return Task.FromResult(_mockConnection);
            }
        }
    }
}
