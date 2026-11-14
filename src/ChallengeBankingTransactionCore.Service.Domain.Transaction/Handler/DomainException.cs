
namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler {
    public abstract class DomainException : Exception {
        public abstract string ErrorCode { get; }
        public int StatusCode { get; }
        public string TypeReturn { get; }

        protected DomainException(string message, int statusCode = 400, string typeReturn = "warning")
            : base(message) {
            StatusCode = statusCode;
            TypeReturn = typeReturn;
        }
    }
}
