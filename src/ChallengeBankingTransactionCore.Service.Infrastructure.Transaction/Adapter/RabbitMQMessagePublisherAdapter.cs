using System.Text;
using System.Text.Json;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Adapter {
    public class RabbitMQMessagePublisherAdapter : IMessagePublisherPort {
        private readonly IConfiguration _configuration;
        private const string QueueName = "transaction-created-queue";

        public RabbitMQMessagePublisherAdapter(IConfiguration configuration) {
            _configuration = configuration;
        }

        public async Task PublishTransactionCreatedAsync(TransactionCreatedEventEntity eventMessage) {
            var factory = new ConnectionFactory {
                HostName = _configuration["RabbitMQ:HostName"] ?? "localhost",
                Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
                UserName = _configuration["RabbitMQ:UserName"] ?? "guest",
                Password = _configuration["RabbitMQ:Password"] ?? "guest"
            };

            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var jsonBody = JsonSerializer.Serialize(eventMessage);
            var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

            var properties = new BasicProperties {
                Persistent = true
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: QueueName,
                mandatory: true,
                basicProperties: properties,
                body: bodyBytes
            );
        }
    }
}
