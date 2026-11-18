using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Domain.Entities {
    public class DomainConstantsAndEntitiesTests {
        [Fact]
        public void TransactionTypeConstants_ShouldHaveExpectedGuids() {
            // Assert
            Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), TransactionTypeConstants.CreditId);
            Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), TransactionTypeConstants.DebitId);
        }

        [Fact]
        public void TransactionTypeEntityEnum_ShouldHaveCorrectIntegerMappings() {
            // Assert
            Assert.Equal(1, (int)TransactionTypeEntityEnum.Credit);
            Assert.Equal(2, (int)TransactionTypeEntityEnum.Debit);
        }

        [Fact]
        public void AccountEntity_ShouldInitializeWithDefaultValues() {
            // Arrange & Act
            var account = new AccountEntity();

            // Assert
            Assert.NotEqual(Guid.Empty, account.Id); // Garante que o Guid é gerado por padrão
            Assert.Equal(string.Empty, account.AccountNumber);
            Assert.Equal(0m, account.Balance);
            Assert.False(account.IsActive);
        }

        [Fact]
        public void TransactionEntity_ShouldInitializeWithDefaultValues() {
            // Arrange & Act
            var transaction = new TransactionEntity();

            // Assert
            Assert.NotEqual(Guid.Empty, transaction.Id);
            Assert.Equal(string.Empty, transaction.Description);
            Assert.Equal(string.Empty, transaction.AccountId);
            Assert.Equal(0m, transaction.Amount);
            Assert.Equal(0m, transaction.BalanceAfter);
        }

        [Fact]
        public void OutboxMessageEntity_ShouldInitializeAndSetPropertiesCorrectly() {
            // Arrange & Act
            var id = Guid.NewGuid();
            var createdAt = DateTime.UtcNow;
            var processedAt = createdAt.AddSeconds(2);
            var outbox = new OutboxMessageEntity {
                Id = id,
                EventType = "TransactionCreated",
                Payload = "{\"amount\":100}",
                CreatedAt = createdAt,
                ProcessedAt = processedAt,
                RetryCount = 2,
                LastError = "Connection refused"
            };

            // Assert
            Assert.Equal(id, outbox.Id);
            Assert.Equal("TransactionCreated", outbox.EventType);
            Assert.Equal("{\"amount\":100}", outbox.Payload);
            Assert.Equal(createdAt, outbox.CreatedAt);
            Assert.Equal(processedAt, outbox.ProcessedAt);
            Assert.Equal(2, outbox.RetryCount);
            Assert.Equal("Connection refused", outbox.LastError);

            var defaultOutbox = new OutboxMessageEntity();
            Assert.NotEqual(Guid.Empty, defaultOutbox.Id);
            Assert.Equal(string.Empty, defaultOutbox.EventType);
            Assert.Equal(string.Empty, defaultOutbox.Payload);
            Assert.Null(defaultOutbox.ProcessedAt);
            Assert.Equal(0, defaultOutbox.RetryCount);
            Assert.Null(defaultOutbox.LastError);
        }

        [Fact]
        public void DailyConsolidatedBalanceEntity_ShouldInitializeAndSetPropertiesCorrectly() {
            // Arrange & Act
            var id = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var balanceDate = DateTime.UtcNow.Date;
            var updatedAt = DateTime.UtcNow;

            var dailyBalance = new DailyConsolidatedBalanceEntity {
                Id = id,
                AccountId = accountId,
                AccountNumber = "12345-6",
                BalanceDate = balanceDate,
                TotalCredits = 500.50m,
                TotalDebits = 150.25m,
                ClosingBalance = 1350.25m,
                TransactionCount = 4,
                UpdatedAt = updatedAt
            };

            // Assert
            Assert.Equal(id, dailyBalance.Id);
            Assert.Equal(accountId, dailyBalance.AccountId);
            Assert.Equal("12345-6", dailyBalance.AccountNumber);
            Assert.Equal(balanceDate, dailyBalance.BalanceDate);
            Assert.Equal(500.50m, dailyBalance.TotalCredits);
            Assert.Equal(150.25m, dailyBalance.TotalDebits);
            Assert.Equal(1350.25m, dailyBalance.ClosingBalance);
            Assert.Equal(4, dailyBalance.TransactionCount);
            Assert.Equal(updatedAt, dailyBalance.UpdatedAt);

            var defaultDaily = new DailyConsolidatedBalanceEntity();
            Assert.NotEqual(Guid.Empty, defaultDaily.Id);
            Assert.Equal(string.Empty, defaultDaily.AccountNumber);
        }

        [Fact]
        public void ProcessedEventEntity_ShouldInitializeAndSetPropertiesCorrectly() {
            // Arrange & Act
            var eventId = Guid.NewGuid();
            var processedAt = DateTime.UtcNow;
            var processedEvent = new ProcessedEventEntity {
                EventId = eventId,
                EventType = "TransactionCreatedEvent",
                ProcessedAt = processedAt
            };

            // Assert
            Assert.Equal(eventId, processedEvent.EventId);
            Assert.Equal("TransactionCreatedEvent", processedEvent.EventType);
            Assert.Equal(processedAt, processedEvent.ProcessedAt);

            var defaultProcessed = new ProcessedEventEntity();
            Assert.Equal(string.Empty, defaultProcessed.EventType);
        }

        [Fact]
        public void TransactionCreatedEventEntity_ShouldStoreAllPropertiesCorrectly() {
            // Arrange
            var txId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var idempotencyKey = Guid.NewGuid();
            var createdAt = DateTime.UtcNow;

            // Act
            var evt = new TransactionCreatedEventEntity(
                TransactionId: txId,
                AccountId: accountId,
                AccountNumber: "12345-6",
                Amount: 250.75m,
                BalanceAfter: 1250.75m,
                TransactionType: "Crédito em conta",
                IdempotencyKey: idempotencyKey,
                CreatedAt: createdAt
            );

            // Assert
            Assert.Equal(txId, evt.TransactionId);
            Assert.Equal(accountId, evt.AccountId);
            Assert.Equal("12345-6", evt.AccountNumber);
            Assert.Equal(250.75m, evt.Amount);
            Assert.Equal(1250.75m, evt.BalanceAfter);
            Assert.Equal("Crédito em conta", evt.TransactionType);
            Assert.Equal(idempotencyKey, evt.IdempotencyKey);
            Assert.Equal(createdAt, evt.CreatedAt);
        }
    }
}
