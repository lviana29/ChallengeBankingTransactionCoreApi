using ChallengeBankingTransactionCore.Service.Api.Transaction.Configurations;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Configurations;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repositories;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repositories.Implementation;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Adapter;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.ProcessTransactionUseCase;
using ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.ProcessTransactionEventUseCase;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repository.Implementation;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repository;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Workers;
using ChallengeBankingTransactionCore.Service.Api.Transaction.Middlewares;
using ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.GetConsolidatedStatementUseCase;

var builder = WebApplication.CreateBuilder(args);

// configurations
builder.AddSerilogConfiguration();
builder.Services.AddSingleton(typeof(IAppLogger<>), typeof(SerilogAdapter<>));

builder.Services.AddDatabaseConfiguration(builder.Configuration);
builder.Services.AddEvolveConfiguration(builder.Configuration, builder.Environment);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenAPIConfiguration();
builder.Services.AddSwaggerConfiguration();
builder.Services.AddSwaggerGen();
builder.Services.AddRouteConfiguration();

builder.Services.AddDataProtection();
builder.Services.AddMemoryCache();

var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnection)) {
    builder.Services.AddStackExchangeRedisCache(options => {
        options.Configuration = redisConnection;
        options.InstanceName = "BankingCore:";
    });
}
else {
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddCorsConfiguration(builder.Configuration);
builder.Services.AddSecurityConfiguration(builder.Configuration);

// Repositories (Infrastructure)
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IDailyConsolidatedBalanceRepository, DailyConsolidatedBalanceRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IResiliencePolicyExecutorPort, PollyResiliencePolicyExecutorAdapter>();
builder.Services.AddSingleton<IConsolidatedCachePort, ConsolidatedCacheAdapter>();
builder.Services.AddScoped<IGetConsolidatedStatementUseCase, GetConsolidatedStatementUseCase>();
builder.Services.AddSingleton<IMessagePublisherPort, RabbitMQMessagePublisherAdapter>();

// Use cases (Application)
builder.Services.AddScoped<IProcessTransactionUseCase, ProcessTransactionUseCase>();
builder.Services.AddScoped<IProcessTransactionEventUseCase, ProcessTransactionEventUseCase>();

// Background Workers (Configurable via Hosting:EnableOutboxRelay & Hosting:EnableConsolidationWorker)
var enableOutboxRelay = !bool.TryParse(builder.Configuration["Hosting:EnableOutboxRelay"], out var parsedEnableOutbox) || parsedEnableOutbox;
if (enableOutboxRelay) {
    builder.Services.AddHostedService<OutboxRelayHostedService>();
}

var enableConsolidationWorker = !bool.TryParse(builder.Configuration["Hosting:EnableConsolidationWorker"], out var parsedEnableConsolidation) || parsedEnableConsolidation;
if (enableConsolidationWorker) {
    builder.Services.AddHostedService<ConsolidationWorkerHostedService>();
}

var app = builder.Build();

//middlewares
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsProduction()) {
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseCorsConfiguration(builder.Configuration);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.UseSwaggerSpecification();
app.UseScalarConfiguration();

app.Run();
