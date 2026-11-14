using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Auth.Adapter;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Infrastructure.Adapters {
    public class SerilogAdapterTests {
        [Fact]
        public void LogInformation_ShouldNotThrowException() {
            // Arrange
            var adapter = new SerilogAdapter<SerilogAdapterTests>();

            // Act & Assert
            var exception = Record.Exception(() => adapter.LogInformation("Test log message {Param}", "Value"));
            Assert.Null(exception);
        }

        [Fact]
        public void LogError_ShouldNotThrowException() {
            // Arrange
            var adapter = new SerilogAdapter<SerilogAdapterTests>();
            var testException = new System.Exception("Simulated error");

            // Act & Assert
            var exception = Record.Exception(() => adapter.LogError(testException, "Error log message"));
            Assert.Null(exception);
        }
    }
}
