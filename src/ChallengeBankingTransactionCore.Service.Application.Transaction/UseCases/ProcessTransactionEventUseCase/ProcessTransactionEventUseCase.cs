using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Utils;

namespace ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.ProcessTransactionEventUseCase {
    public class ProcessTransactionEventUseCase : IProcessTransactionEventUseCase {
        private readonly IDailyConsolidatedBalanceRepository _dailyBalanceRepository;
        private readonly IConsolidatedCachePort _cachePort;
        private readonly IAppLogger<ProcessTransactionEventUseCase> _logger;

        public ProcessTransactionEventUseCase(
            IDailyConsolidatedBalanceRepository dailyBalanceRepository,
            IConsolidatedCachePort cachePort,
            IAppLogger<ProcessTransactionEventUseCase> logger
        ) {
            _dailyBalanceRepository = dailyBalanceRepository;
            _cachePort = cachePort;
            _logger = logger;
        }

        public async Task<bool> ExecuteAsync(TransactionCreatedEventEntity transactionEvent, CancellationToken cancellationToken = default) {
            if (transactionEvent == null
                || transactionEvent.TransactionId == Guid.Empty
                || transactionEvent.AccountId == Guid.Empty
                || string.IsNullOrWhiteSpace(transactionEvent.AccountNumber)
                || transactionEvent.Amount <= 0) {
                throw new ConsolidateStatementError();
            }

            var maskedAccount = SensitiveDataMasker.MaskAccountNumber(transactionEvent.AccountNumber);

            var alreadyProcessed = await _dailyBalanceRepository.HasEventBeenProcessedAsync(transactionEvent.TransactionId, cancellationToken);
            if (alreadyProcessed) {
                _logger.LogInformation("Event {TransactionId} for account {MaskedAccount} already processed in Inbox. Ignoring duplicate.", transactionEvent.TransactionId, maskedAccount);
                return false;
            }

            var updated = await _dailyBalanceRepository.UpsertDailyBalanceWithInboxAsync(transactionEvent, cancellationToken);
            if (!updated) {
                _logger.LogInformation("Event {TransactionId} for account {MaskedAccount} already processed concurrently in Inbox. Ignoring duplicate.", transactionEvent.TransactionId, maskedAccount);
                return false;
            }

            await _cachePort.InvalidateByAccountAsync(transactionEvent.AccountNumber, cancellationToken);
            _logger.LogInformation("Consolidated daily balance for Account {MaskedAccount} on event {TransactionId}.", maskedAccount, transactionEvent.TransactionId);
            return true;
        }
    }
}
