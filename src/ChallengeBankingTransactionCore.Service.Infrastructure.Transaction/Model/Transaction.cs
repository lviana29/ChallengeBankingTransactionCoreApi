using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Base;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model {
    [Table("Transaction")]
    public class Transaction : BaseEntity {
        [Required]
        public string Description { get; set; } = String.Empty;
        [Required]
        public string AccountId { get; set; } = String.Empty;
        [Required]
        public string IdempotencyKey { get; set; } = String.Empty;
        [Required]
        public decimal Amount { get; set; } = 0;
        [Required]
        public decimal BalanceAfter { get; set; } = 0;
        [Required]
        public int TransactionTypeId { get; set; } = 0;
        [Required]
        public DateTime CreatedAt { get; set; } = new();
        [Required]
        public DateTime UpdatedAt { get; set; } = new();
        public TransactionType Type { get; set; } = new();

    }
}
