using ChallengeBankingTransactionCore.Service.Application.Transaction.Dto;

namespace ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.GetConsolidatedStatementUseCase {
    public interface IGetConsolidatedStatementUseCase {
        public Task<ConsolidatedStatementDto> ExecuteAsync(ConsolidatedTransactionsFiltersDto filtersDto);
    }
}
