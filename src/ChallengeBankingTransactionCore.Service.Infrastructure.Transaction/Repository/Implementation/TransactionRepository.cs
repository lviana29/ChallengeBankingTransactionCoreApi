using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model;
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

        public async Task<List<TransactionEntity>> GetRangeByAccountNumber(Guid accountNumberId, DateTime? startDate, DateTime? endDate) {
            var query = _context.Transactions
                .Include(x => x.Type)
                .Where(x => x.AccountId == accountNumberId);

            if (startDate.HasValue) {
                query = query.Where(x => x.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue) {
                var adjustedEndDate = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(x => x.CreatedAt <= adjustedEndDate);
            }
            var entities = await query.ToListAsync();

            return entities == null ? new List<TransactionEntity>() : entities.Adapt<List<TransactionEntity>>();
        }

        public async Task<decimal?> GetBalanceAtDateAsync(Guid accountNumberId, DateTime cutOffDate) {
            var adjustedDate = cutOffDate.TimeOfDay == TimeSpan.Zero
                ? cutOffDate.Date.AddDays(1).AddTicks(-1)
                : cutOffDate;

            var transaction = await _context.Transactions
                .Where(x => x.AccountId == accountNumberId && x.CreatedAt <= adjustedDate)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync();

            return transaction?.BalanceAfter;
        }

        public Guid ProcessTransactionAsync(Guid accountNumberId, string description, decimal amount, Guid idempotencyKey, decimal balanceAfter, Guid transactionTypeId) {
            var entity = new Model.Transaction {
                AccountId = accountNumberId,
                Description = description,
                Amount = amount,
                BalanceAfter = balanceAfter,
                IdempotencyKey = idempotencyKey,
                TransactionTypeId = transactionTypeId
            };
            _context.Entry(new Model.TransactionType { Id = transactionTypeId }).State = EntityState.Unchanged;
            _repository.Create(entity, false);
            return entity.Id;
        }
    }
}
