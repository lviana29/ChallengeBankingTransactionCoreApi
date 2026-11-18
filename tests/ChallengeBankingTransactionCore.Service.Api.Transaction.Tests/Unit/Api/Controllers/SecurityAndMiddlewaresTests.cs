using System.Text.Encodings.Web;
using System.Text.Json;
using ChallengeBankingTransactionCore.Service.Api.Transaction.Configurations;
using ChallengeBankingTransactionCore.Service.Api.Transaction.Middlewares;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Handler.Exceptions;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Api.Controllers {
    public class SecurityAndMiddlewaresTests {
        [Theory]
        [InlineData("12345-6", "12***-6")]
        [InlineData("987654-0", "98***-0")]
        [InlineData("999999", "99***9")]
        [InlineData("1234", "12***4")]
        [InlineData("123", "***")]
        [InlineData("12", "***")]
        [InlineData("", "***")]
        [InlineData("   ", "***")]
        [InlineData(null, "***")]
        public void SensitiveDataMasker_MaskAccountNumber_ShouldMaskProperly(string? input, string expected) {
            // Act
            var masked = SensitiveDataMasker.MaskAccountNumber(input);

            // Assert
            Assert.Equal(expected, masked);
        }

        [Fact]
        public async Task GlobalExceptionMiddleware_ShouldPassThrough_WhenNoExceptionOccurs() {
            // Arrange
            var context = new DefaultHttpContext();
            var nextCalled = false;
            RequestDelegate next = _ => {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var loggerMock = new Mock<ILogger<GlobalExceptionMiddleware>>();
            var middleware = new GlobalExceptionMiddleware(next, loggerMock.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(nextCalled);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }

        [Fact]
        public async Task GlobalExceptionMiddleware_ShouldHandleDomainException_AndReturnExpectedStatusCodeAndJson() {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            context.Request.QueryString = new QueryString("?AccountNumber=12345-6");

            RequestDelegate next = _ => throw new InvalidAccountNumber();

            var loggerMock = new Mock<ILogger<GlobalExceptionMiddleware>>();
            var middleware = new GlobalExceptionMiddleware(next, loggerMock.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(StatusCodes.Status422UnprocessableEntity, context.Response.StatusCode);
            Assert.Equal("application/json", context.Response.ContentType);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var bodyText = await new StreamReader(context.Response.Body).ReadToEndAsync();
            using var doc = JsonDocument.Parse(bodyText);
            Assert.Equal("CORE_BANKING_007", doc.RootElement.GetProperty("errorCode").GetString());
        }

        [Fact]
        public async Task GlobalExceptionMiddleware_ShouldHandleGenericException_AndReturn500InternalServerError() {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            RequestDelegate next = _ => throw new InvalidOperationException("Unexpected database failure");

            var loggerMock = new Mock<ILogger<GlobalExceptionMiddleware>>();
            var middleware = new GlobalExceptionMiddleware(next, loggerMock.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
            Assert.Equal("application/json", context.Response.ContentType);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var bodyText = await new StreamReader(context.Response.Body).ReadToEndAsync();
            using var doc = JsonDocument.Parse(bodyText);
            Assert.Equal("INTERNAL_SERVER_ERROR", doc.RootElement.GetProperty("errorCode").GetString());
            Assert.Equal("An unexpected error occurred", doc.RootElement.GetProperty("message").GetString());
            Assert.Equal("error", doc.RootElement.GetProperty("typeReturn").GetString());
        }

        private static async Task<AuthenticateResult> ExecuteAuthHandlerAsync(
            Dictionary<string, string?> configData,
            Action<HttpRequest>? configureRequest = null
        ) {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var optionsMonitorMock = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
            optionsMonitorMock
                .Setup(o => o.Get(It.IsAny<string>()))
                .Returns(new AuthenticationSchemeOptions());

            var handler = new ApiKeyOrBearerAuthHandler(
                optionsMonitorMock.Object,
                NullLoggerFactory.Instance,
                UrlEncoder.Default,
                configuration
            );

            var scheme = new AuthenticationScheme(
                SecurityConfig.SchemeName,
                SecurityConfig.SchemeName,
                typeof(ApiKeyOrBearerAuthHandler)
            );

            var context = new DefaultHttpContext();
            configureRequest?.Invoke(context.Request);

            await handler.InitializeAsync(scheme, context);
            return await handler.AuthenticateAsync();
        }

        [Fact]
        public async Task ApiKeyOrBearerAuthHandler_ShouldSucceed_WhenRequireAuthenticationIsFalseAndNoHeaderProvided() {
            // Arrange
            var config = new Dictionary<string, string?> {
                ["Security:RequireAuthentication"] = "false"
            };

            // Act
            var result = await ExecuteAuthHandlerAsync(config);

            // Assert
            Assert.True(result.Succeeded);
            Assert.Equal("DevelopmentBankingClient", result.Principal?.Identity?.Name);
        }

        [Fact]
        public async Task ApiKeyOrBearerAuthHandler_ShouldSucceed_WhenValidXApiKeyIsProvided() {
            // Arrange
            var config = new Dictionary<string, string?> {
                ["Security:RequireAuthentication"] = "true",
                ["Security:ApiKey"] = "secret-api-key-999"
            };

            // Act
            var result = await ExecuteAuthHandlerAsync(config, req => {
                req.Headers["X-Api-Key"] = "secret-api-key-999";
            });

            // Assert
            Assert.True(result.Succeeded);
            Assert.Equal("BankingApiClient", result.Principal?.Identity?.Name);
        }

        [Fact]
        public async Task ApiKeyOrBearerAuthHandler_ShouldSucceed_WhenValidBearerTokenIsProvided() {
            // Arrange
            var config = new Dictionary<string, string?> {
                ["Security:RequireAuthentication"] = "true",
                ["Security:ApiKey"] = "secret-bearer-token-123"
            };

            // Act
            var result = await ExecuteAuthHandlerAsync(config, req => {
                req.Headers["Authorization"] = "Bearer secret-bearer-token-123";
            });

            // Assert
            Assert.True(result.Succeeded);
            Assert.Equal("BankingApiClient", result.Principal?.Identity?.Name);
        }

        [Fact]
        public async Task ApiKeyOrBearerAuthHandler_ShouldFail_WhenRequireAuthenticationIsTrueAndHeaderIsMissing() {
            // Arrange
            var config = new Dictionary<string, string?> {
                ["Security:RequireAuthentication"] = "true",
                ["Security:ApiKey"] = "secret-key"
            };

            // Act
            var result = await ExecuteAuthHandlerAsync(config);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Equal("Missing API Key or Bearer Token", result.Failure?.Message);
        }

        [Fact]
        public async Task ApiKeyOrBearerAuthHandler_ShouldFail_WhenRequireAuthenticationIsTrueAndKeyIsInvalid() {
            // Arrange
            var config = new Dictionary<string, string?> {
                ["Security:RequireAuthentication"] = "true",
                ["Security:ApiKey"] = "secret-key"
            };

            // Act
            var result = await ExecuteAuthHandlerAsync(config, req => {
                req.Headers["X-Api-Key"] = "wrong-key";
            });

            // Assert
            Assert.False(result.Succeeded);
            Assert.Equal("Invalid credentials", result.Failure?.Message);
        }

        [Fact]
        public void AddSecurityConfiguration_ShouldRegisterAuthenticationAndAuthorizationServices() {
            // Arrange
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().Build();

            // Act
            services.AddLogging();
            services.AddSecurityConfiguration(config);
            using var provider = services.BuildServiceProvider();

            // Assert
            var schemeProvider = provider.GetService<IAuthenticationSchemeProvider>();
            Assert.NotNull(schemeProvider);
        }

        [Fact]
        public void AddCorsConfiguration_ShouldRegisterCorsService() {
            // Arrange
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> {
                    ["Cors:Origins:0"] = "http://localhost:3050"
                })
                .Build();

            // Act
            services.AddCorsConfiguration(config);
            using var provider = services.BuildServiceProvider();

            // Assert
            Assert.NotNull(provider);
        }

        [Fact]
        public async Task UseCorsConfiguration_ShouldReturn403Forbidden_WhenOriginIsDisallowed_AndAllowWhenOriginIsAllowedOrEmpty() {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Cors:Origins:0"] = "http://localhost:3000"
            });
            builder.Services.AddCorsConfiguration(builder.Configuration);

            await using var app = builder.Build();
            app.UseCorsConfiguration(builder.Configuration);

            var nextCalled = 0;
            app.Run(ctx => {
                nextCalled++;
                ctx.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            });

            var pipeline = ((IApplicationBuilder)app).Build();

            // Act 1: Disallowed origin -> 403
            var disallowedContext = new DefaultHttpContext();
            disallowedContext.Request.Headers["Origin"] = "http://malicious.site.com";
            disallowedContext.Response.Body = new MemoryStream();
            await pipeline(disallowedContext);

            // Act 2: Allowed origin -> 200
            var allowedContext = new DefaultHttpContext();
            allowedContext.Request.Headers["Origin"] = "http://localhost:3000";
            await pipeline(allowedContext);

            // Act 3: Empty origin -> 200
            var emptyOriginContext = new DefaultHttpContext();
            await pipeline(emptyOriginContext);

            // Assert
            Assert.Equal(StatusCodes.Status403Forbidden, disallowedContext.Response.StatusCode);
            Assert.Equal(StatusCodes.Status200OK, allowedContext.Response.StatusCode);
            Assert.Equal(StatusCodes.Status200OK, emptyOriginContext.Response.StatusCode);
            Assert.Equal(2, nextCalled);
        }
    }
}
