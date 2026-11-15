using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;

namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface {
    public interface ITransactionRepository {
        Guid ProcessTransactionAsync(Guid accountNumberId, string description, decimal amount, Guid idempotencyKey, decimal balanceAfter);
        Task<TransactionEntity?> GetByIdempotency(Guid idempotencyKey);
    }
}
