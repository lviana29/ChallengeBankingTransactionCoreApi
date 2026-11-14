namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions {
    public class AccountBlockedError : DomainException {
        public override string ErrorCode => "CORE_BANKING_002";

        public AccountBlockedError() : base("Account blocked", statusCode: 403, typeReturn: "warning") { }
    }
}
