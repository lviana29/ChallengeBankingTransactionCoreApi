using ChallengeBankingTransactionCore.Service.Application.Transaction.Dto;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;

namespace ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.ProcessTransactionUseCase {
    public class ProcessTransactionUseCase : IProcessTransactionUseCase {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMessagePublisherPort _messagePublisher;
        public ProcessTransactionUseCase(IAccountRepository accountRepository, ITransactionRepository transactionRepository, IUnitOfWork unitOfWork, IMessagePublisherPort messagePublisher) {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _unitOfWork = unitOfWork;
            _messagePublisher = messagePublisher;
        }
        public async Task<string> ExecuteAsync(TransactionDto transactionDto) {

            if (string.IsNullOrWhiteSpace(transactionDto.accountNumber)) throw new InvalidAccountNumber();

            if (transactionDto.value <= 0) throw new InvalidTransactionValueError();

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

            const int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++) {
                try {
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
                        transactionId = _transactionRepository.ProcessTransactionAsync(account.Id, description, transactionDto.value, idempotencyKey, newBalance, transactionTypeId);
                    }
                    else {
                        newBalance = transactionDto.value + account.Balance;
                        account.Balance = newBalance;
                        //update account
                        await _accountRepository.UpdateAccount(account);
                        //insert in transaction table
                        description = "Crédito em conta";
                        transactionId = _transactionRepository.ProcessTransactionAsync(account.Id, description, transactionDto.value, idempotencyKey, newBalance, transactionTypeId);
                    }

                    var success = await _unitOfWork.CommitAsync();
                    if (!success) throw new ProcessTransactionError();

                    // --- DESacoplamento / CQRS: Publica o evento de transação criada de forma assíncrona ---
                    var transactionEvent = new TransactionCreatedEventEntity(
                        TransactionId: transactionId,
                        AccountId: account.Id,
                        AccountNumber: account.AccountNumber,
                        Amount: transactionDto.value,
                        BalanceAfter: newBalance,
                        TransactionType: description,
                        IdempotencyKey: idempotencyKey,
                        CreatedAt: DateTime.UtcNow
                    );

                    // Dispara a publicação (pode ser em background ou aguardar de forma resiliente)
                    await _messagePublisher.PublishTransactionCreatedAsync(transactionEvent);

                    return transactionId.ToString();
                }
                catch (AccountConcurrencyConflictException) {
                    if (attempt == maxRetries) {
                        throw;
                    }
                    await Task.Delay(50 * attempt);
                }
            }

            throw new AccountConcurrencyConflictException();
        }
    }
}
