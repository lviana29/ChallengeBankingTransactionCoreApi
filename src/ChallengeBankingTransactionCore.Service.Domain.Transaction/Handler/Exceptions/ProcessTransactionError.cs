namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions {
    public class ProcessTransactionError : DomainException {
        public override string ErrorCode => "CORE_BANKING_004";
        public ProcessTransactionError() : base("Transaction failed", statusCode: 422, typeReturn: "error") { }
    }
}
