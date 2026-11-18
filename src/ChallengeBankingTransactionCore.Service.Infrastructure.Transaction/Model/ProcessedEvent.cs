using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model {
    [Table("ProcessedEvent")]
    public class ProcessedEvent {
        [Key]
        public Guid EventId { get; set; }

        [Required]
        [MaxLength(100)]
        public string EventType { get; set; } = string.Empty;

        [Required]
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }
}
