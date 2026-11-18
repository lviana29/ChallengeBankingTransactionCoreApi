using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities {
    public record TransactionCreatedEventEntity(
        Guid TransactionId,
        Guid AccountId,
        decimal Amount,
        string TransactionType,
        DateTime CreatedAt
    );
}
