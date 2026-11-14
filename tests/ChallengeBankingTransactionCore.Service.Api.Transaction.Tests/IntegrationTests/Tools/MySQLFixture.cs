using System.Data.Common;
using Testcontainers.MySql;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Configurations;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.IntegrationTests.Tools {
    public class MySQLFixture : IAsyncLifetime {
        public MySqlContainer Container { get; }
        public string ConnectionString => Container.GetConnectionString();

        public MySQLFixture() {
            Container = new MySqlBuilder()
                .WithDatabase("ChallengeBankingTransactionCore_Transaction_IntegrationTestDb")
                .WithUsername("root")
                .WithPassword("@admin123")
                .Build();
        }

        public async Task InitializeAsync() {
            await Container.StartAsync();
            EvolveConfig.ExecuteEvolveMigration(ConnectionString);
        }

        public async Task DisposeAsync() {
            await Container.DisposeAsync();
        }
    }
}
