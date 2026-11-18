using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Base;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model {
    [Table("DailyConsolidatedBalance")]
    public class DailyConsolidatedBalance : BaseEntity {
        [Required]
        public Guid AccountId { get; set; }

        [Required]
        [MaxLength(20)]
        public string AccountNumber { get; set; } = string.Empty;

        [Required]
        public DateTime BalanceDate { get; set; }

        [Required]
        public decimal TotalCredits { get; set; }

        [Required]
        public decimal TotalDebits { get; set; }

        [Required]
        public decimal ClosingBalance { get; set; }

        [Required]
        public int TransactionCount { get; set; }

        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
