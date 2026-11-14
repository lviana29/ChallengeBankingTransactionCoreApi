using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;

namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface {
    public interface ITransactionRepository {
        Task<string> ProcessTransactionAsync(string accountNumberId, string description, decimal amount, decimal balanceAfter);
        Task<TransactionEntity> GetByIdempotency(string idempotencyKey);
    }
}
