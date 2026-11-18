using ChallengeBankingTransactionCore.Service.Domain.Transaction.Entities;

namespace ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.ProcessTransactionEventUseCase {
    public interface IProcessTransactionEventUseCase {
        Task<bool> ExecuteAsync(TransactionCreatedEventEntity transactionEvent, CancellationToken cancellationToken = default);
    }
}
