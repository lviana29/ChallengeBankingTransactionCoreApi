using ChallengeBankingTransactionCore.Service.Application.Transaction.Dto;
using ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.GetConsolidatedStatementUseCase;
using Microsoft.AspNetCore.Mvc;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Controllers.V1 {
    [ApiController]
    [Route("api/[controller]/v1")]
    public class ConsolidatedStatementController : ControllerBase {
        private readonly IGetConsolidatedStatementUseCase _getConsolidatedStatementUseCase;

        public ConsolidatedStatementController(IGetConsolidatedStatementUseCase getConsolidatedStatementUseCase) {
            _getConsolidatedStatementUseCase = getConsolidatedStatementUseCase;
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<ConsolidatedStatementDto>), 200)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        [ProducesResponseType(422)]
        public async Task<IActionResult> GetConsolidated([FromQuery] ConsolidatedTransactionsFiltersDto dto) {
            var result = await _getConsolidatedStatementUseCase.ExecuteAsync(dto);
            return Ok(result);
        }
    }
}
