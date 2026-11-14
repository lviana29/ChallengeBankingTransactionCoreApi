using ChallengeBankingTransactionCore.Service.Application.Transaction.Dto;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;

namespace ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.ProcessTransactionUseCase {
    public class ProcessTransactionUseCase : IProcessTransactionUseCase {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IUnitOfWork _unitOfWork;
        public ProcessTransactionUseCase(IAccountRepository accountRepository, ITransactionRepository transactionRepository, IUnitOfWork unitOfWork) {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _unitOfWork = unitOfWork;
        }
        public async Task<string> ExecuteAsync(TransactionDto transactionDto) {
            decimal newBalance = 0;
            var transactionId = "";
            var existingTransaction = await _transactionRepository.GetByIdempotency(transactionDto.idempotencyKey);

            if (existingTransaction != null) return existingTransaction.Id;

            if ((TransactionTypeEntityEnum)transactionDto.transactionType != TransactionTypeEntityEnum.Credit ||
            (TransactionTypeEntityEnum)transactionDto.transactionType != TransactionTypeEntityEnum.Debit) throw new InvalidTransactionTypeError();

            var account = await _accountRepository.GetByAccountNumber(transactionDto.accountNumber);
            if (account == null) throw new AccountNotFoundError();

            if (!account.IsActive) throw new AccountBlockedError();

            if ((TransactionTypeEntityEnum)transactionDto.transactionType == TransactionTypeEntityEnum.Debit) {
                if (account.Balance < transactionDto.value) throw new InvalidValueToDebitError();
                newBalance = account.Balance - transactionDto.value;
                //update account
                await _accountRepository.UpdateAccount(account);
                //insert in transaction table
            }
            else {
                newBalance = transactionDto.value + account.Balance;
                //update account
                await _accountRepository.UpdateAccount(account);
                //insert in transaction table
            }

            var success = await _unitOfWork.CommitAsync();
            if (!success) throw new ProcessTransactionError();

            return transactionId;
        }
    }
}
