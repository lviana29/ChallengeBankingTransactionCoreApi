namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities {
    public enum TransactionTypeEntityEnum : int {
        Credit = 1,
        Debit = 2
    }
    public class TransactionTypeEntity {
        public string Title { get; set; } = String.Empty;
    }
}
