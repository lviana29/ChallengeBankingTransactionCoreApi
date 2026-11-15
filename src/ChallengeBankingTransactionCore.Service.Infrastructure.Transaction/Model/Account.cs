using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Base;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model {
    [Table("Account")]
    public class Account : BaseEntity {
        [Required]
        public string AccountNumber { get; set; } = String.Empty;
        [Required]
        public decimal Balance { get; set; }
        [Required]
        public bool IsActive { get; set; }
        [Required]
        [ConcurrencyCheck]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime RowVersion { get; set; }
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime CreatedAt { get; set; }
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime UpdatedAt { get; set; }
    }
}
