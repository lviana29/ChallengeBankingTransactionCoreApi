using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Base;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model {
    [Table("Transaction")]
    public class Transaction : BaseEntity {
        [Required]
        public string Description { get; set; } = String.Empty;
        [Required]
        public Guid AccountId { get; set; } = new Guid();
        [Required]
        public Guid IdempotencyKey { get; set; } = new Guid();
        [Required]
        public decimal Amount { get; set; } = 0;
        [Required]
        public decimal BalanceAfter { get; set; } = 0;
        [Required]
        public Guid TransactionTypeId { get; set; } = new Guid();
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime CreatedAt { get; set; }
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime UpdatedAt { get; set; }
        public TransactionType Type { get; set; } = new();

    }
}
