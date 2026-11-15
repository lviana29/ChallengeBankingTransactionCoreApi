using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repositories;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repository.Implementation {
    public class AccountRepository : IAccountRepository {
        private readonly AppDbContext _context;
        private readonly IGenericRepository<Account> _repository;
        public AccountRepository(AppDbContext context, IGenericRepository<Account> repository) {
            _context = context;
            _repository = repository;
        }
        public async Task<AccountEntity> GetByAccountNumber(string accountNumber) {
            var entity = await _context.Accounts
            .FirstOrDefaultAsync(x => x.AccountNumber == accountNumber);
            return entity == null ? new AccountEntity() : entity.Adapt<AccountEntity>();
        }

        public async Task UpdateAccount(AccountEntity accountEntity) {
            var entity = accountEntity.Adapt<Account>();
            entity.RowVersion = DateTime.UtcNow;
            _repository.Update(entity, false);
        }
    }
}
