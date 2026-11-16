using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;

namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface {
    public interface ITransactionRepository {
        Guid ProcessTransactionAsync(Guid accountNumberId, string description, decimal amount, Guid idempotencyKey, decimal balanceAfter, Guid transactionTypeId);
        Task<TransactionEntity?> GetByIdempotency(Guid idempotencyKey);
        Task<List<TransactionEntity>> GetRangeByAccountNumber(Guid accountNumberId, DateTime? startDate, DateTime? endDate);
    }
}
