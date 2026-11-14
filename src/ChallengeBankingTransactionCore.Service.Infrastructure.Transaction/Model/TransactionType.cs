using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Base;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model {
    [Table("TransactionType")]
    public class TransactionType : BaseEntity {
        [Required]
        public string Title { get; set; } = String.Empty;
    }
}
