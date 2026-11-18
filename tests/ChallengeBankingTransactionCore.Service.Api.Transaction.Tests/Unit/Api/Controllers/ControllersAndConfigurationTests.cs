using ChallengeBankingTransactionCore.Service.Api.Transaction.Controllers.V1;
using ChallengeBankingTransactionCore.Service.Application.Transaction.Dto;
using ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.GetConsolidatedStatementUseCase;
using ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.ProcessTransactionUseCase;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Api.Controllers {
    public class ControllersAndConfigurationTests {
        [Fact]
        public async Task TransactionController_Post_ShouldReturn201CreatedWithTransactionId() {
            // Arrange
            var useCaseMock = new Mock<IProcessTransactionUseCase>();
            var expectedTxId = Guid.NewGuid().ToString();
            var dto = new TransactionDto("12345-6", 150.00m, 1, Guid.NewGuid().ToString());

            useCaseMock
                .Setup(u => u.ExecuteAsync(dto))
                .ReturnsAsync(expectedTxId);

            var controller = new TransactionController(useCaseMock.Object);

            // Act
            var actionResult = await controller.Post(dto);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(actionResult);
            Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
            Assert.Equal(expectedTxId, objectResult.Value);
            useCaseMock.Verify(u => u.ExecuteAsync(dto), Times.Once);
        }

        [Fact]
        public async Task ConsolidatedStatementController_GetConsolidated_ShouldReturn200OkWithStatement() {
            // Arrange
            var useCaseMock = new Mock<IGetConsolidatedStatementUseCase>();
            var filters = new ConsolidatedTransactionsFiltersDto("12345-6", null, null);
            var expectedStatement = new ConsolidatedStatementDto(
                "12345-6",
                1000.00m,
                1000.00m,
                DateTime.UtcNow,
                new List<ConsolidatedTransactionsDto>()
            );

            useCaseMock
                .Setup(u => u.ExecuteAsync(filters))
                .ReturnsAsync(expectedStatement);

            var controller = new ConsolidatedStatementController(useCaseMock.Object);

            // Act
            var actionResult = await controller.GetConsolidated(filters);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
            Assert.Equal(expectedStatement, okResult.Value);
            useCaseMock.Verify(u => u.ExecuteAsync(filters), Times.Once);
        }
    }
}
