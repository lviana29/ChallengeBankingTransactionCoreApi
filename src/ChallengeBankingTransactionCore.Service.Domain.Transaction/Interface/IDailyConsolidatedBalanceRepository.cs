using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;

namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface {
    public interface IDailyConsolidatedBalanceRepository {
        Task<bool> HasEventBeenProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
        Task<bool> UpsertDailyBalanceWithInboxAsync(TransactionCreatedEventEntity transactionEvent, CancellationToken cancellationToken = default);
        Task<DailyConsolidatedBalanceEntity?> GetDailyBalanceAsync(Guid accountId, DateTime balanceDate, CancellationToken cancellationToken = default);
        Task<List<DailyConsolidatedBalanceEntity>> GetDailyBalancesInRangeAsync(Guid accountId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    }
}
