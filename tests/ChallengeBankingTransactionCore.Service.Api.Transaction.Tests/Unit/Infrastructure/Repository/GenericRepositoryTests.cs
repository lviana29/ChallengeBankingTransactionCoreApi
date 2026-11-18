using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repositories.Implementation;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Infrastructure.Repository {
    public class GenericRepositoryTests {
        private readonly DbContextOptions<AppDbContext> _dbContextOptions;
        private readonly Mock<IResiliencePolicyExecutorPort> _resilienceMock;

        public GenericRepositoryTests() {
            _dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"GenericRepoTestDb_{Guid.NewGuid()}")
                .Options;

            _resilienceMock = new Mock<IResiliencePolicyExecutorPort>();
            _resilienceMock
                .Setup(r => r.ExecuteAsyncWithoutResult(It.IsAny<Func<Task>>()))
                .Returns<Func<Task>>(async action => await action());
        }

        [Fact]
        public void Create_ShouldAddEntityAndSaveChanges_WhenSaveChangesIsTrue() {
            using var context = new AppDbContext(_dbContextOptions);
            var repo = new GenericRepository<Account>(context, _resilienceMock.Object);

            var account = new Account {
                Id = Guid.NewGuid(),
                AccountNumber = "11111-1",
                Balance = 100m,
                IsActive = true
            };

            var created = repo.Create(account, saveChanges: true);

            Assert.Equal(account.Id, created.Id);
            Assert.True(repo.Exists(account.Id));
            _resilienceMock.Verify(r => r.ExecuteAsyncWithoutResult(It.IsAny<Func<Task>>()), Times.Once);
        }

        [Fact]
        public void Create_ShouldAddEntityWithoutSavingChanges_WhenSaveChangesIsFalse() {
            using var context = new AppDbContext(_dbContextOptions);
            var repo = new GenericRepository<Account>(context, _resilienceMock.Object);

            var account = new Account {
                Id = Guid.NewGuid(),
                AccountNumber = "22222-2",
                Balance = 200m,
                IsActive = true
            };

            var created = repo.Create(account, saveChanges: false);

            Assert.Equal(account.Id, created.Id);
            Assert.False(repo.Exists(account.Id));
            _resilienceMock.Verify(r => r.ExecuteAsyncWithoutResult(It.IsAny<Func<Task>>()), Times.Never);
        }

        [Fact]
        public void FindById_FindAll_Exists_ShouldWorkAsExpected() {
            using var context = new AppDbContext(_dbContextOptions);
            var repo = new GenericRepository<Account>(context, _resilienceMock.Object);

            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();

            repo.Create(new Account { Id = id1, AccountNumber = "10001-1", Balance = 10m, IsActive = true }, saveChanges: true);
            repo.Create(new Account { Id = id2, AccountNumber = "10002-2", Balance = 20m, IsActive = true }, saveChanges: true);

            var found1 = repo.FindById(id1);
            var notFound = repo.FindById(Guid.NewGuid());
            var all = repo.FindAll();

            Assert.NotNull(found1);
            Assert.Equal("10001-1", found1.AccountNumber);
            Assert.Null(notFound);
            Assert.Equal(2, all.Count);
            Assert.True(repo.Exists(id2));
            Assert.False(repo.Exists(Guid.NewGuid()));
        }

        [Fact]
        public void Update_ShouldModifyExistingEntity_WhenSaveChangesIsTrueAndFalse() {
            using var context = new AppDbContext(_dbContextOptions);
            var repo = new GenericRepository<Account>(context, _resilienceMock.Object);

            var id = Guid.NewGuid();
            repo.Create(new Account { Id = id, AccountNumber = "33333-3", Balance = 300m, IsActive = true }, saveChanges: true);

            var updatedEntity = new Account { Id = id, AccountNumber = "33333-3", Balance = 750m, IsActive = false };
            var resultTrue = repo.Update(updatedEntity, saveChanges: true);

            Assert.NotNull(resultTrue);
            Assert.Equal(750m, resultTrue.Balance);
            Assert.False(resultTrue.IsActive);

            var secondUpdate = new Account { Id = id, AccountNumber = "33333-3", Balance = 900m, IsActive = true };
            var resultFalse = repo.Update(secondUpdate, saveChanges: false);

            Assert.NotNull(resultFalse);
            Assert.Equal(900m, resultFalse.Balance);
        }

        [Fact]
        public void Update_ShouldReturnNull_WhenEntityDoesNotExist() {
            using var context = new AppDbContext(_dbContextOptions);
            var repo = new GenericRepository<Account>(context, _resilienceMock.Object);

            var nonExistent = new Account { Id = Guid.NewGuid(), AccountNumber = "99999-9", Balance = 0m };
            var result = repo.Update(nonExistent, saveChanges: true);

            Assert.Null(result);
        }

        [Fact]
        public void Delete_ShouldRemoveEntity_WhenSaveChangesIsTrueAndFalse_AndIgnoreNonExistent() {
            using var context = new AppDbContext(_dbContextOptions);
            var repo = new GenericRepository<Account>(context, _resilienceMock.Object);

            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            repo.Create(new Account { Id = id1, AccountNumber = "44444-4", Balance = 50m, IsActive = true }, saveChanges: true);
            repo.Create(new Account { Id = id2, AccountNumber = "55555-5", Balance = 60m, IsActive = true }, saveChanges: true);

            // Delete with saveChanges = true
            repo.Delete(id1, saveChanges: true);
            Assert.False(repo.Exists(id1));

            // Delete with saveChanges = false
            repo.Delete(id2, saveChanges: false);
            context.SaveChanges();
            Assert.False(repo.Exists(id2));

            // Delete non-existent entity (no-op)
            repo.Delete(Guid.NewGuid(), saveChanges: true);
        }
    }
}
