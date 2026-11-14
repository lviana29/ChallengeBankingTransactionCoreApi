namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities {
    public class AccountEntity {
        public string Id { get; set; } = String.Empty;
        public string AccountNumber { get; set; } = String.Empty;
        public decimal Balance { get; set; }
        public bool IsActive { get; set; }
    }
}
