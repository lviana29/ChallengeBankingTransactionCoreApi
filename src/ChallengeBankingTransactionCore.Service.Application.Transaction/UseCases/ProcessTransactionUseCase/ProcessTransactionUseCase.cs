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
            Guid idempotencyKey = Guid.TryParse(transactionDto.idempotencyKey, out var parsedIdempotencyKey)
                ? parsedIdempotencyKey
                : Guid.NewGuid();

            var existingTransaction = await _transactionRepository.GetByIdempotency(idempotencyKey);

            if (existingTransaction != null) return existingTransaction.Id.ToString();

            Guid transactionTypeId = transactionDto.transactionType switch {
                1 => TransactionTypeConstants.CreditId,
                2 => TransactionTypeConstants.DebitId,
                _ => throw new InvalidTransactionTypeError()
            };

            var account = await _accountRepository.GetByAccountNumber(transactionDto.accountNumber);
            if (account == null) throw new AccountNotFoundError();

            if (!account.IsActive) throw new AccountBlockedError();

            Guid transactionId;
            string? description;
            decimal newBalance;

            if ((TransactionTypeEntityEnum)transactionDto.transactionType == TransactionTypeEntityEnum.Debit) {
                if (account.Balance < transactionDto.value) throw new InvalidValueToDebitError();
                newBalance = account.Balance - transactionDto.value;
                account.Balance = newBalance;
                //update account
                await _accountRepository.UpdateAccount(account);
                //insert in transaction table
                description = "Débito em conta";
                transactionId = _transactionRepository.ProcessTransactionAsync(account.Id, description, transactionDto.value, idempotencyKey, newBalance);
            }
            else {
                newBalance = transactionDto.value + account.Balance;
                account.Balance = newBalance;
                //update account
                await _accountRepository.UpdateAccount(account);
                //insert in transaction table
                description = "Crédito em conta";
                transactionId = _transactionRepository.ProcessTransactionAsync(account.Id, description, transactionDto.value, idempotencyKey, newBalance);
            }

            var success = await _unitOfWork.CommitAsync();
            if (!success) throw new ProcessTransactionError();

            return transactionId.ToString();
        }
    }
}
