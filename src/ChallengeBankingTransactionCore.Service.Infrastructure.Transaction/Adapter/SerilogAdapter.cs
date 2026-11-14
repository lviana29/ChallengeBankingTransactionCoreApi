using Serilog;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Adapter {
    public class SerilogAdapter<T> : IAppLogger<T> {
        private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<T>();

        public void LogInformation(string message, params object[] args) {
            _logger.Information(message, args);
        }

        public void LogError(Exception ex, string message, params object[] args) {
            _logger.Error(ex, message, args);
        }
    }
}
