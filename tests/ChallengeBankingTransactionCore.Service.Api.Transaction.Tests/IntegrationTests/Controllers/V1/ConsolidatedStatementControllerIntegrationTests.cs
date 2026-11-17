using System.Net;
using System.Net.Http.Json;
using ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.IntegrationTests.Tools;
using ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Infrastructure.Adapters;
using ChallengeBankingTransactionCore.Service.Application.Transaction.Dto;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.IntegrationTests.Controllers.V1 {
    [Collection(TestConfigs.TestCaseOrdererMySQLCollection)]
    [TestCaseOrderer(TestConfigs.TestCaseOrdererFullName, TestConfigs.TestCaseOrdererFullNameAssembly)]
    public class ConsolidatedStatementControllerIntegrationTests : IClassFixture<MySQLFixture> {
        private readonly HttpClient _httpClient;

        public ConsolidatedStatementControllerIntegrationTests(MySQLFixture fixture) {
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

        [Fact(DisplayName = "01 - Get Consolidated Statement Successfully (200 OK)")]
        [TestPriority(1)]
        public async Task GetConsolidatedStatement_ShouldReturn200_WhenAccountExists() {
            // Arrange
            AddOriginHeader("http://localhost:3000");

            // Utilize uma conta válida que exista no seu banco/seed de testes (ex: "12345-6")
            string accountNumber = "12345-6";

            // Act - Como é um Get, enviamos os filtros via Query String
            var response = await _httpClient.GetAsync($"api/consolidatedstatement/v1?AccountNumber={accountNumber}");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var statement = await response.Content.ReadFromJsonAsync<ConsolidatedStatementDto>();
            Assert.NotNull(statement);
            Assert.Equal(accountNumber, statement.accountNumber);
        }

        [Fact(DisplayName = "02 - Get Consolidated Statement With Non-Existent Account (404 Not Found)")]
        [TestPriority(2)]
        public async Task GetConsolidatedStatement_ShouldReturn404_WhenAccountDoesNotExist() {
            // Arrange
            AddOriginHeader("http://localhost:3000");
            string nonExistentAccount = "99999-9";

            // Act
            var response = await _httpClient.GetAsync($"api/consolidatedstatement/v1?AccountNumber={nonExistentAccount}");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact(DisplayName = "03 - Get Consolidated Statement With Empty Account Number (422 / 400 Unprocessable Entity)")]
        [TestPriority(3)]
        public async Task GetConsolidatedStatement_ShouldReturnError_WhenAccountNumberIsEmpty() {
            // Arrange
            AddOriginHeader("http://localhost:3000");

            // Act - Sem passar o número da conta (deve disparar a exceção InvalidAccountNumber mapeada)
            var response = await _httpClient.GetAsync("api/consolidatedstatement/v1?AccountNumber=");

            // Assert
            Assert.True(
                response.StatusCode == HttpStatusCode.UnprocessableEntity ||
                response.StatusCode == HttpStatusCode.BadRequest,
                $"Expected 422 or 400, but got {response.StatusCode}"
            );
        }
    }
}
