namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions {
    public class InvalidDateRangeException : DomainException {
        public override string ErrorCode => "CORE_BANKING_009";

        public InvalidDateRangeException() : base("Start date must be less than or equal to end date", statusCode: 422, typeReturn: "warning") { }
    }
}
