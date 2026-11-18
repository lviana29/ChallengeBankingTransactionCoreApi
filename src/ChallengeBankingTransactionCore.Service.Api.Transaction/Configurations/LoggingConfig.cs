using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Configurations {
    [ExcludeFromCodeCoverage]
    public static class LoggingConfig {
        public static void AddSerilogConfiguration(this WebApplicationBuilder builder) {
            Log.Logger = new LoggerConfiguration()
               .ReadFrom.Configuration(builder.Configuration)
               .Enrich.FromLogContext()
               .WriteTo.Console()
               .WriteTo.Debug()
               .CreateLogger();
            builder.Host.UseSerilog();
        }
    }
}
