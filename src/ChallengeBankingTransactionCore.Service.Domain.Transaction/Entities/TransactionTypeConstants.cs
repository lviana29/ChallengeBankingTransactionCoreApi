namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities {
    public enum TransactionTypeEntityEnum : int {
        Credit = 1,
        Debit = 2
    }
    public class TransactionTypeConstants {
        public static readonly Guid CreditId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public static readonly Guid DebitId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    }
}
