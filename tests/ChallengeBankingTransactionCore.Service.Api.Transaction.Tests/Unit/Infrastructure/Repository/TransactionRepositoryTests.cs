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
    }
}
