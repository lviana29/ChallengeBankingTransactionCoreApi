using System.Collections.Generic;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.IntegrationTests.Tools {
    public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class {
        private readonly string _connectionString;
        private readonly IDictionary<string, string?>? _overrideConfig;

        public CustomWebApplicationFactory(string connectionString, IDictionary<string, string?>? overrideConfig = null) {
            _connectionString = connectionString;
            _overrideConfig = overrideConfig;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            builder.ConfigureAppConfiguration((context, config) => {
                var testConfigPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "appsettings.Test.json");
                config.Sources.Clear();
                config.AddJsonFile(testConfigPath, optional: false, reloadOnChange: true);
                if (_overrideConfig != null) {
                    config.AddInMemoryCollection(_overrideConfig);
                }
            });
            builder.ConfigureServices(services => {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) {
                    services.Remove(descriptor);
                    services.AddDbContext<AppDbContext>(options =>
                        options.UseMySQL(_connectionString));
                }
            });
        }
    }
}
