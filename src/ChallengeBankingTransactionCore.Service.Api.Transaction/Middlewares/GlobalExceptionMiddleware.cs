using System.Text.Json;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Middlewares {
    public class GlobalExceptionMiddleware {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger) {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context) {
            try {
                // Continua o fluxo da requisição
                await _next(context);
            }
            catch (Exception exception) {
                _logger.LogError(exception, "Ocorreu um erro não tratado: {Message}", exception.Message);
                await HandleExceptionAsync(context, exception);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception) {
            context.Response.ContentType = "application/json";

            if (exception is DomainException domainException) {
                context.Response.StatusCode = domainException.StatusCode;

                var errorResponse = new {
                    errorCode = domainException.ErrorCode,
                    message = domainException.Message,
                    typeReturn = domainException.TypeReturn
                };

                var jsonResponse = JsonSerializer.Serialize(errorResponse);
                await context.Response.WriteAsync(jsonResponse);
                return;
            }

            // 2. Fallback para Erros Inesperados (500)
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            var unexpectedResponse = new {
                errorCode = "INTERNAL_SERVER_ERROR",
                message = "An unexpected error occurred",
                typeReturn = "error"
            };

            var jsonInternalResponse = JsonSerializer.Serialize(unexpectedResponse);
            await context.Response.WriteAsync(jsonInternalResponse);
        }
    }
}

