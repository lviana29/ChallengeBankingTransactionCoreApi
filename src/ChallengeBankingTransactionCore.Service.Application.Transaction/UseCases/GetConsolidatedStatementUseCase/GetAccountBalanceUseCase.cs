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
            if (string.IsNullOrEmpty(filtersDto.AccountNumber)) throw new InvalidAccountNumber();

            var account = await _accountRepository.GetByAccountNumber(filtersDto.AccountNumber);
            if (account == null) throw new AccountNotFoundError();
            var startDate = filtersDto.StartDate;
            var endDate = filtersDto.EndDate;

            DateTime effectiveStartDate;
            DateTime effectiveEndDate;

            // 1. Resolução inteligente da janela de segurança de no máximo 10 dias
            if (startDate.HasValue && endDate.HasValue) {
                effectiveStartDate = startDate.Value;
                effectiveEndDate = endDate.Value;
            }
            else if (startDate.HasValue && !endDate.HasValue) {
                effectiveStartDate = startDate.Value;
                var projectedEnd = effectiveStartDate.AddDays(10);
                effectiveEndDate = projectedEnd > DateTime.UtcNow ? DateTime.UtcNow : projectedEnd;
            }
            else if (!startDate.HasValue && endDate.HasValue) {
                effectiveEndDate = endDate.Value;
                effectiveStartDate = effectiveEndDate.AddDays(-10);
            }
            else {
                effectiveEndDate = DateTime.UtcNow;
                effectiveStartDate = effectiveEndDate.AddDays(-10);
            }

            // 2. Validação de consistência temporal
            if (effectiveStartDate > effectiveEndDate) {
                throw new InvalidDateRangeException();
            }

            // 3. Busca transações dentro do intervalo protegido
            var transactions = await _transactionRepository.GetRangeByAccountNumber(account.Id, effectiveStartDate, effectiveEndDate);

            // 4. Saldo consolidado no momento da data de corte (effectiveEndDate)
            decimal consolidatedBalance;
            if (effectiveEndDate.Date < DateTime.UtcNow.Date) {
                var historicalBalance = await _transactionRepository.GetBalanceAtDateAsync(account.Id, effectiveEndDate);
                consolidatedBalance = historicalBalance ?? 0.00m;
            }
            else {
                consolidatedBalance = account.Balance;
            }

            var consolidateTransactions = new List<ConsolidatedTransactionsDto>();

            transactions.ForEach(t => {
                var consolidateT = new ConsolidatedTransactionsDto(t.Amount, t.CreatedAt, t.Type.Title, t.BalanceAfter);
                consolidateTransactions.Add(consolidateT);
            });

            return new ConsolidatedStatementDto(filtersDto.AccountNumber, consolidatedBalance, account.Balance, effectiveEndDate, consolidateTransactions);
        }
    }
}
