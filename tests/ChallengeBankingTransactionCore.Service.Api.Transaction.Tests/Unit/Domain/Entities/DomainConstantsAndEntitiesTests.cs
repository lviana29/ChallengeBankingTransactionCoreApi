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
    }
}
