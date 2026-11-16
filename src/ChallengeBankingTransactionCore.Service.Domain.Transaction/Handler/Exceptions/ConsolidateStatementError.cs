namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions {
    public class ConsolidateStatementError : DomainException {
        public override string ErrorCode => "CORE_BANKING_008";
        public ConsolidateStatementError() : base("Consolidate statement error", statusCode: 403, typeReturn: "warning") { }
    }
}
