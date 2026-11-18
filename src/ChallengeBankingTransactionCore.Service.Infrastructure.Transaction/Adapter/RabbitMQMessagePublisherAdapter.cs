using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Adapter {
    public class RabbitMQMessagePublisherAdapter : IMessagePublisherPort, IAsyncDisposable {
        private readonly IConfiguration _configuration;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private IConnection? _connection;

        public const string ExchangeName = "transactions.events";
        public const string QueueName = "balance.consolidation";
        public const string DeadLetterExchangeName = "transactions.events.dlx";
        public const string DeadLetterQueueName = "balance.consolidation.dlq";
        public const string LegacyQueueName = "transaction-created-queue";

        public RabbitMQMessagePublisherAdapter(IConfiguration configuration) {
            _configuration = configuration;
        }

        [ExcludeFromCodeCoverage]
        protected virtual async Task<IConnection> GetOrCreateConnectionAsync(CancellationToken cancellationToken = default) {
            if (_connection is { IsOpen: true }) {
                return _connection;
            }

            await _connectionLock.WaitAsync(cancellationToken);
            try {
                if (_connection is { IsOpen: true }) {
                    return _connection;
                }

                if (_connection != null) {
                    await _connection.DisposeAsync();
                    _connection = null;
                }

                var factory = new ConnectionFactory {
                    HostName = _configuration["RabbitMQ:HostName"] ?? "localhost",
                    Port = int.TryParse(_configuration["RabbitMQ:Port"], out var port) ? port : 5672,
                    UserName = _configuration["RabbitMQ:UserName"] ?? "guest",
                    Password = _configuration["RabbitMQ:Password"] ?? "guest"
                };

                _connection = await factory.CreateConnectionAsync(cancellationToken);
                return _connection;
            }
            finally {
                _connectionLock.Release();
            }
        }

        public static async Task EnsureTopologyDeclaredAsync(IChannel channel, CancellationToken cancellationToken = default) {
            await channel.ExchangeDeclareAsync(
                exchange: DeadLetterExchangeName,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken
            );

            await channel.QueueDeclareAsync(
                queue: DeadLetterQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken
            );

            await channel.QueueBindAsync(
                queue: DeadLetterQueueName,
                exchange: DeadLetterExchangeName,
                routingKey: DeadLetterQueueName,
                arguments: null,
                cancellationToken: cancellationToken
            );

            await channel.ExchangeDeclareAsync(
                exchange: ExchangeName,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken
            );

            var queueArgs = new Dictionary<string, object?> {
                ["x-dead-letter-exchange"] = DeadLetterExchangeName,
                ["x-dead-letter-routing-key"] = DeadLetterQueueName
            };

            await channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs,
                cancellationToken: cancellationToken
            );

            await channel.QueueBindAsync(
                queue: QueueName,
                exchange: ExchangeName,
                routingKey: QueueName,
                arguments: null,
                cancellationToken: cancellationToken
            );

            await channel.QueueDeclareAsync(
                queue: LegacyQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken
            );

            await channel.QueueBindAsync(
                queue: LegacyQueueName,
                exchange: ExchangeName,
                routingKey: QueueName,
                arguments: null,
                cancellationToken: cancellationToken
            );
        }

        public async Task PublishTransactionCreatedAsync(TransactionCreatedEventEntity eventMessage) {
            var connection = await GetOrCreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            await EnsureTopologyDeclaredAsync(channel);

            var jsonBody = JsonSerializer.Serialize(eventMessage);
            var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

            var properties = new BasicProperties {
                Persistent = true,
                MessageId = eventMessage.TransactionId.ToString(),
                ContentType = "application/json"
            };

            await channel.BasicPublishAsync(
                exchange: ExchangeName,
                routingKey: QueueName,
                mandatory: true,
                basicProperties: properties,
                body: bodyBytes
            );
        }

        public async ValueTask DisposeAsync() {
            await _connectionLock.WaitAsync();
            try {
                if (_connection != null) {
                    await _connection.DisposeAsync();
                    _connection = null;
                }
            }
            finally {
                _connectionLock.Release();
                _connectionLock.Dispose();
            }
            GC.SuppressFinalize(this);
        }
    }
}
