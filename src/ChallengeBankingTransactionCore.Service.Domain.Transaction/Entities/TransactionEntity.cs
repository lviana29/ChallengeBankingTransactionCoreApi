namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities {
    public class TransactionEntity {
        public Guid Id { get; set; } = new Guid();
        public string Description { get; set; } = String.Empty;
        public string AccountId { get; set; } = String.Empty;
        public Guid IdempotencyKey { get; set; } = new Guid();
        public decimal Amount { get; set; } = 0;
        public decimal BalanceAfter { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = new();
        public Guid TransactionTypeId { get; set; } = new Guid();
        public TransactionTypeEntity Type { get; set; } = new();
    }
}
