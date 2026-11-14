namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions {
    public class AccountNotFoundError : DomainException {
        public override string ErrorCode => "CORE_BANKING_003";

        public AccountNotFoundError() : base("Account not found", statusCode: 404, typeReturn: "warning") { }
    }
}
