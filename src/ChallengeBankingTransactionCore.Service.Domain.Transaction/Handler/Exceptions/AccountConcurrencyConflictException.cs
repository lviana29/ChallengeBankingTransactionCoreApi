namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions {
    public class AccountConcurrencyConflictException : DomainException {
        public override string ErrorCode => "CORE_BANKING_010";

        public AccountConcurrencyConflictException(string message = "A concurrent update conflict occurred for this account. Please retry the request.") 
            : base(message, statusCode: 409, typeReturn: "warning") { }
    }
}
