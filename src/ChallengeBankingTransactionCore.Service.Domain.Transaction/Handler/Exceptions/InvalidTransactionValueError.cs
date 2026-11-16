namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions {
    public class InvalidTransactionValueError : DomainException {
        public override string ErrorCode => "CORE_BANKING_006";

        public InvalidTransactionValueError() : base("Invalid value for transaction", statusCode: 422, typeReturn: "warning") { }
    }
}
