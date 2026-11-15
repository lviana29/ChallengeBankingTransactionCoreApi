using ChallengeBankingTransactionCore.Service.Application.Transaction.Dto;
using ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.ProcessTransactionUseCase;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using Moq;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Application.UseCases {
    public class ProcessTransactionUseCaseTests {
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<ITransactionRepository> _transactionRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly ProcessTransactionUseCase _useCase;

        public ProcessTransactionUseCaseTests() {
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _transactionRepositoryMock = new Mock<ITransactionRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _useCase = new ProcessTransactionUseCase(
                _accountRepositoryMock.Object,
                _transactionRepositoryMock.Object,
                _unitOfWorkMock.Object
            );
        }

        [Fact]
        public async Task ExecuteAsync_ShouldProcessCreditSuccessfully_WhenAccountIsValid() {
            // Arrange
            var accountId = Guid.NewGuid();
            var accountNumber = "12345-6";
            var idempotencyKey = Guid.NewGuid();

            var account = new AccountEntity {
                Id = accountId,
                AccountNumber = accountNumber,
                Balance = 1000m,
                IsActive = true
            };

            var dto = new TransactionDto(accountNumber, 200, 1, idempotencyKey.ToString());

            _transactionRepositoryMock
                .Setup(r => r.GetByIdempotency(idempotencyKey))
                .ReturnsAsync((TransactionEntity?)null);

            _accountRepositoryMock
                .Setup(r => r.GetByAccountNumber(accountNumber))
                .ReturnsAsync(account);

            _unitOfWorkMock
                .Setup(u => u.CommitAsync())
                .ReturnsAsync(true);

            // Act
            var resultId = await _useCase.ExecuteAsync(dto);

            // Assert
            Assert.NotNull(resultId);
            Assert.True(Guid.TryParse(resultId, out _));

            // Verifica se o saldo foi atualizado corretamente na conta (1000 + 200)
            Assert.Equal(1200, account.Balance);

            // Garante que a conta foi consultada e o commit foi realizado
            _accountRepositoryMock.Verify(r => r.GetByAccountNumber(accountNumber), Times.Once);
            _accountRepositoryMock.Verify(r => r.UpdateAccount(It.Is<AccountEntity>(a => a.Id == accountId && a.Balance == 1200)), Times.Once);

            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldThrowAccountNotFoundException_WhenAccountDoesNotExist() {
            // Arrange
            var accountNumber = "99999-9";

            var dto = new TransactionDto(accountNumber, 100, 1, Guid.NewGuid().ToString());


            _transactionRepositoryMock
                .Setup(r => r.GetByIdempotency(It.IsAny<Guid>()))
                .ReturnsAsync((TransactionEntity?)null);

            _accountRepositoryMock
                .Setup(r => r.GetByAccountNumber(accountNumber))
                .ReturnsAsync((AccountEntity)null!);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<AccountNotFoundError>(() => _useCase.ExecuteAsync(dto));

            Assert.NotNull(exception);
            Assert.Equal("Account not found", exception.Message);
            Assert.Equal(404, exception.StatusCode);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldThrowAccountBlockedException_WhenAccountIsInactive() {
            // Arrange
            var accountNumber = "12345-6";
            var account = new AccountEntity {
                Id = Guid.NewGuid(),
                AccountNumber = accountNumber,
                Balance = 1000,
                IsActive = false
            };

            var dto = new TransactionDto(accountNumber, 100m, 1, Guid.NewGuid().ToString());

            _transactionRepositoryMock
                .Setup(r => r.GetByIdempotency(It.IsAny<Guid>()))
                .ReturnsAsync((TransactionEntity?)null);

            _accountRepositoryMock
                .Setup(r => r.GetByAccountNumber(accountNumber))
                .ReturnsAsync(account);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<AccountBlockedError>(() => _useCase.ExecuteAsync(dto));

            Assert.NotNull(exception);
            Assert.Equal(403, exception.StatusCode);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldReturnExistingTransactionId_WhenIdempotencyKeyAlreadyExists() {
            // Arrange
            var existingTransactionId = Guid.NewGuid();
            var idempotencyKey = Guid.NewGuid();
            var accountNumber = "12345-6";
            var existingTransaction = new TransactionEntity {
                Id = existingTransactionId,
                IdempotencyKey = idempotencyKey,
                Amount = 100
            };

            var dto = new TransactionDto(accountNumber, 100, 1, idempotencyKey.ToString());

            _transactionRepositoryMock
                .Setup(r => r.GetByIdempotency(idempotencyKey))
                .ReturnsAsync(existingTransaction);

            // Act
            var resultId = await _useCase.ExecuteAsync(dto);

            // Assert
            Assert.Equal(existingTransactionId.ToString(), resultId);

            // Garante que nenhuma operação de escrita ou commit foi acionada
            _accountRepositoryMock.Verify(r => r.GetByAccountNumber(It.IsAny<string>()), Times.Never);

            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Never);
        }
    }
}

