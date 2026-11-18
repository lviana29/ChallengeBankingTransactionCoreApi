using ChallengeBankingTransactionCore.Service.Application.Transaction.Dto;
using ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.ProcessTransactionUseCase;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Controllers.V1 {
    [ApiController]
    [Authorize]
    [Route("api/[controller]/v1")]
    public class TransactionController : ControllerBase {
        private readonly IProcessTransactionUseCase _processTransactionUseCase;
        public TransactionController(IProcessTransactionUseCase processTransactionUseCase) {
            _processTransactionUseCase = processTransactionUseCase;
        }

        [HttpPost]
        [ProducesResponseType(typeof(string), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Post([FromBody] TransactionDto dto) {
            var result = await _processTransactionUseCase.ExecuteAsync(dto);
            return StatusCode(StatusCodes.Status201Created, result);
        }
    }
}
