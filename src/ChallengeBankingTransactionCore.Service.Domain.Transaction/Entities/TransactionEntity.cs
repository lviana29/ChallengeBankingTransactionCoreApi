namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities {
    public class TransactionEntity {
        public string Id { get; set; } = String.Empty;
        public string Description { get; set; } = String.Empty;
        public string AccountId { get; set; } = String.Empty;
        public string IdempotencyKey { get; set; } = String.Empty;
        public Decimal Amount { get; set; } = 0;
        public Decimal BalanceAfter { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = new();
        public int TransactionTypeId { get; set; } = 0;
        public TransactionTypeEntity Type { get; set; } = new();
    }
}
