namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities {
    public class AccountEntity {
        public Guid Id { get; set; } = new Guid();
        public string AccountNumber { get; set; } = String.Empty;
        public decimal Balance { get; set; }
        public bool IsActive { get; set; }
    }
}
