using System.Diagnostics.CodeAnalysis;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Configurations {
    [ExcludeFromCodeCoverage]
    public static class RouteConfig {
        public static IServiceCollection AddRouteConfiguration(this IServiceCollection services) {
            services.Configure<RouteOptions>(options => {
                options.LowercaseUrls = true;
                options.LowercaseQueryStrings = true;
            });

            return services;
        }
    }
}
