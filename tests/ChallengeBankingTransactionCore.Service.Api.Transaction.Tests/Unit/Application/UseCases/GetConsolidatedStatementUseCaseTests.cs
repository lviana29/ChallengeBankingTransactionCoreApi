using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChallengeBankingTransactionCore.Service.Application.Transaction.Dto;
using ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.GetConsolidatedStatementUseCase;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using Moq;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Application.UseCases {
    public class GetConsolidatedStatementUseCaseTests {
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<ITransactionRepository> _transactionRepositoryMock;
        private readonly GetConsolidatedStatementUseCase _useCase;

        public GetConsolidatedStatementUseCaseTests() {
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _transactionRepositoryMock = new Mock<ITransactionRepository>();
            _useCase = new GetConsolidatedStatementUseCase(_accountRepositoryMock.Object, _transactionRepositoryMock.Object);
        }

        [Fact(DisplayName = "Should throw InvalidAccountNumber when AccountNumber is null or empty")]
        public async Task ExecuteAsync_ShouldThrowInvalidAccountNumber_WhenAccountNumberIsNullOrEmpty() {
            // Arrange
            var filters = new ConsolidatedTransactionsFiltersDto(string.Empty, null, null);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidAccountNumber>(() => _useCase.ExecuteAsync(filters));
        }

        [Fact(DisplayName = "Should throw AccountNotFoundError when account does not exist")]
        public async Task ExecuteAsync_ShouldThrowAccountNotFoundError_WhenAccountDoesNotExist() {
            // Arrange
            var accountNumber = "12345-9";
            var filters = new ConsolidatedTransactionsFiltersDto(accountNumber, null, null);


            _accountRepositoryMock
                .Setup(repo => repo.GetByAccountNumber(accountNumber));

            // Act & Assert
            await Assert.ThrowsAsync<AccountNotFoundError>(() => _useCase.ExecuteAsync(filters));
        }

        [Fact(DisplayName = "Should apply default date range fallback when dates are null")]
        public async Task ExecuteAsync_ShouldUseDefaultDates_WhenDatesAreNull() {
            // Arrange
            var accountNumber = "12345-6";
            var accountId = Guid.NewGuid();
            var filters = new ConsolidatedTransactionsFiltersDto(accountNumber, null, null);

            var accountEntity = new AccountEntity {
                Id = accountId,
                AccountNumber = accountNumber,
                Balance = 500.00m,
                IsActive = true
            };

            _accountRepositoryMock
                .Setup(repo => repo.GetByAccountNumber(accountNumber))
                .ReturnsAsync(accountEntity);

            _transactionRepositoryMock
                .Setup(repo => repo.GetRangeByAccountNumber(accountId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new List<TransactionEntity>());

            // Act
            var result = await _useCase.ExecuteAsync(filters);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(accountNumber, result.accountNumber);
            Assert.Empty(result.consolidatedTransactions);

            _transactionRepositoryMock.Verify(repo => repo.GetRangeByAccountNumber(
                accountId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>()
            ), Times.Once);
        }

        [Fact(DisplayName = "Should return consolidated statement successfully when valid filters and transactions exist")]
        public async Task ExecuteAsync_ShouldReturnConsolidatedStatement_WhenValidFiltersAreProvided() {
            // Arrange
            var accountNumber = "12345-6";
            var accountId = Guid.NewGuid();
            var startDate = new DateTime(2026, 11, 1);
            var endDate = new DateTime(2026, 11, 16);
            var filters = new ConsolidatedTransactionsFiltersDto(accountNumber, startDate, endDate);

            var accountEntity = new AccountEntity {
                Id = accountId,
                AccountNumber = accountNumber,
                Balance = 1500.00m,
                IsActive = true
            };

            var transactions = new List<TransactionEntity> {
            new TransactionEntity {
                Id = Guid.NewGuid(),
                AccountId = accountId.ToString(),
                Amount = 100.00m,
                BalanceAfter = 1500.00m,
                CreatedAt = new DateTime(2026, 11, 5),
                Type = new TransactionTypeEntity { Title = "Credit" }
            }
        };

            _accountRepositoryMock
                .Setup(repo => repo.GetByAccountNumber(accountNumber))
                .ReturnsAsync(accountEntity);

            _transactionRepositoryMock
                .Setup(repo => repo.GetRangeByAccountNumber(accountId, startDate, endDate))
                .ReturnsAsync(transactions);

            // Act
            var result = await _useCase.ExecuteAsync(filters);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(accountNumber, result.accountNumber);
            Assert.Equal(1500.00m, result.currentBalance);
            Assert.Single(result.consolidatedTransactions);

            var transactionDto = result.consolidatedTransactions[0];
            Assert.Equal(100.00m, transactionDto.value);
            Assert.Equal("Credit", transactionDto.transactionType);
            Assert.Equal(1500.00m, transactionDto.balanceAfter);
        }
    }
}
