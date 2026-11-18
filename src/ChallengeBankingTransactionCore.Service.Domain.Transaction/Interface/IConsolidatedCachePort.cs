namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface {
    public interface IConsolidatedCachePort {
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class;
        Task InvalidateByAccountAsync(string accountNumber, CancellationToken cancellationToken = default);
    }
}
