namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities {
    public class TransactionEntity {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Description { get; set; } = String.Empty;
        public string AccountId { get; set; } = String.Empty;
        public Guid IdempotencyKey { get; set; } = Guid.NewGuid();
        public decimal Amount { get; set; } = 0;
        public decimal BalanceAfter { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = new();
        public Guid TransactionTypeId { get; set; } = Guid.NewGuid();
        public TransactionTypeEntity Type { get; set; } = new();
    }
}
