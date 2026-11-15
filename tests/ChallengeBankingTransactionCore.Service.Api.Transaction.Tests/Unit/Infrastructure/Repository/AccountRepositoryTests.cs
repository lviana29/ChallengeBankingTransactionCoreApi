using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repositories;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repository.Implementation;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Infrastructure.Repository {
    public class AccountRepositoryTests {
        private readonly DbContextOptions<AppDbContext> _dbContextOptions;

        public AccountRepositoryTests() {
            _dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"AccountTestDb_{Guid.NewGuid()}")
                .Options;
        }

        [Fact]
        public async Task GetByAccountNumber_ShouldReturnAccountEntity_WhenAccountExists() {
            // Arrange
            using var context = new AppDbContext(_dbContextOptions);
            var accountId = Guid.NewGuid();
            var accountNumber = "12345-6";

            context.Accounts.Add(new Account {
                Id = accountId,
                AccountNumber = accountNumber,
                Balance = 500m,
                IsActive = true
            });
            await context.SaveChangesAsync();

            var genericRepoMock = new Mock<IGenericRepository<Account>>();
            var repository = new AccountRepository(context, genericRepoMock.Object);

            // Act
            var result = await repository.GetByAccountNumber(accountNumber);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(accountNumber, result.AccountNumber);
            Assert.Equal(500m, result.Balance);
            Assert.True(result.IsActive);
        }

        [Fact]
        public async Task GetByAccountNumber_ShouldReturnEmptyAccountEntity_WhenAccountDoesNotExist() {
            // Arrange
            using var context = new AppDbContext(_dbContextOptions);
            var genericRepoMock = new Mock<IGenericRepository<Account>>();
            var repository = new AccountRepository(context, genericRepoMock.Object);

            // Act
            var result = await repository.GetByAccountNumber("00000-0");

            // Assert
            Assert.NotNull(result);
            // Como o repositório retorna new AccountEntity() quando é nulo:
            Assert.Equal(Guid.Empty, result.Id);
            Assert.Equal(string.Empty, result.AccountNumber);
        }
    }
}
