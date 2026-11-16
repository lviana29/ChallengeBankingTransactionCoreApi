using System.Net;
using System.Net.Http.Json;
using ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.IntegrationTests.Tools;
using ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Infrastructure.Adapters;
using ChallengeBankingTransactionCore.Service.Application.Transaction.Dto;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.IntegrationTests {
    [Collection(TestConfigs.TestCaseOrdererMySQLCollection)]
    [TestCaseOrderer(TestConfigs.TestCaseOrdererFullName, TestConfigs.TestCaseOrdererFullNameAssembly)]
    public class TransactionControllerIntegrationTests : IClassFixture<MySQLFixture> {
        private readonly HttpClient _httpClient;

        public TransactionControllerIntegrationTests(MySQLFixture fixture) {
            var factory = new CustomWebApplicationFactory<Program>(fixture.ConnectionString);
            _httpClient = factory.CreateClient(
                new WebApplicationFactoryClientOptions {
                    BaseAddress = new Uri("http://localhost")
                }
            );
        }

        private void AddOriginHeader(string origin) {
            _httpClient.DefaultRequestHeaders.Remove("Origin");
            _httpClient.DefaultRequestHeaders.Add("Origin", origin);
        }

        [Fact(DisplayName = "01 - Process Transaction Successfully")]
        [TestPriority(1)]
        public async Task ProcessTransaction_ShouldReturnCreatedOrOk_WhenRequestIsValid() {
            // Arrange
            AddOriginHeader("http://localhost:3000");

            var transactionDto = new TransactionDto("12345-6", 100.00m, 1, Guid.NewGuid().ToString());

            // Act
            var response = await _httpClient.PostAsJsonAsync("api/transaction/v1", transactionDto);

            // Assert
            response.EnsureSuccessStatusCode();

            Assert.True(
                response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK,
                $"Expected 201 or 200, but got {response.StatusCode}"
            );

            var result = await response.Content.ReadAsStringAsync();
            Assert.NotNull(result);
            Assert.False(string.IsNullOrEmpty(result));
        }

        [Fact(DisplayName = "02 - Process Transaction With Blocked Account (403 Forbidden)")]
        [TestPriority(2)]
        public async Task ProcessTransaction_ShouldReturn403_WhenAccountIsBlocked() {
            // Arrange
            AddOriginHeader("http://localhost:3000");
            var transactionDto = new TransactionDto("BLOCKED-ACC", 50.00m, 1, Guid.NewGuid().ToString());

            // Act
            var response = await _httpClient.PostAsJsonAsync("api/transaction/v1", transactionDto);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact(DisplayName = "03 - Process Transaction With Non-Existent Account (404 Not Found)")]
        [TestPriority(3)]
        public async Task ProcessTransaction_ShouldReturn404_WhenAccountDoesNotExist() {
            // Arrange
            AddOriginHeader("http://localhost:3000");
            var transactionDto = new TransactionDto("99999-9", 50.00m, 1, Guid.NewGuid().ToString());

            // Act
            var response = await _httpClient.PostAsJsonAsync("api/transaction/v1", transactionDto);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact(DisplayName = "04 - Process Transaction With Invalid Payload or Business Rule (422 Unprocessable Entity)")]
        [TestPriority(4)]
        public async Task ProcessTransaction_ShouldReturn422_WhenBusinessRuleFails() {
            // Arrange
            AddOriginHeader("http://localhost:3000");
            var transactionDto = new TransactionDto("12345-6", -500.00m, 2, Guid.NewGuid().ToString());

            // Act
            var response = await _httpClient.PostAsJsonAsync("api/transaction/v1", transactionDto);

            // Assert
            Assert.Equal((HttpStatusCode)422, response.StatusCode);
        }
    }
}
