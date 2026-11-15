using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Base;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repositories;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;
using Microsoft.EntityFrameworkCore;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repositories.Implementation {
    public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity {
        protected readonly AppDbContext _context;
        private DbSet<T> _dataSet;
        public GenericRepository(AppDbContext context) {
            _context = context;
            _dataSet = context.Set<T>();
        }

        public T Create(T entity) {
            _dataSet.Add(entity);
            _context.SaveChanges();
            return entity;
        }

        public void Delete(Guid id) {
            var existingEntity = _dataSet.FirstOrDefault(e => e.Id == id);
            if (existingEntity != null) {
                _dataSet.Remove(existingEntity);
                _context.SaveChanges();
            }
        }

        public bool Exists(Guid id) {
            return _dataSet.Any(e => e.Id == id);
        }

        public List<T> FindAll() {
            return _dataSet.ToList();
        }

        public T? FindById(Guid id) {
            return _dataSet.FirstOrDefault(e => e.Id == id);
        }

        public T? Update(T entity) {
            var existingEntity = _dataSet.FirstOrDefault(e => e.Id == entity.Id);
            if (existingEntity == null) return null;
            _context.Entry(existingEntity).CurrentValues.SetValues(entity);
            _context.SaveChanges();
            return existingEntity;
        }
    }
}
