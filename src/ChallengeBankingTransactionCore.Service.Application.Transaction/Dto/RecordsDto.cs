namespace ChallengeBankingTransactionCore.Service.Application.Transaction.Dto {
    public record TransactionDto(string accountNumber, decimal value, int transactionType, string? idempotencyKey);
}
