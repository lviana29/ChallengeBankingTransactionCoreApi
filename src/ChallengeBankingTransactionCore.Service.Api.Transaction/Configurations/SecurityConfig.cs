using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Configurations {
    public static class SecurityConfig {
        public const string SchemeName = "ApiKeyOrBearer";
        public const string DefaultDevApiKey = "challenge-banking-dev-key-2026";

        public static IServiceCollection AddSecurityConfiguration(this IServiceCollection services, IConfiguration configuration) {
            services.AddAuthentication(options => {
                options.DefaultAuthenticateScheme = SchemeName;
                options.DefaultChallengeScheme = SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyOrBearerAuthHandler>(SchemeName, null);

            services.AddAuthorization();
            return services;
        }
    }

    public class ApiKeyOrBearerAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions> {
        private readonly IConfiguration _configuration;

        public ApiKeyOrBearerAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IConfiguration configuration
        ) : base(options, logger, encoder) {
            _configuration = configuration;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
            var requireAuth = bool.TryParse(_configuration["Security:RequireAuthentication"], out var parsedRequire) && parsedRequire;
            var expectedKey = _configuration["Security:ApiKey"];
            if (string.IsNullOrWhiteSpace(expectedKey)) {
                expectedKey = SecurityConfig.DefaultDevApiKey;
            }

            string? providedToken = null;

            if (Request.Headers.TryGetValue("X-Api-Key", out var apiKeyValues) && !string.IsNullOrWhiteSpace(apiKeyValues.ToString())) {
                providedToken = apiKeyValues.ToString().Trim();
            }
            else if (Request.Headers.TryGetValue("Authorization", out var authValues)) {
                var authHeader = authValues.ToString();
                if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
                    providedToken = authHeader["Bearer ".Length..].Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(providedToken)) {
                if (string.Equals(providedToken, expectedKey, StringComparison.Ordinal)) {
                    var claims = new[] {
                        new Claim(ClaimTypes.Name, "BankingApiClient"),
                        new Claim(ClaimTypes.Role, "BankingClient")
                    };
                    var identity = new ClaimsIdentity(claims, Scheme.Name);
                    var principal = new ClaimsPrincipal(identity);
                    var ticket = new AuthenticationTicket(principal, Scheme.Name);
                    return Task.FromResult(AuthenticateResult.Success(ticket));
                }

                if (requireAuth) {
                    return Task.FromResult(AuthenticateResult.Fail("Invalid credentials"));
                }
            }

            if (!requireAuth) {
                var devClaims = new[] {
                    new Claim(ClaimTypes.Name, "DevelopmentBankingClient"),
                    new Claim(ClaimTypes.Role, "BankingClient")
                };
                var devIdentity = new ClaimsIdentity(devClaims, Scheme.Name);
                var devPrincipal = new ClaimsPrincipal(devIdentity);
                var devTicket = new AuthenticationTicket(devPrincipal, Scheme.Name);
                return Task.FromResult(AuthenticateResult.Success(devTicket));
            }

            return Task.FromResult(AuthenticateResult.Fail("Missing API Key or Bearer Token"));
        }
    }
}
