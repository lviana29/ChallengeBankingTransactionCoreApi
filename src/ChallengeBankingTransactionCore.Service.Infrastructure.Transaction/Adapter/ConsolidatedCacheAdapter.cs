using System.Text.Json;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Utils;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Adapter {
    public class ConsolidatedCacheAdapter : IConsolidatedCachePort {
        private readonly IDistributedCache _distributedCache;
        private readonly IMemoryCache _memoryCache;
        private readonly IAppLogger<ConsolidatedCacheAdapter> _logger;

        public ConsolidatedCacheAdapter(
            IDistributedCache distributedCache,
            IMemoryCache memoryCache,
            IAppLogger<ConsolidatedCacheAdapter> logger
        ) {
            _distributedCache = distributedCache;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        private async Task<string> GetAccountVersionAsync(string accountNumber, CancellationToken cancellationToken) {
            var versionKey = $"account-version:{accountNumber}";

            try {
                var distributedVersion = await _distributedCache.GetStringAsync(versionKey, cancellationToken);
                if (!string.IsNullOrWhiteSpace(distributedVersion)) {
                    _memoryCache.Set(versionKey, distributedVersion, TimeSpan.FromHours(1));
                    return distributedVersion;
                }
            }
            catch (Exception ex) {
                var maskedAccount = SensitiveDataMasker.MaskAccountNumber(accountNumber);
                _logger.LogWarning("Distributed cache unavailable when reading version for account {AccountNumber}. Falling back to MemoryCache: {ErrorMessage}", maskedAccount, ex.Message);
            }

            if (_memoryCache.TryGetValue<string>(versionKey, out var memVersion) && !string.IsNullOrWhiteSpace(memVersion)) {
                return memVersion;
            }

            var initialVersion = "v1";
            _memoryCache.Set(versionKey, initialVersion, TimeSpan.FromHours(1));
            return initialVersion;
        }

        private async Task<string> ResolveVersionedKeyAsync(string rawKey, CancellationToken cancellationToken) {
            // Expected format: consolidated:v1:{accountNumber}:{suffix}
            var parts = rawKey.Split(':', 4);
            if (parts.Length >= 3) {
                var accountNumber = parts[2];
                var version = await GetAccountVersionAsync(accountNumber, cancellationToken);
                var suffix = parts.Length == 4 ? parts[3] : "default";
                return $"consolidated:v1:{accountNumber}:ver:{version}:{suffix}";
            }

            return rawKey;
        }

        private static string MaskCacheKey(string key) {
            var parts = key.Split(':', 4);
            if (parts.Length >= 3) {
                parts[2] = SensitiveDataMasker.MaskAccountNumber(parts[2]);
                return string.Join(':', parts);
            }
            return key;
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class {
            var versionedKey = await ResolveVersionedKeyAsync(key, cancellationToken);

            try {
                var json = await _distributedCache.GetStringAsync(versionedKey, cancellationToken);
                if (!string.IsNullOrWhiteSpace(json)) {
                    var value = JsonSerializer.Deserialize<T>(json);
                    if (value != null) {
                        _memoryCache.Set(versionedKey, value, TimeSpan.FromSeconds(60));
                        return value;
                    }
                }
            }
            catch (Exception ex) {
                _logger.LogWarning("Distributed cache unavailable on GetAsync for key {CacheKey}. Falling back to MemoryCache: {ErrorMessage}", MaskCacheKey(versionedKey), ex.Message);
            }

            if (_memoryCache.TryGetValue<T>(versionedKey, out var cachedValue)) {
                return cachedValue;
            }

            return null;
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class {
            var versionedKey = await ResolveVersionedKeyAsync(key, cancellationToken);

            _memoryCache.Set(versionedKey, value, ttl);

            try {
                var json = JsonSerializer.Serialize(value);
                var options = new DistributedCacheEntryOptions {
                    AbsoluteExpirationRelativeToNow = ttl
                };
                await _distributedCache.SetStringAsync(versionedKey, json, options, cancellationToken);
            }
            catch (Exception ex) {
                _logger.LogWarning("Distributed cache unavailable on SetAsync for key {CacheKey}. Cached in MemoryCache only: {ErrorMessage}", MaskCacheKey(versionedKey), ex.Message);
            }
        }

        public async Task InvalidateByAccountAsync(string accountNumber, CancellationToken cancellationToken = default) {
            var versionKey = $"account-version:{accountNumber}";
            var newVersion = Guid.NewGuid().ToString("N");

            _memoryCache.Set(versionKey, newVersion, TimeSpan.FromHours(1));

            try {
                var options = new DistributedCacheEntryOptions {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                };
                await _distributedCache.SetStringAsync(versionKey, newVersion, options, cancellationToken);
            }
            catch (Exception ex) {
                var maskedAccount = SensitiveDataMasker.MaskAccountNumber(accountNumber);
                _logger.LogWarning("Distributed cache unavailable on InvalidateByAccountAsync for account {AccountNumber}. Updated version in MemoryCache: {ErrorMessage}", maskedAccount, ex.Message);
            }
        }
    }
}
