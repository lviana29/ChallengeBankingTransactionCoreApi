
using System.Runtime.Serialization;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Configurations {
    public static class CorsConfig {
        public static void AddCorsConfiguration(this IServiceCollection services, IConfiguration configuration) {
            var origins = GetAllowedOrigins(configuration);

            services.AddCors(options => {
                options.AddPolicy("DefaultPolicy",
                policy => policy.WithOrigins(origins)
                                .AllowAnyMethod()
                                .AllowAnyHeader()
                                .AllowCredentials());
            });
        }

        public static IApplicationBuilder UseCorsConfiguration(this IApplicationBuilder app, IConfiguration configuration) {
            //app.UseCors();
            var origins = GetAllowedOrigins(configuration);
            app.Use(async (context, next) => {
                var origin = context.Request.Headers["Origin"].ToString();
                if (!string.IsNullOrEmpty(origin) &&
                    !origins.Contains(origin, StringComparer.OrdinalIgnoreCase)) {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("CORS origin not allowed.");
                    return;
                }
                await next();
            });

            app.UseCors("DefaultPolicy"); //adding cors globally
            return app;
        }

        private static string[] GetAllowedOrigins(IConfiguration configuration) {
            return configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
        }
    }
}
