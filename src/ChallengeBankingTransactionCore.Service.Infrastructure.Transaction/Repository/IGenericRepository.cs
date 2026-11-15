using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Base;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repositories {
    public interface IGenericRepository<T> where T : BaseEntity {
        T Create(T entity, bool saveChanges = true);
        T? FindById(Guid id);
        List<T> FindAll();
        T? Update(T entity, bool saveChanges = true);
        void Delete(Guid id, bool saveChanges = true);
        bool Exists(Guid id);
    }
}
