namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities {
    public class ProcessedEventEntity {
        public Guid EventId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }
}
