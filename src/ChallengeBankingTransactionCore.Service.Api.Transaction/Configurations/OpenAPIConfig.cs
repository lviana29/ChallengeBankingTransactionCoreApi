using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Configurations {
    public static class OpenAPIConfig {
        private static readonly string _AppName = "API_NAME";
        private static readonly string _AppDescription = $"{_AppName} - Test Project for Asp.Net 10 Web API";
        private static readonly string _AppVersion = "v1";

        public static IServiceCollection AddOpenAPIConfiguration(this IServiceCollection services) {
            services.AddSingleton<OpenApiInfo>(new OpenApiInfo {
                Title = _AppName,
                Version = _AppVersion,
                Description = _AppDescription,
                Contact = new OpenApiContact {
                    Name = "Leo",
                    Email = "leo@example.com",
                    Url = new Uri("http://www.example.com")
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
