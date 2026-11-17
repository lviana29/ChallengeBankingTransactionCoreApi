using ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;
using Microsoft.EntityFrameworkCore;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repository {
    public class UnitOfWork : IUnitOfWork {
        private readonly AppDbContext _context;
        private readonly IResiliencePolicyExecutorPort _resiliencePolicyExecutorPort;

        public UnitOfWork(AppDbContext context, IResiliencePolicyExecutorPort resiliencePolicyExecutorPort) {
            _context = context;
            _resiliencePolicyExecutorPort = resiliencePolicyExecutorPort;
        }
        public async Task<bool> CommitAsync() {
            return await _resiliencePolicyExecutorPort.ExecuteAsync<bool>(async () => {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try {
                    var affectedRows = await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return affectedRows >= 0;
                }
                catch (DbUpdateConcurrencyException ex) {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    throw new AccountConcurrencyConflictException(ex.Message);
                }
                catch {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    throw;
                }
            });

        }
    }
}
