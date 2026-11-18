namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities {
    public record TransactionCreatedEventEntity(
        Guid TransactionId,
        Guid AccountId,
        string AccountNumber,
        decimal Amount,
        decimal BalanceAfter,
        string TransactionType,
        Guid IdempotencyKey,
        DateTime CreatedAt
    );
}
