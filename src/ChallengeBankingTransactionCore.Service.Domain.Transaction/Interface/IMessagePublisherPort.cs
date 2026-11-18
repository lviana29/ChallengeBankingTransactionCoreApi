using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;

namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface {
    public interface IMessagePublisherPort {
        Task PublishTransactionCreatedAsync(TransactionCreatedEventEntity eventMessage);
    }
}
