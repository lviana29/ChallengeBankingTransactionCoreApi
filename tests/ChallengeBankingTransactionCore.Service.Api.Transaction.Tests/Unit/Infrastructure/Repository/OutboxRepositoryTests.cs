using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repository.Implementation;
using Microsoft.EntityFrameworkCore;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Infrastructure.Repository {
    public class OutboxRepositoryTests {
        private static DbContextOptions<AppDbContext> CreateOptions() {
            return new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"OutboxRepoTestDb_{Guid.NewGuid()}")
                .Options;
        }

        [Fact]
        public async Task AddOutboxMessage_ShouldTrackMessageAndPersistOnSaveChanges() {
            // Arrange
            using var context = new AppDbContext(CreateOptions());
            var repo = new OutboxRepository(context);
            var msgId = Guid.NewGuid();
            var entity = new OutboxMessageEntity {
                Id = msgId,
                EventType = nameof(TransactionCreatedEventEntity),
                Payload = "{\"amount\":100}",
                CreatedAt = DateTime.UtcNow
            };

            // Act
            repo.AddOutboxMessage(entity);
            // Antes do SaveChanges não está gravado no banco, mas rastreado
            await context.SaveChangesAsync();

            // Assert
            var saved = await context.OutboxMessages.FirstOrDefaultAsync(x => x.Id == msgId);
            Assert.NotNull(saved);
            Assert.Equal(nameof(TransactionCreatedEventEntity), saved.EventType);
            Assert.Equal("{\"amount\":100}", saved.Payload);
            Assert.Null(saved.ProcessedAt);
            Assert.Equal(0, saved.RetryCount);
        }

        [Fact]
        public async Task GetPendingMessagesAsync_ShouldReturnOnlyUnprocessedWithinRetryLimitOrderedByCreatedAt() {
            // Arrange
            using var context = new AppDbContext(CreateOptions());
            var repo = new OutboxRepository(context);
            var baseTime = DateTime.UtcNow;

            var pendingOld = new OutboxMessage {
                Id = Guid.NewGuid(),
                EventType = "TxCreated",
                Payload = "{\"seq\":1}",
                CreatedAt = baseTime.AddMinutes(-10),
                ProcessedAt = null,
                RetryCount = 1
            };
            var pendingNew = new OutboxMessage {
                Id = Guid.NewGuid(),
                EventType = "TxCreated",
                Payload = "{\"seq\":2}",
                CreatedAt = baseTime.AddMinutes(-5),
                ProcessedAt = null,
                RetryCount = 0
            };
            var alreadyProcessed = new OutboxMessage {
                Id = Guid.NewGuid(),
                EventType = "TxCreated",
                Payload = "{\"seq\":3}",
                CreatedAt = baseTime.AddMinutes(-20),
                ProcessedAt = baseTime,
                RetryCount = 0
            };
            var exhaustedRetries = new OutboxMessage {
                Id = Guid.NewGuid(),
                EventType = "TxCreated",
                Payload = "{\"seq\":4}",
                CreatedAt = baseTime.AddMinutes(-15),
                ProcessedAt = null,
                RetryCount = 5
            };

            context.OutboxMessages.AddRange(pendingNew, pendingOld, alreadyProcessed, exhaustedRetries);
            await context.SaveChangesAsync();

            // Act
            var pending = await repo.GetPendingMessagesAsync(batchSize: 10, maxRetries: 5);

            // Assert
            Assert.Equal(2, pending.Count);
            Assert.Equal(pendingOld.Id, pending[0].Id);
            Assert.Equal(pendingNew.Id, pending[1].Id);
        }

        [Fact]
        public async Task MarkAsProcessedAsync_ShouldSetProcessedAtAndClearLastError() {
            // Arrange
            using var context = new AppDbContext(CreateOptions());
            var repo = new OutboxRepository(context);
            var msgId = Guid.NewGuid();

            context.OutboxMessages.Add(new OutboxMessage {
                Id = msgId,
                EventType = "TxCreated",
                Payload = "{}",
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                RetryCount = 1,
                LastError = "Previous transient error"
            });
            await context.SaveChangesAsync();

            var processedAt = DateTime.UtcNow;

            // Act
            await repo.MarkAsProcessedAsync(msgId, processedAt);

            // Assert
            var updated = await context.OutboxMessages.FirstAsync(m => m.Id == msgId);
            Assert.Equal(processedAt, updated.ProcessedAt);
            Assert.Null(updated.LastError);
        }

        [Fact]
        public async Task MarkAsFailedAsync_ShouldIncrementRetryCountAndTruncateErrorOver500Chars() {
            // Arrange
            using var context = new AppDbContext(CreateOptions());
            var repo = new OutboxRepository(context);
            var msgId = Guid.NewGuid();

            context.OutboxMessages.Add(new OutboxMessage {
                Id = msgId,
                EventType = "TxCreated",
                Payload = "{}",
                CreatedAt = DateTime.UtcNow,
                RetryCount = 0
            });
            await context.SaveChangesAsync();

            var longError = new string('E', 650);

            // Act
            await repo.MarkAsFailedAsync(msgId, longError);

            // Assert
            var updated = await context.OutboxMessages.FirstAsync(m => m.Id == msgId);
            Assert.Equal(1, updated.RetryCount);
            Assert.NotNull(updated.LastError);
            Assert.Equal(500, updated.LastError.Length);
        }
    }
}
