using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repository.Implementation;
using Microsoft.EntityFrameworkCore;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Infrastructure.Repository {
    public class DailyConsolidatedBalanceRepositoryTests {
        private static DbContextOptions<AppDbContext> CreateOptions() {
            return new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"DailyBalanceRepoTestDb_{Guid.NewGuid()}")
                .Options;
        }

        [Fact]
        public async Task UpsertDailyBalanceWithInboxAsync_ShouldCreateNewDailyBalance_AndAccumulateCreditAndDebit() {
            // Arrange
            using var context = new AppDbContext(CreateOptions());
            var repo = new DailyConsolidatedBalanceRepository(context);
            var accountId = Guid.NewGuid();
            var refDate = new DateTime(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc);

            var creditEvent = new TransactionCreatedEventEntity(
                TransactionId: Guid.NewGuid(),
                AccountId: accountId,
                AccountNumber: "12345-6",
                Amount: 300m,
                BalanceAfter: 1300m,
                TransactionType: "Crédito em conta",
                IdempotencyKey: Guid.NewGuid(),
                CreatedAt: refDate
            );

            var debitEvent = new TransactionCreatedEventEntity(
                TransactionId: Guid.NewGuid(),
                AccountId: accountId,
                AccountNumber: "12345-6",
                Amount: 100m,
                BalanceAfter: 1200m,
                TransactionType: "Débito em conta",
                IdempotencyKey: Guid.NewGuid(),
                CreatedAt: refDate.AddHours(2)
            );

            // Act
            var firstResult = await repo.UpsertDailyBalanceWithInboxAsync(creditEvent);
            var secondResult = await repo.UpsertDailyBalanceWithInboxAsync(debitEvent);

            // Assert
            Assert.True(firstResult);
            Assert.True(secondResult);

            Assert.True(await repo.HasEventBeenProcessedAsync(creditEvent.TransactionId));
            Assert.True(await repo.HasEventBeenProcessedAsync(debitEvent.TransactionId));
            Assert.False(await repo.HasEventBeenProcessedAsync(Guid.NewGuid()));

            var daily = await repo.GetDailyBalanceAsync(accountId, refDate);
            Assert.NotNull(daily);
            Assert.Equal("12345-6", daily.AccountNumber);
            Assert.Equal(300m, daily.TotalCredits);
            Assert.Equal(100m, daily.TotalDebits);
            Assert.Equal(1200m, daily.ClosingBalance);
            Assert.Equal(2, daily.TransactionCount);
        }

        [Fact]
        public async Task UpsertDailyBalanceWithInboxAsync_ShouldReturnFalseAndNotModifyBalance_WhenEventIsRepeated() {
            // Arrange
            using var context = new AppDbContext(CreateOptions());
            var repo = new DailyConsolidatedBalanceRepository(context);
            var accountId = Guid.NewGuid();
            var txId = Guid.NewGuid();
            var refDate = new DateTime(2026, 11, 18, 14, 0, 0, DateTimeKind.Utc);

            var creditEvent = new TransactionCreatedEventEntity(
                TransactionId: txId,
                AccountId: accountId,
                AccountNumber: "12345-6",
                Amount: 500m,
                BalanceAfter: 1500m,
                TransactionType: "Crédito em conta",
                IdempotencyKey: Guid.NewGuid(),
                CreatedAt: refDate
            );

            // Act
            var first = await repo.UpsertDailyBalanceWithInboxAsync(creditEvent);
            var duplicate = await repo.UpsertDailyBalanceWithInboxAsync(creditEvent);

            // Assert
            Assert.True(first);
            Assert.False(duplicate);

            var daily = await repo.GetDailyBalanceAsync(accountId, refDate);
            Assert.NotNull(daily);
            Assert.Equal(500m, daily.TotalCredits);
            Assert.Equal(1, daily.TransactionCount);
        }

        [Fact]
        public async Task GetDailyBalancesInRangeAsync_ShouldReturnOrderedBalancesWithinDateRange() {
            // Arrange
            using var context = new AppDbContext(CreateOptions());
            var repo = new DailyConsolidatedBalanceRepository(context);
            var accountId = Guid.NewGuid();
            var day1 = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
            var day2 = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
            var day3 = new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

            await repo.UpsertDailyBalanceWithInboxAsync(new TransactionCreatedEventEntity(
                Guid.NewGuid(), accountId, "12345-6", 100m, 1100m, "Crédito em conta", Guid.NewGuid(), day2));
            await repo.UpsertDailyBalanceWithInboxAsync(new TransactionCreatedEventEntity(
                Guid.NewGuid(), accountId, "12345-6", 50m, 1050m, "Débito em conta", Guid.NewGuid(), day1));
            await repo.UpsertDailyBalanceWithInboxAsync(new TransactionCreatedEventEntity(
                Guid.NewGuid(), accountId, "12345-6", 200m, 1300m, "Crédito em conta", Guid.NewGuid(), day3));

            // Act
            var range = await repo.GetDailyBalancesInRangeAsync(accountId, day1, day2);

            // Assert
            Assert.Equal(2, range.Count);
            Assert.Equal(day1.Date, range[0].BalanceDate);
            Assert.Equal(day2.Date, range[1].BalanceDate);
        }
    }
}
