using System.Text;
using System.Text.Json;
using ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.ProcessTransactionEventUseCase;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Adapter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Workers {
    public class ConsolidationWorkerHostedService : BackgroundService {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly IAppLogger<ConsolidationWorkerHostedService> _logger;

        public ConsolidationWorkerHostedService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            IAppLogger<ConsolidationWorkerHostedService> logger
        ) {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> HandleMessagePayloadAsync(string jsonPayload, CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(jsonPayload)) {
                throw new ConsolidateStatementError();
            }

            TransactionCreatedEventEntity? transactionEvent;
            try {
                transactionEvent = JsonSerializer.Deserialize<TransactionCreatedEventEntity>(jsonPayload);
            }
            catch (JsonException) {
                throw;
            }

            if (transactionEvent == null) {
                throw new ConsolidateStatementError();
            }

            using var scope = _scopeFactory.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<IProcessTransactionEventUseCase>();
            return await useCase.ExecuteAsync(transactionEvent, cancellationToken);
        }

        public async Task ProcessDeliveryAsync(IChannel channel, ulong deliveryTag, string jsonPayload, CancellationToken cancellationToken = default) {
            try {
                await HandleMessagePayloadAsync(jsonPayload, cancellationToken);
                await channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is JsonException or ConsolidateStatementError) {
                _logger.LogError(ex, "Poison message rejected to DLQ (DeliveryTag={DeliveryTag})", deliveryTag);
                await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: cancellationToken);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Transient error processing message (DeliveryTag={DeliveryTag}). Requeueing.", deliveryTag);
                await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: true, cancellationToken: cancellationToken);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            await Task.Yield();

            var enabled = !bool.TryParse(_configuration["Hosting:EnableConsolidationWorker"], out var parsedEnabled) || parsedEnabled;
            if (!enabled) {
                _logger.LogInformation("ConsolidationWorkerHostedService is disabled by configuration.");
                return;
            }

            var retryDelayMs = int.TryParse(_configuration["RabbitMQ:ReconnectDelayMs"], out var parsedDelay) && parsedDelay > 0
                ? parsedDelay
                : 3000;

            while (!stoppingToken.IsCancellationRequested) {
                IConnection? connection = null;
                IChannel? channel = null;

                try {
                    var factory = new ConnectionFactory {
                        HostName = _configuration["RabbitMQ:HostName"] ?? "localhost",
                        Port = int.TryParse(_configuration["RabbitMQ:Port"], out var port) ? port : 5672,
                        UserName = _configuration["RabbitMQ:UserName"] ?? "guest",
                        Password = _configuration["RabbitMQ:Password"] ?? "guest"
                    };

                    connection = await factory.CreateConnectionAsync(stoppingToken);
                    channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                    await RabbitMQMessagePublisherAdapter.EnsureTopologyDeclaredAsync(channel, stoppingToken);
                    await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.ReceivedAsync += async (_, ea) => {
                        var payload = Encoding.UTF8.GetString(ea.Body.ToArray());
                        await ProcessDeliveryAsync(channel, ea.DeliveryTag, payload, stoppingToken);
                    };

                    await channel.BasicConsumeAsync(
                        queue: RabbitMQMessagePublisherAdapter.QueueName,
                        autoAck: false,
                        consumer: consumer,
                        cancellationToken: stoppingToken
                    );

                    _logger.LogInformation("ConsolidationWorkerHostedService connected and consuming from queue {QueueName}", RabbitMQMessagePublisherAdapter.QueueName);

                    while (!stoppingToken.IsCancellationRequested && connection.IsOpen && channel.IsOpen) {
                        await Task.Delay(1000, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                    break;
                }
                catch (Exception ex) {
                    _logger.LogWarning("ConsolidationWorkerHostedService connection/channel interrupted: {Message}. Retrying in {Delay}ms...", ex.Message, retryDelayMs);
                }
                finally {
                    if (channel != null) {
                        try { await channel.DisposeAsync(); } catch { /* ignore cleanup error */ }
                    }
                    if (connection != null) {
                        try { await connection.DisposeAsync(); } catch { /* ignore cleanup error */ }
                    }
                }

                if (!stoppingToken.IsCancellationRequested) {
                    try {
                        await Task.Delay(retryDelayMs, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                        break;
                    }
                }
            }
        }
    }
}
