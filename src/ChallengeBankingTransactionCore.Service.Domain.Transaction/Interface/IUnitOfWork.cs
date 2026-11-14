namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface {
    public interface IUnitOfWork {
        Task<bool> CommitAsync();
    }
}
