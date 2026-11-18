using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repositories;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repository.Implementation;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Infrastructure.Repository {
    public class TransactionRepositoryTests {
        private readonly DbContextOptions<AppDbContext> _dbContextOptions;

        public TransactionRepositoryTests() {
            _dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"TransactionTestDb_{Guid.NewGuid()}")
                .Options;
        }

        [Fact]
        public async Task GetByIdempotency_ShouldReturnTransactionEntity_WhenKeyExists() {
            // Arrange
            using var context = new AppDbContext(_dbContextOptions);
            var idempotencyKey = Guid.NewGuid();
            var transactionId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var typeId = Guid.NewGuid();

            context.Transactions.Add(new Service.Infrastructure.Transaction.Model.Transaction {
                Id = transactionId,
                AccountId = accountId,
                IdempotencyKey = idempotencyKey,
                Amount = 150m,
                BalanceAfter = 1150m,
                Description = "Deposit",
                TransactionTypeId = typeId,
                Type = new TransactionType { Id = typeId, Title = "Credit" }
            });
            await context.SaveChangesAsync();

            var genericRepoMock = new Mock<IGenericRepository<Service.Infrastructure.Transaction.Model.Transaction>>();
            var repository = new TransactionRepository(context, genericRepoMock.Object);

            // Act
            var result = await repository.GetByIdempotency(idempotencyKey);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(transactionId, result.Id);
            Assert.Equal(idempotencyKey, result.IdempotencyKey);
            Assert.Equal(150m, result.Amount);
        }

        [Fact]
        public async Task GetByIdempotency_ShouldReturnNull_WhenKeyDoesNotExist() {
            // Arrange
            using var context = new AppDbContext(_dbContextOptions);
            var genericRepoMock = new Mock<IGenericRepository<Service.Infrastructure.Transaction.Model.Transaction>>();
            var repository = new TransactionRepository(context, genericRepoMock.Object);

            // Act
            var result = await repository.GetByIdempotency(Guid.NewGuid());

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ProcessTransactionAsync_ShouldInvokeGenericRepositoryCreate_AndReturnTransactionId() {
            // Arrange
            using var context = new AppDbContext(_dbContextOptions);
            var accountId = Guid.NewGuid();
            var idempotencyKey = Guid.NewGuid();

            var genericRepoMock = new Mock<IGenericRepository<Service.Infrastructure.Transaction.Model.Transaction>>();

            // Configura o mock do repositório genérico para apenas retornar a entidade passada
            genericRepoMock
                .Setup(r => r.Create(It.IsAny<Service.Infrastructure.Transaction.Model.Transaction>(), false))
                .Returns<Service.Infrastructure.Transaction.Model.Transaction, bool>((entity, save) => entity);

            var repository = new TransactionRepository(context, genericRepoMock.Object);

            // Act
            var transactionId = repository.ProcessTransactionAsync(accountId, "Pix In", 300m, idempotencyKey, 1300m, TransactionTypeConstants.CreditId);

            // Assert
            Assert.NotEqual(Guid.Empty, transactionId);

            // Verifica se o repositório genérico foi chamado exatamente uma vez com saveChanges = false
            genericRepoMock.Verify(r => r.Create(It.Is<Service.Infrastructure.Transaction.Model.Transaction>(t =>
                t.AccountId == accountId &&
                t.Amount == 300m &&
                t.IdempotencyKey == idempotencyKey &&
                t.BalanceAfter == 1300m
            ), false), Times.Once);
        }

        [Fact]
        public async Task GetRangeByAccountNumber_ShouldFilterByAccountAndOptionalDates() {
            // Arrange
            using var context = new AppDbContext(_dbContextOptions);
            var accountId = Guid.NewGuid();
            var otherAccountId = Guid.NewGuid();
            var typeId = Guid.NewGuid();
            var txType = new TransactionType { Id = typeId, Title = "Credit" };
            context.Set<TransactionType>().Add(txType);

            var t1Date = new DateTime(2026, 5, 10, 10, 0, 0, DateTimeKind.Utc);
            var t2Date = new DateTime(2026, 5, 15, 14, 0, 0, DateTimeKind.Utc);
            var t3Date = new DateTime(2026, 5, 20, 18, 0, 0, DateTimeKind.Utc);

            context.Transactions.AddRange(
                new Service.Infrastructure.Transaction.Model.Transaction {
                    Id = Guid.NewGuid(),
                    AccountId = accountId,
                    Amount = 100m,
                    BalanceAfter = 100m,
                    CreatedAt = t1Date,
                    TransactionTypeId = typeId,
                    Type = txType
                },
                new Service.Infrastructure.Transaction.Model.Transaction {
                    Id = Guid.NewGuid(),
                    AccountId = accountId,
                    Amount = 200m,
                    BalanceAfter = 300m,
                    CreatedAt = t2Date,
                    TransactionTypeId = typeId,
                    Type = txType
                },
                new Service.Infrastructure.Transaction.Model.Transaction {
                    Id = Guid.NewGuid(),
                    AccountId = accountId,
                    Amount = 300m,
                    BalanceAfter = 600m,
                    CreatedAt = t3Date,
                    TransactionTypeId = typeId,
                    Type = txType
                },
                new Service.Infrastructure.Transaction.Model.Transaction {
                    Id = Guid.NewGuid(),
                    AccountId = otherAccountId,
                    Amount = 999m,
                    BalanceAfter = 999m,
                    CreatedAt = t2Date,
                    TransactionTypeId = typeId,
                    Type = txType
                }
            );
            await context.SaveChangesAsync();

            var genericRepoMock = new Mock<IGenericRepository<Service.Infrastructure.Transaction.Model.Transaction>>();
            var repository = new TransactionRepository(context, genericRepoMock.Object);

            // Act 1: With both startDate and endDate
            var filtered = await repository.GetRangeByAccountNumber(
                accountId,
                new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 16, 0, 0, 0, DateTimeKind.Utc)
            );

            // Act 2: With null dates (returns all for accountId)
            var allForAccount = await repository.GetRangeByAccountNumber(accountId, null, null);

            // Assert
            Assert.Single(filtered);
            Assert.Equal(200m, filtered[0].Amount);
            Assert.Equal(3, allForAccount.Count);
        }

        [Fact]
        public async Task GetBalanceAtDateAsync_ShouldHandleMidnightAndSpecificTime_AndReturnNullWhenEmpty() {
            // Arrange
            using var context = new AppDbContext(_dbContextOptions);
            var accountId = Guid.NewGuid();
            var typeId = Guid.NewGuid();

            context.Transactions.AddRange(
                new Service.Infrastructure.Transaction.Model.Transaction {
                    Id = Guid.NewGuid(),
                    AccountId = accountId,
                    Amount = 100m,
                    BalanceAfter = 1100m,
                    CreatedAt = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc),
                    TransactionTypeId = typeId
                },
                new Service.Infrastructure.Transaction.Model.Transaction {
                    Id = Guid.NewGuid(),
                    AccountId = accountId,
                    Amount = 250m,
                    BalanceAfter = 1350m,
                    CreatedAt = new DateTime(2026, 6, 10, 18, 30, 0, DateTimeKind.Utc),
                    TransactionTypeId = typeId
                }
            );
            await context.SaveChangesAsync();

            var genericRepoMock = new Mock<IGenericRepository<Service.Infrastructure.Transaction.Model.Transaction>>();
            var repository = new TransactionRepository(context, genericRepoMock.Object);

            // Act 1: Midnight date (TimeSpan.Zero) -> adjusts to end of day (23:59:59.9999999)
            var balanceEndOfDay = await repository.GetBalanceAtDateAsync(accountId, new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));

            // Act 2: Specific time (12:00:00) -> only includes the 09:00 transaction
            var balanceMidDay = await repository.GetBalanceAtDateAsync(accountId, new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

            // Act 3: Date before any transaction -> returns null
            var balanceBefore = await repository.GetBalanceAtDateAsync(accountId, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

            // Assert
            Assert.Equal(1350m, balanceEndOfDay);
            Assert.Equal(1100m, balanceMidDay);
            Assert.Null(balanceBefore);
        }
    }
}
