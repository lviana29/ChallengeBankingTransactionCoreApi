using Microsoft.OpenApi;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Configurations {
    public static class SwaggerConfig {
        private static readonly string _AppName = "API_NAME";
        private static readonly string _AppDescription = $"{_AppName} - Test Project for Asp.Net 10 Web API";
        private static readonly string _AppVersion = "v1";

        public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services) {
            services.AddSwaggerGen(c => {
                c.SwaggerDoc(_AppVersion, new OpenApiInfo {
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

                c.CustomSchemaIds(type => type.FullName);
            });
            return services;
        }

        public static IApplicationBuilder UseSwaggerSpecification(this IApplicationBuilder app) {
            app.UseSwagger();
            app.UseSwaggerUI(c => {
                c.SwaggerEndpoint($"/swagger/{_AppVersion}/swagger.json", $"{_AppName} {_AppVersion}");
                c.RoutePrefix = "swagger-ui"; // Set Swagger UI at the app's root
                c.DocumentTitle = $"{_AppName} - Swagger UI";
            });

            return app;
        }
    }
}
