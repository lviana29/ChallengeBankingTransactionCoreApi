using Scalar.AspNetCore;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Configurations {
    public static class ScalarConfig {

        private static readonly string _AppName = "API_NAME";
        private static readonly string _AppVersion = "v1";
        public static WebApplication UseScalarConfiguration(this WebApplication app) {
            app.MapScalarApiReference("/scalar", options => {
                options.WithTitle(_AppName)
                .WithOpenApiRoutePattern($"/swagger/{_AppVersion}/swagger.json");

            });

            return app;
        }
    }
}
