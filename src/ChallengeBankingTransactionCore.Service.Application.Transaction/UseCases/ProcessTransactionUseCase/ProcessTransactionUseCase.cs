using System.Text.Json;
using ChallengeBankingTransactionCore.Service.Application.Transaction.Dto;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Utils;

namespace ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.ProcessTransactionUseCase {
    public class ProcessTransactionUseCase : IProcessTransactionUseCase {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOutboxRepository _outboxRepository;
        private readonly IAppLogger<ProcessTransactionUseCase>? _logger;

        public ProcessTransactionUseCase(
            IAccountRepository accountRepository,
            ITransactionRepository transactionRepository,
            IUnitOfWork unitOfWork,
            IOutboxRepository outboxRepository,
            IAppLogger<ProcessTransactionUseCase>? logger = null
        ) {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _unitOfWork = unitOfWork;
            _outboxRepository = outboxRepository;
            _logger = logger;
        }
        public async Task<string> ExecuteAsync(TransactionDto transactionDto) {

            if (string.IsNullOrWhiteSpace(transactionDto.accountNumber)) throw new InvalidAccountNumber();

            if (transactionDto.value <= 0) throw new InvalidTransactionValueError();

            var maskedAccount = SensitiveDataMasker.MaskAccountNumber(transactionDto.accountNumber);

            Guid idempotencyKey = Guid.TryParse(transactionDto.idempotencyKey, out var parsedIdempotencyKey)
                ? parsedIdempotencyKey
                : Guid.NewGuid();

            var existingTransaction = await _transactionRepository.GetByIdempotency(idempotencyKey);

            if (existingTransaction != null) {
                _logger?.LogInformation("Idempotent transaction hit for account {MaskedAccount} with IdempotencyKey {IdempotencyKey}.", maskedAccount, idempotencyKey);
                return existingTransaction.Id.ToString();
            }

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
                    string description;
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

                    // --- Transactional Outbox Pattern: Enfileira evento na mesma transação ACID ---
                    var now = DateTime.UtcNow;
                    var transactionEvent = new TransactionCreatedEventEntity(
                        TransactionId: transactionId,
                        AccountId: account.Id,
                        AccountNumber: account.AccountNumber,
                        Amount: transactionDto.value,
                        BalanceAfter: newBalance,
                        TransactionType: description,
                        IdempotencyKey: idempotencyKey,
                        CreatedAt: now
                    );

                    var outboxMessage = new OutboxMessageEntity {
                        Id = Guid.NewGuid(),
                        EventType = nameof(TransactionCreatedEventEntity),
                        Payload = JsonSerializer.Serialize(transactionEvent),
                        CreatedAt = now,
                        ProcessedAt = null,
                        RetryCount = 0,
                        LastError = null
                    };

                    _outboxRepository.AddOutboxMessage(outboxMessage);

                    var success = await _unitOfWork.CommitAsync();
                    if (!success) throw new ProcessTransactionError();

                    _logger?.LogInformation("Processed transaction {TransactionId} ({TransactionType}) for account {MaskedAccount}.", transactionId, description, maskedAccount);
                    return transactionId.ToString();
                }
                catch (AccountConcurrencyConflictException) {
                    _logger?.LogWarning("Concurrency conflict on account {MaskedAccount} (Attempt {Attempt}/{MaxRetries}).", maskedAccount, attempt, maxRetries);
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
