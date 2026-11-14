using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;

namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface {
    public interface IAccountRepository {
        Task<AccountEntity> GetByAccountNumber(string accountNumber);
        Task UpdateAccount(AccountEntity accountEntity);
    }
}
