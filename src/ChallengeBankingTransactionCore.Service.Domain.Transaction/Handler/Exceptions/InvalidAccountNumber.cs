namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions {
    public class InvalidAccountNumber : DomainException {
        public override string ErrorCode => "CORE_BANKING_007";

        public InvalidAccountNumber() : base("Invalid account number", statusCode: 403, typeReturn: "warning") { }
    }
}
