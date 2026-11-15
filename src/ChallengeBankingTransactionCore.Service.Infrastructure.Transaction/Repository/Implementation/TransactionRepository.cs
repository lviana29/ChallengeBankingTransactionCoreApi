using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repositories;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repository.Implementation {
    public class TransactionRepository : ITransactionRepository {
        private readonly AppDbContext _context;
        private readonly IGenericRepository<Model.Transaction> _repository;
        public TransactionRepository(AppDbContext context, IGenericRepository<Model.Transaction> repository) {
            _context = context;
            _repository = repository;
        }
        public async Task<TransactionEntity?> GetByIdempotency(Guid idempotencyKey) {
            var entity = await _context.Transactions
            .Include(x => x.Type)
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey);
            return entity == null ? null : entity.Adapt<TransactionEntity>();
        }

        public Guid ProcessTransactionAsync(Guid accountNumberId, string description, decimal amount, Guid idempotencyKey, decimal balanceAfter) {
            var entity = new Model.Transaction {
                AccountId = accountNumberId,
                Description = description,
                Amount = amount,
                BalanceAfter = balanceAfter,
                IdempotencyKey = idempotencyKey
            };
            _repository.Create(entity, false);
            return entity.Id;
        }
    }
}
