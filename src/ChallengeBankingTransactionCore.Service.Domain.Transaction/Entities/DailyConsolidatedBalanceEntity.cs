namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities {
    public class DailyConsolidatedBalanceEntity {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public DateTime BalanceDate { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal TotalDebits { get; set; }
        public decimal ClosingBalance { get; set; }
        public int TransactionCount { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
