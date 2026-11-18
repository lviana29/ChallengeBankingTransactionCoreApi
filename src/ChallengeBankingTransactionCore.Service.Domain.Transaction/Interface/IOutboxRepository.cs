using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;

namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface {
    public interface IOutboxRepository {
        void AddOutboxMessage(OutboxMessageEntity message);
        Task<List<OutboxMessageEntity>> GetPendingMessagesAsync(int batchSize, int maxRetries, CancellationToken cancellationToken = default);
        Task MarkAsProcessedAsync(Guid messageId, DateTime processedAt, CancellationToken cancellationToken = default);
        Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default);
    }
}
