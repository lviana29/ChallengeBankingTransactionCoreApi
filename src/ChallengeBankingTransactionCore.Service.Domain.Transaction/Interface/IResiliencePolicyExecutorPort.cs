namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface {
    public interface IResiliencePolicyExecutorPort {
        Task<T> ExecuteAsync<T>(Func<Task<T>> action);
        Task ExecuteAsyncWithoutResult(Func<Task> action);
    }
}
