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
        private readonly Mock<IMessagePublisherPort> _messagePublisher;

        public ProcessTransactionUseCaseTests() {
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _transactionRepositoryMock = new Mock<ITransactionRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _messagePublisher = new Mock<IMessagePublisherPort>();

            _useCase = new ProcessTransactionUseCase(
                _accountRepositoryMock.Object,
                _transactionRepositoryMock.Object,
                _unitOfWorkMock.Object,
                _messagePublisher.Object
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

        [Fact]
        public async Task ExecuteAsync_ShouldRetryAndSucceed_WhenConcurrencyConflictOccursOnFirstAttempt() {
            // Arrange
            var accountId = Guid.NewGuid();
            var accountNumber = "12345-6";
            var idempotencyKey = Guid.NewGuid();

            var accountAttempt1 = new AccountEntity {
                Id = accountId,
                AccountNumber = accountNumber,
                Balance = 1000m,
                IsActive = true
            };

            var accountAttempt2 = new AccountEntity {
                Id = accountId,
                AccountNumber = accountNumber,
                Balance = 800m,
                IsActive = true
            };

            var dto = new TransactionDto(accountNumber, 200, 2, idempotencyKey.ToString()); // Débito 200

            _transactionRepositoryMock
                .Setup(r => r.GetByIdempotency(idempotencyKey))
                .ReturnsAsync((TransactionEntity?)null);

            var accountCalls = 0;
            _accountRepositoryMock
                .Setup(r => r.GetByAccountNumber(accountNumber))
                .ReturnsAsync(() => {
                    accountCalls++;
                    return accountCalls == 1 ? accountAttempt1 : accountAttempt2;
                });

            var commitCalls = 0;
            _unitOfWorkMock
                .Setup(u => u.CommitAsync())
                .Returns(() => {
                    commitCalls++;
                    if (commitCalls == 1) {
                        throw new AccountConcurrencyConflictException();
                    }
                    return Task.FromResult(true);
                });

            // Act
            var resultId = await _useCase.ExecuteAsync(dto);

            // Assert
            Assert.NotNull(resultId);
            Assert.True(Guid.TryParse(resultId, out _));
            Assert.Equal(2, commitCalls);
            Assert.Equal(2, accountCalls);

            // No segundo commit, saldo recalculado de 800 - 200 = 600
            Assert.Equal(600m, accountAttempt2.Balance);
            _accountRepositoryMock.Verify(r => r.GetByAccountNumber(accountNumber), Times.Exactly(2));
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Exactly(2));
        }

        [Fact]
        public async Task ExecuteAsync_ShouldThrowAccountConcurrencyConflictException_WhenMaxRetriesExceeded() {
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

            var dto = new TransactionDto(accountNumber, 100, 1, idempotencyKey.ToString());

            _transactionRepositoryMock
                .Setup(r => r.GetByIdempotency(idempotencyKey))
                .ReturnsAsync((TransactionEntity?)null);

            _accountRepositoryMock
                .Setup(r => r.GetByAccountNumber(accountNumber))
                .ReturnsAsync(account);

            _unitOfWorkMock
                .Setup(u => u.CommitAsync())
                .ThrowsAsync(new AccountConcurrencyConflictException());

            // Act & Assert
            var exception = await Assert.ThrowsAsync<AccountConcurrencyConflictException>(() => _useCase.ExecuteAsync(dto));

            Assert.NotNull(exception);
            Assert.Equal(409, exception.StatusCode);
            Assert.Equal("CORE_BANKING_010", exception.ErrorCode);
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Exactly(3));
        }

        [Fact]
        public async Task ExecuteAsync_ShouldThrowInvalidValueToDebitError_WhenBalanceBecomesInsufficientAfterConcurrencyRetry() {
            // Arrange
            var accountId = Guid.NewGuid();
            var accountNumber = "12345-6";
            var idempotencyKey = Guid.NewGuid();

            var accountAttempt1 = new AccountEntity {
                Id = accountId,
                AccountNumber = accountNumber,
                Balance = 500m,
                IsActive = true
            };

            var accountAttempt2 = new AccountEntity {
                Id = accountId,
                AccountNumber = accountNumber,
                Balance = 100m, // Saldo caiu para 100 por concorrência
                IsActive = true
            };

            var dto = new TransactionDto(accountNumber, 300, 2, idempotencyKey.ToString()); // Débito 300

            _transactionRepositoryMock
                .Setup(r => r.GetByIdempotency(idempotencyKey))
                .ReturnsAsync((TransactionEntity?)null);

            var accountCalls = 0;
            _accountRepositoryMock
                .Setup(r => r.GetByAccountNumber(accountNumber))
                .ReturnsAsync(() => {
                    accountCalls++;
                    return accountCalls == 1 ? accountAttempt1 : accountAttempt2;
                });

            _unitOfWorkMock
                .Setup(u => u.CommitAsync())
                .ThrowsAsync(new AccountConcurrencyConflictException());

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidValueToDebitError>(() => _useCase.ExecuteAsync(dto));

            Assert.NotNull(exception);
            Assert.Equal(422, exception.StatusCode);
            Assert.Equal(2, accountCalls);
            // Commit foi chamado apenas 1 vez (na 1ª tentativa); na 2ª tentativa falhou antes do commit
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-10)]
        [InlineData(-0.01)]
        public async Task ExecuteAsync_ShouldThrowInvalidTransactionValueError_WhenValueIsZeroOrNegative(decimal invalidValue) {
            // Arrange
            var dto = new TransactionDto("12345-6", invalidValue, 1, Guid.NewGuid().ToString());

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidTransactionValueError>(() => _useCase.ExecuteAsync(dto));
            Assert.Equal(422, ex.StatusCode);
            _accountRepositoryMock.Verify(r => r.GetByAccountNumber(It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ExecuteAsync_ShouldThrowInvalidAccountNumber_WhenAccountNumberIsNullOrWhitespace(string invalidAccount) {
            // Arrange
            var dto = new TransactionDto(invalidAccount, 100m, 1, Guid.NewGuid().ToString());

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidAccountNumber>(() => _useCase.ExecuteAsync(dto));
            Assert.Equal(422, ex.StatusCode);
            _accountRepositoryMock.Verify(r => r.GetByAccountNumber(It.IsAny<string>()), Times.Never);
        }
    }
}

