using ChallengeBankingTransactionCore.Service.Application.Transaction.Dto;
using ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.ProcessTransactionUseCase;
using Microsoft.AspNetCore.Mvc;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Controllers.V1 {
    [ApiController]
    [Route("api/[controller]/v1")]
    public class TransactionController : ControllerBase {
        private readonly IProcessTransactionUseCase _processTransactionUseCase;
        public TransactionController(IProcessTransactionUseCase processTransactionUseCase) {
            _processTransactionUseCase = processTransactionUseCase;
        }

        [HttpPost]
        [ProducesResponseType(typeof(string), 201)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        [ProducesResponseType(422)]
        public async Task<IActionResult> Post([FromBody] TransactionDto dto) {
            var result = await _processTransactionUseCase.ExecuteAsync(dto);
            return Ok(result);
        }
    }
}
