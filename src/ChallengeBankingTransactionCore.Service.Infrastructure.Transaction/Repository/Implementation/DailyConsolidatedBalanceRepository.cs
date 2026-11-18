using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repository.Implementation {
    public class DailyConsolidatedBalanceRepository : IDailyConsolidatedBalanceRepository {
        private readonly AppDbContext _context;

        public DailyConsolidatedBalanceRepository(AppDbContext context) {
            _context = context;
        }

        public async Task<bool> HasEventBeenProcessedAsync(Guid eventId, CancellationToken cancellationToken = default) {
            return await _context.ProcessedEvents
                .AsNoTracking()
                .AnyAsync(e => e.EventId == eventId, cancellationToken);
        }

        public async Task<bool> UpsertDailyBalanceWithInboxAsync(TransactionCreatedEventEntity transactionEvent, CancellationToken cancellationToken = default) {
            var alreadyProcessed = await _context.ProcessedEvents
                .AnyAsync(e => e.EventId == transactionEvent.TransactionId, cancellationToken);

            if (alreadyProcessed) {
                return false;
            }

            var processedEvent = new ProcessedEvent {
                EventId = transactionEvent.TransactionId,
                EventType = nameof(TransactionCreatedEventEntity),
                ProcessedAt = DateTime.UtcNow
            };
            _context.ProcessedEvents.Add(processedEvent);

            var balanceDate = transactionEvent.CreatedAt.Date;
            var dailyBalance = await _context.DailyConsolidatedBalances
                .FirstOrDefaultAsync(b => b.AccountId == transactionEvent.AccountId && b.BalanceDate == balanceDate, cancellationToken);

            var isCredit = transactionEvent.TransactionType.Contains("Crédito", StringComparison.OrdinalIgnoreCase)
                || transactionEvent.TransactionType.Contains("Credit", StringComparison.OrdinalIgnoreCase);

            if (dailyBalance == null) {
                dailyBalance = new DailyConsolidatedBalance {
                    Id = Guid.NewGuid(),
                    AccountId = transactionEvent.AccountId,
                    AccountNumber = transactionEvent.AccountNumber,
                    BalanceDate = balanceDate,
                    TotalCredits = isCredit ? transactionEvent.Amount : 0m,
                    TotalDebits = isCredit ? 0m : transactionEvent.Amount,
                    ClosingBalance = transactionEvent.BalanceAfter,
                    TransactionCount = 1,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.DailyConsolidatedBalances.Add(dailyBalance);
            }
            else {
                if (isCredit) {
                    dailyBalance.TotalCredits += transactionEvent.Amount;
                }
                else {
                    dailyBalance.TotalDebits += transactionEvent.Amount;
                }

                dailyBalance.ClosingBalance = transactionEvent.BalanceAfter;
                dailyBalance.TransactionCount += 1;
                dailyBalance.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<DailyConsolidatedBalanceEntity?> GetDailyBalanceAsync(Guid accountId, DateTime balanceDate, CancellationToken cancellationToken = default) {
            var dateOnly = balanceDate.Date;
            var entity = await _context.DailyConsolidatedBalances
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.AccountId == accountId && b.BalanceDate == dateOnly, cancellationToken);

            return entity?.Adapt<DailyConsolidatedBalanceEntity>();
        }

        public async Task<List<DailyConsolidatedBalanceEntity>> GetDailyBalancesInRangeAsync(Guid accountId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) {
            var start = startDate.Date;
            var end = endDate.Date;
            var entities = await _context.DailyConsolidatedBalances
                .AsNoTracking()
                .Where(b => b.AccountId == accountId && b.BalanceDate >= start && b.BalanceDate <= end)
                .OrderBy(b => b.BalanceDate)
                .ToListAsync(cancellationToken);

            return entities.Adapt<List<DailyConsolidatedBalanceEntity>>();
        }
    }
}
