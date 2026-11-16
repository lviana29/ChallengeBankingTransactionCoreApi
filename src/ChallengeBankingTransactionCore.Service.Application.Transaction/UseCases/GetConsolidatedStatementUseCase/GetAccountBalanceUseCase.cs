using ChallengeBankingTransactionCore.Service.Application.Transaction.Dto;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;

namespace ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.GetConsolidatedStatementUseCase {
    public class GetConsolidatedStatementUseCase : IGetConsolidatedStatementUseCase {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        public GetConsolidatedStatementUseCase(IAccountRepository accountRepository, ITransactionRepository transactionRepository) {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
        }

        public async Task<ConsolidatedStatementDto> ExecuteAsync(ConsolidatedTransactionsFiltersDto filtersDto) {
            if (String.IsNullOrEmpty(filtersDto.AccountNumber)) throw new InvalidAccountNumber();

            var account = await _accountRepository.GetByAccountNumber(filtersDto.AccountNumber);
            if (String.IsNullOrEmpty(account.AccountNumber)) throw new AccountNotFoundError();
            var startDate = filtersDto.StartDate;
            var endDate = filtersDto.EndDate;

            if (startDate == null || endDate == null) {
                endDate = DateTime.UtcNow;
                startDate = DateTime.UtcNow.AddDays(-10);
            }

            var transactions = await _transactionRepository.GetRangeByAccountNumber(account.Id, startDate, endDate);

            var consolidateTransactions = new List<ConsolidatedTransactionsDto>();

            transactions.ForEach(t => {
                var consolidateT = new ConsolidatedTransactionsDto(t.Amount, t.CreatedAt, t.Type.Title, t.BalanceAfter);
                consolidateTransactions.Add(consolidateT);
            });

            return new ConsolidatedStatementDto(filtersDto.AccountNumber, account.Balance, consolidateTransactions);
        }
    }
}
