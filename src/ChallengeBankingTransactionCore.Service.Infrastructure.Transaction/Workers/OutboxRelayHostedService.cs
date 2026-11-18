using System.Text.Json;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Workers {
    public class OutboxRelayHostedService : BackgroundService {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly IAppLogger<OutboxRelayHostedService> _logger;

        public OutboxRelayHostedService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            IAppLogger<OutboxRelayHostedService> logger
        ) {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            var pollingIntervalMs = int.TryParse(_configuration["Outbox:PollingIntervalMs"], out var parsedInterval) && parsedInterval > 0
                ? parsedInterval
                : 500;

            _logger.LogInformation("OutboxRelayHostedService started with polling interval {PollingIntervalMs}ms", pollingIntervalMs);

            while (!stoppingToken.IsCancellationRequested) {
                try {
                    await ProcessPendingBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                    break;
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Unexpected error during OutboxRelayHostedService polling cycle");
                }

                await Task.Delay(pollingIntervalMs, stoppingToken);
            }
        }

        public async Task<int> ProcessPendingBatchAsync(CancellationToken cancellationToken = default) {
            var batchSize = int.TryParse(_configuration["Outbox:BatchSize"], out var parsedBatch) && parsedBatch > 0
                ? parsedBatch
                : 50;
            var maxRetries = int.TryParse(_configuration["Outbox:MaxRetries"], out var parsedRetries) && parsedRetries > 0
                ? parsedRetries
                : 5;

            using var scope = _scopeFactory.CreateScope();
            var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
            var messagePublisher = scope.ServiceProvider.GetRequiredService<IMessagePublisherPort>();

            var pendingMessages = await outboxRepository.GetPendingMessagesAsync(batchSize, maxRetries, cancellationToken);
            var processedCount = 0;

            foreach (var message in pendingMessages) {
                if (cancellationToken.IsCancellationRequested) break;

                try {
                    var transactionEvent = JsonSerializer.Deserialize<TransactionCreatedEventEntity>(message.Payload);
                    if (transactionEvent == null) {
                        await outboxRepository.MarkAsFailedAsync(message.Id, "Failed to deserialize TransactionCreatedEventEntity payload", cancellationToken);
                        _logger.LogWarning("OutboxMessage {MessageId} payload deserialized to null", message.Id);
                        continue;
                    }

                    await messagePublisher.PublishTransactionCreatedAsync(transactionEvent);
                    await outboxRepository.MarkAsProcessedAsync(message.Id, DateTime.UtcNow, cancellationToken);
                    processedCount++;
                }
                catch (Exception ex) {
                    await outboxRepository.MarkAsFailedAsync(message.Id, ex.Message, cancellationToken);
                    _logger.LogWarning("Failed to publish OutboxMessage {MessageId} (RetryCount={RetryCount}): {ErrorMessage}", message.Id, message.RetryCount + 1, ex.Message);
                }
            }

            return processedCount;
        }
    }
}
