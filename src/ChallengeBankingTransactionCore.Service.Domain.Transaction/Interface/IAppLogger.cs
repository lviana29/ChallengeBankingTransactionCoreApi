
namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface {
    public interface IAppLogger<T> {
        void LogInformation(string message, params object[] args);
        void LogError(Exception ex, string message, params object[] args);
    }
}
