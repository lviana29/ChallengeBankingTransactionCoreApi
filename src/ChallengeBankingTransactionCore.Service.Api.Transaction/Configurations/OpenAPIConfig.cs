using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Configurations {
    [ExcludeFromCodeCoverage]
    public static class OpenAPIConfig {
        private static readonly string _AppName = "Challenge Banking Transaction Core API";
        private static readonly string _AppDescription = $"{_AppName} - Microsserviço de Core Banking para gestão de contas e transações financeiras com foco em concorrência, consistência e idempotência.";
        private static readonly string _AppVersion = "v1";

        public static IServiceCollection AddOpenAPIConfiguration(this IServiceCollection services) {
            services.AddSingleton<OpenApiInfo>(new OpenApiInfo {
                Title = _AppName,
                Version = _AppVersion,
                Description = _AppDescription,
                Contact = new OpenApiContact {
                    Name = "Leonardo Viana",
                    Email = "leonardovianaamorim@gmail.com"
                },
                License = new OpenApiLicense {
                    Name = "MIT License",
                    Url = new Uri("https://opensource.org/licenses/MIT")
                }
            });

            return services;
        }
    }
}
