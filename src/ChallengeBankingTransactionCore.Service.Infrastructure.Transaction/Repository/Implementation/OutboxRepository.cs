using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repository.Implementation {
    public class OutboxRepository : IOutboxRepository {
        private readonly AppDbContext _context;

        public OutboxRepository(AppDbContext context) {
            _context = context;
        }

        public void AddOutboxMessage(OutboxMessageEntity message) {
            var entity = new OutboxMessage {
                Id = message.Id == Guid.Empty ? Guid.NewGuid() : message.Id,
                EventType = message.EventType,
                Payload = message.Payload,
                CreatedAt = message.CreatedAt == default ? DateTime.UtcNow : message.CreatedAt,
                ProcessedAt = message.ProcessedAt,
                RetryCount = message.RetryCount,
                LastError = message.LastError
            };

            _context.OutboxMessages.Add(entity);
        }

        public async Task<List<OutboxMessageEntity>> GetPendingMessagesAsync(int batchSize, int maxRetries, CancellationToken cancellationToken = default) {
            var pending = await _context.OutboxMessages
                .AsNoTracking()
                .Where(m => m.ProcessedAt == null && m.RetryCount < maxRetries)
                .OrderBy(m => m.CreatedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            return pending.Adapt<List<OutboxMessageEntity>>();
        }

        public async Task MarkAsProcessedAsync(Guid messageId, DateTime processedAt, CancellationToken cancellationToken = default) {
            var entity = await _context.OutboxMessages
                .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

            if (entity != null) {
                entity.ProcessedAt = processedAt;
                entity.LastError = null;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default) {
            var entity = await _context.OutboxMessages
                .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

            if (entity != null) {
                entity.RetryCount++;
                entity.LastError = string.IsNullOrEmpty(error)
                    ? "Unknown error"
                    : (error.Length > 500 ? error[..500] : error);

                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
