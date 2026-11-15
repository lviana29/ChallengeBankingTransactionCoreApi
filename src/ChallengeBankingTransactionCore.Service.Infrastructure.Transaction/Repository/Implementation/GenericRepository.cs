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

        public T Create(T entity, bool saveChanges = true) {
            _dataSet.Add(entity);
            if (saveChanges) {
                _context.SaveChanges();
            }

            return entity;
        }

        public void Delete(Guid id, bool saveChanges = true) {
            var existingEntity = _dataSet.FirstOrDefault(e => e.Id == id);
            if (existingEntity != null) {
                _dataSet.Remove(existingEntity);
                if (saveChanges) {
                    _context.SaveChanges();
                }
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

        public T? Update(T entity, bool saveChanges = true) {
            var existingEntity = _dataSet.FirstOrDefault(e => e.Id == entity.Id);
            if (existingEntity == null) return null;
            _context.Entry(existingEntity).CurrentValues.SetValues(entity);
            if (saveChanges) {
                _context.SaveChanges();
            }
            return existingEntity;
        }
    }
}
