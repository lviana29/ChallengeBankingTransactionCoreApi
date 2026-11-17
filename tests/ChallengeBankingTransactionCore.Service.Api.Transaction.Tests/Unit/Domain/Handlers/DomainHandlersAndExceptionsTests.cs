using ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Domain.Handlers {
    public class DomainHandlersAndExceptionsTests {
        [Fact]
        public void AccountBlockedError_ShouldHaveCorrectProperties() {
            // Act
            var ex = new AccountBlockedError();

            // Assert
            Assert.Equal("CORE_BANKING_002", ex.ErrorCode);
            Assert.Equal(403, ex.StatusCode);
            Assert.Equal("warning", ex.TypeReturn);
            Assert.Equal("Account blocked", ex.Message);
        }

        [Fact]
        public void AccountNotFoundError_ShouldHaveCorrectProperties() {
            // Act
            var ex = new AccountNotFoundError();

            // Assert
            Assert.Equal("CORE_BANKING_003", ex.ErrorCode);
            Assert.Equal(404, ex.StatusCode);
            Assert.Equal("warning", ex.TypeReturn);
            Assert.Equal("Account not found", ex.Message);
        }

        [Fact]
        public void InvalidTransactionTypeError_ShouldHaveCorrectProperties() {
            // Act
            var ex = new InvalidTransactionTypeError();

            // Assert
            Assert.Equal("CORE_BANKING_005", ex.ErrorCode);
            Assert.Equal(422, ex.StatusCode);
            Assert.Equal("warning", ex.TypeReturn);
            Assert.Contains("Invalid transaction type", ex.Message);
        }

        [Fact]
        public void InvalidValueToDebitError_ShouldHaveCorrectProperties() {
            // Act
            var ex = new InvalidValueToDebitError();

            // Assert
            Assert.Equal("CORE_BANKING_001", ex.ErrorCode);
            Assert.Equal(422, ex.StatusCode);
            Assert.Equal("warning", ex.TypeReturn);
            Assert.Equal("Invalid value to debit", ex.Message);
        }

        [Fact]
        public void ProcessTransactionError_ShouldHaveCorrectProperties() {
            // Act
            var ex = new ProcessTransactionError();

            // Assert
            Assert.Equal("CORE_BANKING_004", ex.ErrorCode);
            Assert.Equal(422, ex.StatusCode);
            Assert.Equal("error", ex.TypeReturn);
            Assert.Equal("Transaction failed", ex.Message);
        }

        [Fact]
        public void InvalidTransactionValueError_ShouldHaveCorrectProperties() {
            // Act
            var ex = new InvalidTransactionValueError();

            // Assert
            Assert.Equal("CORE_BANKING_006", ex.ErrorCode);
            Assert.Equal(422, ex.StatusCode);
            Assert.Equal("warning", ex.TypeReturn);
            Assert.Equal("Invalid value for transaction", ex.Message);
        }

        [Fact]
        public void InvalidAccountNumber_ShouldHaveCorrectProperties() {
            // Act
            var ex = new InvalidAccountNumber();

            // Assert
            Assert.Equal("CORE_BANKING_007", ex.ErrorCode);
            Assert.Equal(403, ex.StatusCode);
            Assert.Equal("warning", ex.TypeReturn);
            Assert.Equal("Invalid account number", ex.Message);
        }

        [Fact]
        public void ConsolidateStatementError_ShouldHaveCorrectProperties() {
            // Act
            var ex = new ConsolidateStatementError();

            // Assert
            Assert.Equal("CORE_BANKING_008", ex.ErrorCode);
            Assert.Equal(403, ex.StatusCode);
            Assert.Equal("warning", ex.TypeReturn);
            Assert.Equal("Consolidate statement error", ex.Message);
        }
    }
}
