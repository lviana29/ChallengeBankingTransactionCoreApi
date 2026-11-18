using ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.ProcessTransactionEventUseCase;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using Moq;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Application.UseCases {
    public class ProcessTransactionEventUseCaseTests {
        private readonly Mock<IDailyConsolidatedBalanceRepository> _dailyBalanceRepoMock;
        private readonly Mock<IConsolidatedCachePort> _cachePortMock;
        private readonly Mock<IAppLogger<ProcessTransactionEventUseCase>> _loggerMock;
        private readonly ProcessTransactionEventUseCase _useCase;

        public ProcessTransactionEventUseCaseTests() {
            _dailyBalanceRepoMock = new Mock<IDailyConsolidatedBalanceRepository>();
            _cachePortMock = new Mock<IConsolidatedCachePort>();
            _loggerMock = new Mock<IAppLogger<ProcessTransactionEventUseCase>>();

            _useCase = new ProcessTransactionEventUseCase(
                _dailyBalanceRepoMock.Object,
                _cachePortMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task ExecuteAsync_ShouldProcessNewCreditEvent_AndInvalidateCache() {
            // Arrange
            var txEvent = new TransactionCreatedEventEntity(
                TransactionId: Guid.NewGuid(),
                AccountId: Guid.NewGuid(),
                AccountNumber: "12345-6",
                Amount: 250m,
                BalanceAfter: 1250m,
                TransactionType: "Crédito em conta",
                IdempotencyKey: Guid.NewGuid(),
                CreatedAt: DateTime.UtcNow
            );

            _dailyBalanceRepoMock
                .Setup(r => r.HasEventBeenProcessedAsync(txEvent.TransactionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _dailyBalanceRepoMock
                .Setup(r => r.UpsertDailyBalanceWithInboxAsync(txEvent, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _useCase.ExecuteAsync(txEvent);

            // Assert
            Assert.True(result);
            _dailyBalanceRepoMock.Verify(r => r.UpsertDailyBalanceWithInboxAsync(txEvent, It.IsAny<CancellationToken>()), Times.Once);
            _cachePortMock.Verify(c => c.InvalidateByAccountAsync("12345-6", It.IsAny<CancellationToken>()), Times.Once);
            _loggerMock.Verify(l => l.LogInformation(
                It.IsAny<string>(),
                It.Is<object[]>(args => args.Contains("12***-6") && !args.Contains("12345-6"))), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldProcessNewDebitEvent_AndInvalidateCache() {
            // Arrange
            var txEvent = new TransactionCreatedEventEntity(
                TransactionId: Guid.NewGuid(),
                AccountId: Guid.NewGuid(),
                AccountNumber: "12345-6",
                Amount: 75m,
                BalanceAfter: 925m,
                TransactionType: "Débito em conta",
                IdempotencyKey: Guid.NewGuid(),
                CreatedAt: DateTime.UtcNow
            );

            _dailyBalanceRepoMock
                .Setup(r => r.HasEventBeenProcessedAsync(txEvent.TransactionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _dailyBalanceRepoMock
                .Setup(r => r.UpsertDailyBalanceWithInboxAsync(txEvent, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _useCase.ExecuteAsync(txEvent);

            // Assert
            Assert.True(result);
            _dailyBalanceRepoMock.Verify(r => r.UpsertDailyBalanceWithInboxAsync(txEvent, It.IsAny<CancellationToken>()), Times.Once);
            _cachePortMock.Verify(c => c.InvalidateByAccountAsync("12345-6", It.IsAny<CancellationToken>()), Times.Once);
            _loggerMock.Verify(l => l.LogInformation(
                It.IsAny<string>(),
                It.Is<object[]>(args => args.Contains("12***-6") && !args.Contains("12345-6"))), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldIgnoreDuplicateEvent_WhenHasEventBeenProcessedReturnsTrue() {
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

            _dailyBalanceRepoMock
                .Setup(r => r.HasEventBeenProcessedAsync(txEvent.TransactionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _useCase.ExecuteAsync(txEvent);

            // Assert
            Assert.False(result);
            _dailyBalanceRepoMock.Verify(r => r.UpsertDailyBalanceWithInboxAsync(It.IsAny<TransactionCreatedEventEntity>(), It.IsAny<CancellationToken>()), Times.Never);
            _cachePortMock.Verify(c => c.InvalidateByAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldIgnoreDuplicateEvent_WhenUpsertReturnsFalse() {
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

            _dailyBalanceRepoMock
                .Setup(r => r.HasEventBeenProcessedAsync(txEvent.TransactionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _dailyBalanceRepoMock
                .Setup(r => r.UpsertDailyBalanceWithInboxAsync(txEvent, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _useCase.ExecuteAsync(txEvent);

            // Assert
            Assert.False(result);
            _cachePortMock.Verify(c => c.InvalidateByAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData("00000000-0000-0000-0000-000000000000", "11111111-1111-1111-1111-111111111111", "12345-6", 100)]
        [InlineData("11111111-1111-1111-1111-111111111111", "00000000-0000-0000-0000-000000000000", "12345-6", 100)]
        [InlineData("11111111-1111-1111-1111-111111111111", "11111111-1111-1111-1111-111111111111", "", 100)]
        [InlineData("11111111-1111-1111-1111-111111111111", "11111111-1111-1111-1111-111111111111", "12345-6", 0)]
        [InlineData("11111111-1111-1111-1111-111111111111", "11111111-1111-1111-1111-111111111111", "12345-6", -15)]
        public async Task ExecuteAsync_ShouldThrowConsolidateStatementError_WhenPayloadIsInvalid(
            string txIdStr,
            string accountIdStr,
            string accountNumber,
            decimal amount
        ) {
            // Arrange
            var txEvent = new TransactionCreatedEventEntity(
                TransactionId: Guid.Parse(txIdStr),
                AccountId: Guid.Parse(accountIdStr),
                AccountNumber: accountNumber,
                Amount: amount,
                BalanceAfter: 1000m,
                TransactionType: "Crédito em conta",
                IdempotencyKey: Guid.NewGuid(),
                CreatedAt: DateTime.UtcNow
            );

            // Act & Assert
            await Assert.ThrowsAsync<ConsolidateStatementError>(() => _useCase.ExecuteAsync(txEvent));
        }
    }
}
