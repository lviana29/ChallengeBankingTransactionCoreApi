using ChallengeBankingTransactionCore.Service.Application.Transaction.Dto;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;

namespace ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.GetConsolidatedStatementUseCase {
    public class GetConsolidatedStatementUseCase : IGetConsolidatedStatementUseCase {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IConsolidatedCachePort? _cachePort;
        private readonly IDailyConsolidatedBalanceRepository? _dailyBalanceRepository;

        public GetConsolidatedStatementUseCase(
            IAccountRepository accountRepository,
            ITransactionRepository transactionRepository,
            IConsolidatedCachePort? cachePort = null,
            IDailyConsolidatedBalanceRepository? dailyBalanceRepository = null
        ) {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _cachePort = cachePort;
            _dailyBalanceRepository = dailyBalanceRepository;
        }

        public async Task<ConsolidatedStatementDto> ExecuteAsync(ConsolidatedTransactionsFiltersDto filtersDto) {
            if (string.IsNullOrWhiteSpace(filtersDto.AccountNumber)) throw new InvalidAccountNumber();

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

            // 3. Consulta Cache-Aside (L1/L2) antes de acionar o banco relacional
            var keySuffix = (!startDate.HasValue && !endDate.HasValue)
                ? "default-10d"
                : $"{effectiveStartDate:yyyyMMddHHmmss}:{effectiveEndDate:yyyyMMddHHmmss}";
            var cacheKey = $"consolidated:v1:{filtersDto.AccountNumber}:{keySuffix}";

            if (_cachePort != null) {
                var cachedStatement = await _cachePort.GetAsync<ConsolidatedStatementDto>(cacheKey);
                if (cachedStatement != null) {
                    return cachedStatement;
                }
            }

            var account = await _accountRepository.GetByAccountNumber(filtersDto.AccountNumber);
            if (account == null) throw new AccountNotFoundError();

            // 4. Busca transações dentro do intervalo protegido
            var transactions = await _transactionRepository.GetRangeByAccountNumber(account.Id, effectiveStartDate, effectiveEndDate);

            // 5. Saldo consolidado no momento da data de corte (effectiveEndDate)
            decimal consolidatedBalance;
            if (effectiveEndDate.Date < DateTime.UtcNow.Date) {
                var dailyReadModel = _dailyBalanceRepository != null
                    ? await _dailyBalanceRepository.GetDailyBalanceAsync(account.Id, effectiveEndDate.Date)
                    : null;

                if (dailyReadModel != null && effectiveEndDate.TimeOfDay == TimeSpan.Zero) {
                    consolidatedBalance = dailyReadModel.ClosingBalance;
                }
                else {
                    var historicalBalance = await _transactionRepository.GetBalanceAtDateAsync(account.Id, effectiveEndDate);
                    consolidatedBalance = historicalBalance ?? 0.00m;
                }
            }
            else {
                var todayReadModel = _dailyBalanceRepository != null
                    ? await _dailyBalanceRepository.GetDailyBalanceAsync(account.Id, effectiveEndDate.Date)
                    : null;

                consolidatedBalance = todayReadModel != null ? todayReadModel.ClosingBalance : account.Balance;
            }

            var consolidateTransactions = new List<ConsolidatedTransactionsDto>();

            transactions.ForEach(t => {
                var consolidateT = new ConsolidatedTransactionsDto(t.Amount, t.CreatedAt, t.Type.Title, t.BalanceAfter);
                consolidateTransactions.Add(consolidateT);
            });

            var statementDto = new ConsolidatedStatementDto(
                filtersDto.AccountNumber,
                consolidatedBalance,
                account.Balance,
                effectiveEndDate,
                consolidateTransactions
            );

            if (_cachePort != null) {
                await _cachePort.SetAsync(cacheKey, statementDto, TimeSpan.FromSeconds(60));
            }

            return statementDto;
        }
    }
}
