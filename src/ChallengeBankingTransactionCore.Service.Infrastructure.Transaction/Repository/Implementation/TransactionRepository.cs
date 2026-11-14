using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repository.Implementation {
    public class TransactionRepository : ITransactionRepository {
        public Task<TransactionEntity> GetByIdempotency(string idempotencyKey) {
            throw new NotImplementedException();
        }

        public Task<string> ProcessTransactionAsync(string accountNumberId, string description, decimal amount, decimal balanceAfter) {
            throw new NotImplementedException();
        }
    }
}
