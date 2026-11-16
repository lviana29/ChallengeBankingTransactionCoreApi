using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Adapter;
using Microsoft.EntityFrameworkCore;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Infrastructure.Adapters {
    public class ResiliencePolicyExecutorAdapterTests {
        [Fact]
        public async Task ExecuteAsync_ShouldRetry_WhenTransientExceptionOccurs() {
            // Arrange
            var executor = new PollyResiliencePolicyExecutorAdapter();
            int executionAttempts = 0;

            // Act & Assert
            // Simula uma ação que falha nas primeiras vezes com erro transitório e depois teria sucesso
            // (ou falha todas para testar se esgotou as tentativas)
            await Assert.ThrowsAsync<DbUpdateException>(async () => {
                await executor.ExecuteAsync<bool>(async () => {
                    executionAttempts++;

                    // Cria uma exceção do EF Core com inner exception simulando timeout/conexão perdida
                    var innerException = new Exception("A network-related error occurred while establishing a connection to SQL Server. The server was not found or was not accessible.");
                    throw new DbUpdateException("Transient error", innerException);
                });
            });

            // Como configuramos MaxRetryAttempts = 3, ele deve tentar a original + 3 retries = 4 execuções no total
            Assert.Equal(4, executionAttempts);
        }
    }
}
