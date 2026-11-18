using ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Infrastructure.Repository {
    public class UnitOfWorkTests {
        private sealed class ThrowingConcurrencyDbContext : AppDbContext {
            public ThrowingConcurrencyDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) {
                throw new DbUpdateConcurrencyException("Simulated row version conflict");
            }
        }

        private sealed class ThrowingGenericDbContext : AppDbContext {
            public ThrowingGenericDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) {
                throw new InvalidOperationException("Simulated generic database failure");
            }
        }

        private static DbContextOptions<AppDbContext> CreateOptions() {
            return new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"UnitOfWorkTestDb_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private static Mock<IResiliencePolicyExecutorPort> CreatePassThroughResilienceMock() {
            var mock = new Mock<IResiliencePolicyExecutorPort>();
            mock.Setup(r => r.ExecuteAsync(It.IsAny<Func<Task<bool>>>()))
                .Returns<Func<Task<bool>>>(func => func());
            return mock;
        }

        [Fact]
        public async Task CommitAsync_ShouldPersistEntitiesAndReturnTrue_WhenNoConflictOccurs() {
            // Arrange
            using var context = new AppDbContext(CreateOptions());
            var resilienceMock = CreatePassThroughResilienceMock();
            var unitOfWork = new UnitOfWork(context, resilienceMock.Object);

            context.OutboxMessages.Add(new OutboxMessage {
                Id = Guid.NewGuid(),
                EventType = "TransactionCreated",
                Payload = "{}"
            });

            // Act
            var success = await unitOfWork.CommitAsync();

            // Assert
            Assert.True(success);
            Assert.Equal(1, await context.OutboxMessages.CountAsync());
        }

        [Fact]
        public async Task CommitAsync_ShouldRollbackClearTrackerAndThrowAccountConcurrencyConflictException_WhenDbUpdateConcurrencyExceptionOccurs() {
            // Arrange
            using var context = new ThrowingConcurrencyDbContext(CreateOptions());
            var resilienceMock = CreatePassThroughResilienceMock();
            var unitOfWork = new UnitOfWork(context, resilienceMock.Object);

            context.OutboxMessages.Add(new OutboxMessage {
                Id = Guid.NewGuid(),
                EventType = "TransactionCreated",
                Payload = "{}"
            });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AccountConcurrencyConflictException>(() => unitOfWork.CommitAsync());
            Assert.Equal(409, ex.StatusCode);
            Assert.Empty(context.ChangeTracker.Entries());
        }

        [Fact]
        public async Task CommitAsync_ShouldRollbackClearTrackerAndRethrow_WhenGenericExceptionOccurs() {
            // Arrange
            using var context = new ThrowingGenericDbContext(CreateOptions());
            var resilienceMock = CreatePassThroughResilienceMock();
            var unitOfWork = new UnitOfWork(context, resilienceMock.Object);

            context.OutboxMessages.Add(new OutboxMessage {
                Id = Guid.NewGuid(),
                EventType = "TransactionCreated",
                Payload = "{}"
            });

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.CommitAsync());
            Assert.Empty(context.ChangeTracker.Entries());
        }
    }
}
