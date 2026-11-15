using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Base;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repositories {
    public interface IGenericRepository<T> where T : BaseEntity {
        T Create(T entity);
        T? FindById(Guid id);
        List<T> FindAll();
        T? Update(T entity);
        void Delete(Guid id);
        bool Exists(Guid id);
    }
}
