using ChallengeBankingTransactionCore.Service.Api.Transaction.Configurations;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Configurations;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repositories;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Repositories.Implementation;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Adapter;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Application.Transaction.UseCases.ProcessTransactionUseCase;
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


builder.Services.AddCorsConfiguration(builder.Configuration);

// Repositories (Infrastructure)
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IResiliencePolicyExecutorPort, PollyResiliencePolicyExecutorAdapter>();
builder.Services.AddScoped<IGetConsolidatedStatementUseCase, GetConsolidatedStatementUseCase>();
builder.Services.AddScoped<IMessagePublisherPort, RabbitMQMessagePublisherAdapter>();

// Use cases (Application)
builder.Services.AddScoped<IProcessTransactionUseCase, ProcessTransactionUseCase>();

// Background Workers (Configurable via Hosting:EnableOutboxRelay)
var enableOutboxRelay = !bool.TryParse(builder.Configuration["Hosting:EnableOutboxRelay"], out var parsedEnableOutbox) || parsedEnableOutbox;
if (enableOutboxRelay) {
    builder.Services.AddHostedService<OutboxRelayHostedService>();
}

var app = builder.Build();

//middlewares
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsProduction()) {
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseCorsConfiguration(builder.Configuration);

app.UseAuthorization();

app.MapControllers();

app.UseSwaggerSpecification();
app.UseScalarConfiguration();

app.Run();
