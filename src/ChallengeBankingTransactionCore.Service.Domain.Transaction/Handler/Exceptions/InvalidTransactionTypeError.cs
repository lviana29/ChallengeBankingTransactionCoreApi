namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions {
    public class InvalidTransactionTypeError : DomainException {
        public override string ErrorCode => "CORE_BANKING_005";

        public InvalidTransactionTypeError() : base("Invalid transaction type, should be 1 for credit and 2 for debit", statusCode: 422, typeReturn: "warning") { }
    }
}
