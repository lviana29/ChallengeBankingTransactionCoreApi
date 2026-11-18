using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Configurations;

[ExcludeFromCodeCoverage]
public static class DataBaseConfig {
    public static IServiceCollection AddDatabaseConfiguration(this IServiceCollection services, IConfiguration configuration) {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString)) {
            throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseMySQL(connectionString));

        return services;
    }
}
