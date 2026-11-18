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
        private readonly Mock<IConsolidatedCachePort> _cachePortMock;
        private readonly Mock<IDailyConsolidatedBalanceRepository> _dailyBalanceRepositoryMock;
        private readonly GetConsolidatedStatementUseCase _useCase;

        public GetConsolidatedStatementUseCaseTests() {
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _transactionRepositoryMock = new Mock<ITransactionRepository>();
            _cachePortMock = new Mock<IConsolidatedCachePort>();
            _dailyBalanceRepositoryMock = new Mock<IDailyConsolidatedBalanceRepository>();
            _useCase = new GetConsolidatedStatementUseCase(
                _accountRepositoryMock.Object,
                _transactionRepositoryMock.Object,
                _cachePortMock.Object,
                _dailyBalanceRepositoryMock.Object
            );
        }

        [Theory(DisplayName = "Should throw InvalidAccountNumber when AccountNumber is null, empty or whitespace")]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ExecuteAsync_ShouldThrowInvalidAccountNumber_WhenAccountNumberIsNullOrEmpty(string invalidAccountNumber) {
            // Arrange
            var filters = new ConsolidatedTransactionsFiltersDto(invalidAccountNumber, null, null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidAccountNumber>(() => _useCase.ExecuteAsync(filters));
            Assert.Equal(422, ex.StatusCode);
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
            var startDate = DateTime.UtcNow.Date.AddDays(-5);
            var endDate = DateTime.UtcNow;
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
                    CreatedAt = DateTime.UtcNow.Date.AddDays(-2),
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
            Assert.Equal(1500.00m, result.consolidatedBalance);
            Assert.Equal(1500.00m, result.currentBalance);
            Assert.Equal(endDate, result.referenceDate);
            Assert.Single(result.consolidatedTransactions);

            var transactionDto = result.consolidatedTransactions[0];
            Assert.Equal(100.00m, transactionDto.value);
            Assert.Equal("Credit", transactionDto.transactionType);
            Assert.Equal(1500.00m, transactionDto.balanceAfter);
        }

        [Fact(DisplayName = "Should throw InvalidDateRangeException when StartDate is greater than EndDate")]
        public async Task ExecuteAsync_ShouldThrowInvalidDateRangeException_WhenStartDateGreaterThanEndDate() {
            // Arrange
            var accountNumber = "12345-6";
            var accountId = Guid.NewGuid();
            var startDate = new DateTime(2026, 11, 20);
            var endDate = new DateTime(2026, 11, 10);
            var filters = new ConsolidatedTransactionsFiltersDto(accountNumber, startDate, endDate);

            _accountRepositoryMock
                .Setup(repo => repo.GetByAccountNumber(accountNumber))
                .ReturnsAsync(new AccountEntity { Id = accountId, AccountNumber = accountNumber, Balance = 1000m, IsActive = true });

            // Act & Assert
            await Assert.ThrowsAsync<InvalidDateRangeException>(() => _useCase.ExecuteAsync(filters));
        }

        [Fact(DisplayName = "Should project 10 days backward when only EndDate is provided")]
        public async Task ExecuteAsync_ShouldApply10DaysBackward_WhenOnlyEndDateIsProvided() {
            // Arrange
            var accountNumber = "12345-6";
            var accountId = Guid.NewGuid();
            var endDate = new DateTime(2026, 10, 31);
            var expectedStartDate = endDate.AddDays(-10);
            var filters = new ConsolidatedTransactionsFiltersDto(accountNumber, null, endDate);

            var accountEntity = new AccountEntity {
                Id = accountId,
                AccountNumber = accountNumber,
                Balance = 2000.00m,
                IsActive = true
            };

            _accountRepositoryMock
                .Setup(repo => repo.GetByAccountNumber(accountNumber))
                .ReturnsAsync(accountEntity);

            _transactionRepositoryMock
                .Setup(repo => repo.GetRangeByAccountNumber(accountId, expectedStartDate, endDate))
                .ReturnsAsync(new List<TransactionEntity>());

            _transactionRepositoryMock
                .Setup(repo => repo.GetBalanceAtDateAsync(accountId, endDate))
                .ReturnsAsync(1850.00m);

            // Act
            var result = await _useCase.ExecuteAsync(filters);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(accountNumber, result.accountNumber);
            Assert.Equal(1850.00m, result.consolidatedBalance);
            Assert.Equal(2000.00m, result.currentBalance);
            Assert.Equal(endDate, result.referenceDate);

            _transactionRepositoryMock.Verify(repo => repo.GetRangeByAccountNumber(accountId, expectedStartDate, endDate), Times.Once);
            _transactionRepositoryMock.Verify(repo => repo.GetBalanceAtDateAsync(accountId, endDate), Times.Once);
        }

        [Fact(DisplayName = "Should project 10 days forward when only StartDate is provided")]
        public async Task ExecuteAsync_ShouldApply10DaysForward_WhenOnlyStartDateIsProvided() {
            // Arrange
            var accountNumber = "12345-6";
            var accountId = Guid.NewGuid();
            var startDate = new DateTime(2026, 10, 1);
            var expectedEndDate = startDate.AddDays(10);
            var filters = new ConsolidatedTransactionsFiltersDto(accountNumber, startDate, null);

            var accountEntity = new AccountEntity {
                Id = accountId,
                AccountNumber = accountNumber,
                Balance = 3000.00m,
                IsActive = true
            };

            _accountRepositoryMock
                .Setup(repo => repo.GetByAccountNumber(accountNumber))
                .ReturnsAsync(accountEntity);

            _transactionRepositoryMock
                .Setup(repo => repo.GetRangeByAccountNumber(accountId, startDate, expectedEndDate))
                .ReturnsAsync(new List<TransactionEntity>());

            _transactionRepositoryMock
                .Setup(repo => repo.GetBalanceAtDateAsync(accountId, expectedEndDate))
                .ReturnsAsync(1200.00m);

            // Act
            var result = await _useCase.ExecuteAsync(filters);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(accountNumber, result.accountNumber);
            Assert.Equal(1200.00m, result.consolidatedBalance);
            Assert.Equal(3000.00m, result.currentBalance);
            Assert.Equal(expectedEndDate, result.referenceDate);

            _transactionRepositoryMock.Verify(repo => repo.GetRangeByAccountNumber(accountId, startDate, expectedEndDate), Times.Once);
        }

        [Fact(DisplayName = "Should return historical balance at cut-off date when EndDate is in the past")]
        public async Task ExecuteAsync_ShouldReturnHistoricalBalance_WhenEndDateIsRetroactive() {
            // Arrange
            var accountNumber = "12345-6";
            var accountId = Guid.NewGuid();
            var startDate = new DateTime(2026, 9, 1);
            var endDate = new DateTime(2026, 9, 15);
            var filters = new ConsolidatedTransactionsFiltersDto(accountNumber, startDate, endDate);

            var accountEntity = new AccountEntity {
                Id = accountId,
                AccountNumber = accountNumber,
                Balance = 5000.00m,
                IsActive = true
            };

            var transactions = new List<TransactionEntity> {
                new TransactionEntity {
                    Id = Guid.NewGuid(),
                    AccountId = accountId.ToString(),
                    Amount = 250.00m,
                    BalanceAfter = 3500.00m,
                    CreatedAt = new DateTime(2026, 9, 10),
                    Type = new TransactionTypeEntity { Title = "Credit" }
                }
            };

            _accountRepositoryMock
                .Setup(repo => repo.GetByAccountNumber(accountNumber))
                .ReturnsAsync(accountEntity);

            _transactionRepositoryMock
                .Setup(repo => repo.GetRangeByAccountNumber(accountId, startDate, endDate))
                .ReturnsAsync(transactions);

            _transactionRepositoryMock
                .Setup(repo => repo.GetBalanceAtDateAsync(accountId, endDate))
                .ReturnsAsync(3500.00m);

            // Act
            var result = await _useCase.ExecuteAsync(filters);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(accountNumber, result.accountNumber);
            Assert.Equal(3500.00m, result.consolidatedBalance);
            Assert.Equal(5000.00m, result.currentBalance);
            Assert.Equal(endDate, result.referenceDate);
            Assert.Single(result.consolidatedTransactions);

            _transactionRepositoryMock.Verify(repo => repo.GetBalanceAtDateAsync(accountId, endDate), Times.Once);
        }

        [Fact(DisplayName = "Should return directly from cache without querying database on Cache Hit")]
        public async Task ExecuteAsync_ShouldReturnFromCacheWithoutCallingRepositories_OnCacheHit() {
            // Arrange
            var accountNumber = "12345-6";
            var filters = new ConsolidatedTransactionsFiltersDto(accountNumber, null, null);
            var cachedDto = new ConsolidatedStatementDto(
                accountNumber,
                2500.00m,
                2500.00m,
                DateTime.UtcNow,
                new List<ConsolidatedTransactionsDto>()
            );

            _cachePortMock
                .Setup(c => c.GetAsync<ConsolidatedStatementDto>(It.Is<string>(k => k.Contains("12345-6") && k.EndsWith("default-10d")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(cachedDto);

            // Act
            var result = await _useCase.ExecuteAsync(filters);

            // Assert
            Assert.Same(cachedDto, result);
            _accountRepositoryMock.Verify(r => r.GetByAccountNumber(It.IsAny<string>()), Times.Never);
            _transactionRepositoryMock.Verify(r => r.GetRangeByAccountNumber(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Never);
            _cachePortMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<ConsolidatedStatementDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact(DisplayName = "Should query Read Model and populate cache on Cache Miss")]
        public async Task ExecuteAsync_ShouldUseDailyConsolidatedReadModelAndPopulateCache_OnCacheMiss() {
            // Arrange
            var accountNumber = "12345-6";
            var accountId = Guid.NewGuid();
            var startDate = new DateTime(2026, 9, 1);
            var endDate = new DateTime(2026, 9, 10); // Midnight (TimeSpan.Zero)
            var filters = new ConsolidatedTransactionsFiltersDto(accountNumber, startDate, endDate);

            _cachePortMock
                .Setup(c => c.GetAsync<ConsolidatedStatementDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ConsolidatedStatementDto?)null);

            _accountRepositoryMock
                .Setup(r => r.GetByAccountNumber(accountNumber))
                .ReturnsAsync(new AccountEntity {
                    Id = accountId,
                    AccountNumber = accountNumber,
                    Balance = 5000.00m,
                    IsActive = true
                });

            _transactionRepositoryMock
                .Setup(r => r.GetRangeByAccountNumber(accountId, startDate, endDate))
                .ReturnsAsync(new List<TransactionEntity>());

            _dailyBalanceRepositoryMock
                .Setup(d => d.GetDailyBalanceAsync(accountId, endDate.Date, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DailyConsolidatedBalanceEntity {
                    AccountId = accountId,
                    AccountNumber = accountNumber,
                    BalanceDate = endDate.Date,
                    ClosingBalance = 4100.00m
                });

            // Act
            var result = await _useCase.ExecuteAsync(filters);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(4100.00m, result.consolidatedBalance);
            Assert.Equal(5000.00m, result.currentBalance);
            _transactionRepositoryMock.Verify(r => r.GetBalanceAtDateAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
            _cachePortMock.Verify(c => c.SetAsync(
                It.Is<string>(k => k.Contains("12345-6")),
                It.IsAny<ConsolidatedStatementDto>(),
                TimeSpan.FromSeconds(60),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact(DisplayName = "Should return 0.00m when retroactive balance is null and Read Model is empty")]
        public async Task ExecuteAsync_ShouldReturnZeroConsolidatedBalance_WhenNoHistoricalTransactionsExist() {
            // Arrange
            var accountNumber = "12345-6";
            var accountId = Guid.NewGuid();
            var startDate = new DateTime(2026, 1, 1);
            var endDate = new DateTime(2026, 1, 5);
            var filters = new ConsolidatedTransactionsFiltersDto(accountNumber, startDate, endDate);

            _accountRepositoryMock
                .Setup(r => r.GetByAccountNumber(accountNumber))
                .ReturnsAsync(new AccountEntity {
                    Id = accountId,
                    AccountNumber = accountNumber,
                    Balance = 1000.00m,
                    IsActive = true
                });

            _transactionRepositoryMock
                .Setup(r => r.GetRangeByAccountNumber(accountId, startDate, endDate))
                .ReturnsAsync(new List<TransactionEntity>());

            _transactionRepositoryMock
                .Setup(r => r.GetBalanceAtDateAsync(accountId, endDate))
                .ReturnsAsync((decimal?)null);

            // Act
            var result = await _useCase.ExecuteAsync(filters);

            // Assert
            Assert.Equal(0.00m, result.consolidatedBalance);
        }
    }
}
