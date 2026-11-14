using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using EvolveDb;
using MySql.Data.MySqlClient;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Configurations;

public static class EvolveConfig {
    public static IServiceCollection AddEvolveConfiguration(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment) {
        if (environment.IsDevelopment()) {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString)) {
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            }

            try {
                ExecuteEvolveMigration(connectionString);
            }
            catch (Exception ex) {
                Log.Error(ex, "Evolve migration failed");
                throw;
            }
        }

        return services;
    }

    private static void ExecuteEvolveMigration(string connectionString) {
        var baseDirectory = AppContext.BaseDirectory;
        var migrationsPath = Path.Combine(baseDirectory, "Database/Migrations");
        var datasetPath = Path.Combine(baseDirectory, "Database/Dataset");

        using var cnn = new MySqlConnection(connectionString);
        var evolve = new Evolve(cnn, msg => Log.Information(msg)) {
            Locations = new[] { migrationsPath, datasetPath },
            IsEraseDisabled = true,
            CommandTimeout = 60
        };
        evolve.Repair();
        evolve.Migrate();
    }
}
