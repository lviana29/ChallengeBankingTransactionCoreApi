using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Adapter {
    public class PollyResiliencePolicyExecutorAdapter : IResiliencePolicyExecutorPort {
        private readonly ResiliencePipeline _pipeline;
        public PollyResiliencePolicyExecutorAdapter() {
            _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder().Handle<DbUpdateException>(ex => IsTransient(ex))
            })
            .Build();
        }
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action) {
            return await _pipeline.ExecuteAsync(async state => await action(), CancellationToken.None);
        }

        public async Task ExecuteAsyncWithoutResult(Func<Task> action) {
            await _pipeline.ExecuteAsync(async state => {
                await action();
                return true;
            }, CancellationToken.None);
        }

        private bool IsTransient(DbUpdateException ex) {
            if (ex.InnerException != null) {
                var message = ex.InnerException.Message.ToLower();
                return message.Contains("timeout") ||
                       message.Contains("connection") ||
                       message.Contains("network") ||
                       message.Contains("server has gone away");
            }
            return false;
        }
    }
}
