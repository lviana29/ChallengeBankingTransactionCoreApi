using ChallengeBankingTransactionCore.Service.Application.Transaction.Dto;

namespace ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.ProcessTransactionUseCase {
    public interface IProcessTransactionUseCase {
        Task<string> ExecuteAsync(TransactionDto transactionDto);
    }
}
