using System.Net;
using System.Net.Http.Json;
using ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.IntegrationTests.Tools;
using ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Infrastructure.Adapters;
using ChallengeBankingTransactionCore.Service.Application.Transaction.Dto;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Workers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.IntegrationTests {
    [Collection(TestConfigs.TestCaseOrdererMySQLCollection)]
    public class FaultIsolationAndResilienceIntegrationTests : IClassFixture<MySQLFixture> {
        private readonly MySQLFixture _fixture;

        public FaultIsolationAndResilienceIntegrationTests(MySQLFixture fixture) {
            _fixture = fixture;
        }

        [Fact(DisplayName = "01 - Fault Isolation: POST /api/transaction/v1 returns HTTP 201 Created and saves OutboxMessage when RabbitMQ is offline")]
        public async Task PostTransaction_WhenBrokerIsOffline_ShouldStillReturn201AndPersistOutboxMessage() {
            // Arrange: Point RabbitMQ to an unreachable port (59999) and disable background relay
            var overrideConfig = new Dictionary<string, string?> {
                ["RabbitMQ:HostName"] = "localhost",
                ["RabbitMQ:Port"] = "59999",
                ["Hosting:EnableOutboxRelay"] = "false",
                ["Hosting:EnableConsolidationWorker"] = "false"
            };

            using var factory = new CustomWebApplicationFactory<Program>(_fixture.ConnectionString, overrideConfig);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions {
                BaseAddress = new Uri("http://localhost")
            });
            client.DefaultRequestHeaders.Add("Origin", "http://localhost:3000");

            var idempotencyKey = Guid.NewGuid().ToString();
            var dto = new TransactionDto("12345-6", 250.00m, 1, idempotencyKey);

            // Act
            var response = await client.PostAsJsonAsync("api/transaction/v1", dto);

            // Assert: HTTP 201 Created returned even with broker completely unreachable
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var rawBody = (await response.Content.ReadAsStringAsync()).Trim('"');
            Assert.True(Guid.TryParse(rawBody, out var transactionId));

            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var savedTx = await dbContext.Transactions.FirstOrDefaultAsync(t => t.Id == transactionId);
            Assert.NotNull(savedTx);
            Assert.Equal(250.00m, savedTx.Amount);

            var pendingOutbox = await dbContext.OutboxMessages
                .Where(o => o.ProcessedAt == null && o.Payload.Contains(transactionId.ToString()))
                .FirstOrDefaultAsync();
            Assert.NotNull(pendingOutbox);
            Assert.Equal(nameof(TransactionCreatedEventEntity), pendingOutbox.EventType);
        }

        [Fact(DisplayName = "02 - End-to-End Async Consolidation: OutboxRelay -> ConsolidationWorker updates DailyConsolidatedBalance and serves Cached Statement")]
        public async Task EndToEndAsyncConsolidation_WhenBrokerIsOnline_ShouldRelayOutboxAndUpdateDailyConsolidatedBalance() {
            // Arrange: In-process loopback publisher connecting OutboxRelay directly to ConsolidationWorkerHostedService
            using var factory = new CustomWebApplicationFactory<Program>(_fixture.ConnectionString)
                .WithWebHostBuilder(builder => {
                    builder.ConfigureServices(services => {
                        var publisherDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IMessagePublisherPort));
                        if (publisherDescriptor != null) {
                            services.Remove(publisherDescriptor);
                        }
                        services.AddSingleton<IMessagePublisherPort, InProcessLoopbackBrokerPublisher>();
                        services.AddSingleton<OutboxRelayHostedService>();
                        services.AddSingleton<ConsolidationWorkerHostedService>();
                    });
                });

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions {
                BaseAddress = new Uri("http://localhost")
            });
            client.DefaultRequestHeaders.Add("Origin", "http://localhost:3000");

            var idempotencyKey = Guid.NewGuid().ToString();
            var dto = new TransactionDto("12345-6", 175.50m, 1, idempotencyKey);

            // 1. Write transaction (commits Account + Transaction + OutboxMessage)
            var postResponse = await client.PostAsJsonAsync("api/transaction/v1", dto);
            Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
            var txIdStr = (await postResponse.Content.ReadAsStringAsync()).Trim('"');
            var txId = Guid.Parse(txIdStr);

            // 2. Trigger OutboxRelayHostedService batch processing -> publishes to loopback -> invokes ConsolidationWorkerHostedService
            var outboxRelay = factory.Services.GetRequiredService<OutboxRelayHostedService>();
            var processedCount = await outboxRelay.ProcessPendingBatchAsync();
            Assert.True(processedCount >= 1);

            // 3. Verify database state (OutboxMessage marked as processed, ProcessedEvent recorded, DailyConsolidatedBalance updated)
            using (var scope = factory.Services.CreateScope()) {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var outboxMsg = await dbContext.OutboxMessages
                    .FirstOrDefaultAsync(o => o.Payload.Contains(txId.ToString()));
                Assert.NotNull(outboxMsg);
                Assert.NotNull(outboxMsg.ProcessedAt);

                var processedEvent = await dbContext.ProcessedEvents
                    .FirstOrDefaultAsync(e => e.EventId == txId);
                Assert.NotNull(processedEvent);

                var dailyBalance = await dbContext.DailyConsolidatedBalances
                    .FirstOrDefaultAsync(d => d.AccountNumber == "12345-6" && d.BalanceDate == DateTime.UtcNow.Date);
                Assert.NotNull(dailyBalance);
                Assert.True(dailyBalance.TotalCredits >= 175.50m);
                Assert.True(dailyBalance.TransactionCount >= 1);
            }

            // 4. Query Consolidated Statement twice (1st = Cache Miss -> Populates Cache; 2nd = Cache Hit)
            var getResponse1 = await client.GetAsync("api/consolidatedstatement/v1?AccountNumber=12345-6");
            Assert.Equal(HttpStatusCode.OK, getResponse1.StatusCode);
            var statement1 = await getResponse1.Content.ReadFromJsonAsync<ConsolidatedStatementDto>();

            var getResponse2 = await client.GetAsync("api/consolidatedstatement/v1?AccountNumber=12345-6");
            Assert.Equal(HttpStatusCode.OK, getResponse2.StatusCode);
            var statement2 = await getResponse2.Content.ReadFromJsonAsync<ConsolidatedStatementDto>();

            Assert.NotNull(statement1);
            Assert.NotNull(statement2);
            Assert.Equal(statement1.consolidatedBalance, statement2.consolidatedBalance);
            Assert.Equal(statement1.currentBalance, statement2.currentBalance);
        }

        private sealed class InProcessLoopbackBrokerPublisher : IMessagePublisherPort {
            private readonly IServiceProvider _serviceProvider;

            public InProcessLoopbackBrokerPublisher(IServiceProvider serviceProvider) {
                _serviceProvider = serviceProvider;
            }

            public async Task PublishTransactionCreatedAsync(TransactionCreatedEventEntity transactionEvent) {
                var payload = System.Text.Json.JsonSerializer.Serialize(transactionEvent);
                var worker = _serviceProvider.GetRequiredService<ConsolidationWorkerHostedService>();
                await worker.HandleMessagePayloadAsync(payload);
            }
        }
    }
}
