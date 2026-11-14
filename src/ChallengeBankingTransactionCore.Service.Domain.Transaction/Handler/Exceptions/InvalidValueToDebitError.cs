namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions {
    public class InvalidValueToDebitError : DomainException {
        public override string ErrorCode => "CORE_BANKING_001";

        public InvalidValueToDebitError() : base("Invalid value to debit", statusCode: 422, typeReturn: "warning") { }
    }
}
