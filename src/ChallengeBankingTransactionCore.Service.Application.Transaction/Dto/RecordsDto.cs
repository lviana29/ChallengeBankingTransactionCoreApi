namespace ChallengeBankingTransactionCore.Service.Application.Transaction.Dto {
    public record TransactionDto(string accountNumber, decimal value, int transactionType, string? idempotencyKey);
    public record ConsolidatedStatementDto(string accountNumber, decimal currentBalance, List<ConsolidatedTransactionsDto> consolidatedTransactions);
    public record ConsolidatedTransactionsDto(decimal value, DateTime createdAt, string transactionType, decimal balanceAfter);
    public record ConsolidatedTransactionsFiltersDto(string AccountNumber, DateTime? StartDate, DateTime? EndDate);

}
