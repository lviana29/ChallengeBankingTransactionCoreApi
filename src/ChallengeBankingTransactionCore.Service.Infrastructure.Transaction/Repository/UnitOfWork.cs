using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repository {
    public class UnitOfWork : IUnitOfWork {
        private readonly AppDbContext _context;

        public UnitOfWork(AppDbContext context) {
            _context = context;
        }
        public async Task<bool> CommitAsync() {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try {
                var affectedRows = await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return affectedRows > 0;
            }
            catch {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
