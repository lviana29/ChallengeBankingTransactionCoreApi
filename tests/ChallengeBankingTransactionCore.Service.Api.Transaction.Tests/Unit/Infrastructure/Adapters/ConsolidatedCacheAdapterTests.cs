using System.Text;
using System.Text.Json;
using ChallengeBankingTransactionCore.Service.Application.Transaction.Dto;
using ChallengeBankingTransactionCore.Service.Domain.Transaction.Interface;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Adapter;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace ChallengeBankingTransactionCore.Service.Api.Transaction.Tests.Unit.Infrastructure.Adapters {
    public class ConsolidatedCacheAdapterTests {
        private readonly Mock<IDistributedCache> _distributedCacheMock;
        private readonly IMemoryCache _memoryCache;
        private readonly Mock<IAppLogger<ConsolidatedCacheAdapter>> _loggerMock;
        private readonly ConsolidatedCacheAdapter _adapter;

        public ConsolidatedCacheAdapterTests() {
            _distributedCacheMock = new Mock<IDistributedCache>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _loggerMock = new Mock<IAppLogger<ConsolidatedCacheAdapter>>();

            _adapter = new ConsolidatedCacheAdapter(
                _distributedCacheMock.Object,
                _memoryCache,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task GetAsync_ShouldReturnDeserializedDto_WhenDistributedCacheHits() {
            // Arrange
            var expectedDto = new ConsolidatedStatementDto(
                "12345-6",
                1500m,
                1500m,
                DateTime.UtcNow,
                new List<ConsolidatedTransactionsDto>()
            );

            var versionBytes = Encoding.UTF8.GetBytes("v2");
            var dtoBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expectedDto));

            _distributedCacheMock
                .Setup(d => d.GetAsync("account-version:12345-6", It.IsAny<CancellationToken>()))
                .ReturnsAsync(versionBytes);

            _distributedCacheMock
                .Setup(d => d.GetAsync("consolidated:v1:12345-6:ver:v2:default-10d", It.IsAny<CancellationToken>()))
                .ReturnsAsync(dtoBytes);

            // Act
            var result = await _adapter.GetAsync<ConsolidatedStatementDto>("consolidated:v1:12345-6:default-10d");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("12345-6", result.accountNumber);
            Assert.Equal(1500m, result.consolidatedBalance);

            // Second call hits L1 MemoryCache directly without querying DistributedCache again
            var l1Result = await _adapter.GetAsync<ConsolidatedStatementDto>("consolidated:v1:12345-6:default-10d");
            Assert.NotNull(l1Result);
            Assert.Equal(1500m, l1Result.consolidatedBalance);
        }

        [Fact]
        public async Task GetAsync_ShouldFallbackToMemoryCache_WhenDistributedCacheThrowsOnGetAsync() {
            // Arrange
            _distributedCacheMock
                .Setup(d => d.GetAsync("account-version:55555-5", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes("v1"));

            _distributedCacheMock
                .Setup(d => d.GetAsync("consolidated:v1:55555-5:ver:v1:default-10d", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Redis read error"));

            // Act
            var result = await _adapter.GetAsync<ConsolidatedStatementDto>("consolidated:v1:55555-5:default-10d");

            // Assert
            Assert.Null(result);
            _loggerMock.Verify(l => l.LogWarning(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public async Task SetAsync_And_GetAsync_ShouldFallbackToMemoryCache_WhenRedisThrowsException() {
            // Arrange
            _distributedCacheMock
                .Setup(d => d.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Redis connection refused"));

            _distributedCacheMock
                .Setup(d => d.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Redis connection refused"));

            var expectedDto = new ConsolidatedStatementDto(
                "12345-6",
                2200m,
                2200m,
                DateTime.UtcNow,
                new List<ConsolidatedTransactionsDto>()
            );

            // Act
            await _adapter.SetAsync("consolidated:v1:12345-6:default-10d", expectedDto, TimeSpan.FromSeconds(60));
            var cached = await _adapter.GetAsync<ConsolidatedStatementDto>("consolidated:v1:12345-6:default-10d");

            // Assert
            Assert.NotNull(cached);
            Assert.Equal(2200m, cached.consolidatedBalance);
            _loggerMock.Verify(l => l.LogWarning(It.IsAny<string>(), It.IsAny<object[]>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task InvalidateByAccountAsync_ShouldRotateAccountVersion_AndCauseCacheMissForOldEntries() {
            // Arrange
            _distributedCacheMock
                .Setup(d => d.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);

            var dto = new ConsolidatedStatementDto(
                "12345-6",
                900m,
                900m,
                DateTime.UtcNow,
                new List<ConsolidatedTransactionsDto>()
            );

            await _adapter.SetAsync("consolidated:v1:12345-6:default-10d", dto, TimeSpan.FromSeconds(60));

            // Hit before invalidation
            var hit = await _adapter.GetAsync<ConsolidatedStatementDto>("consolidated:v1:12345-6:default-10d");
            Assert.NotNull(hit);

            // Act - Invalidate account
            await _adapter.InvalidateByAccountAsync("12345-6");
            var missAfterInvalidate = await _adapter.GetAsync<ConsolidatedStatementDto>("consolidated:v1:12345-6:default-10d");

            // Assert
            Assert.Null(missAfterInvalidate);
            _distributedCacheMock.Verify(d => d.SetAsync(
                "account-version:12345-6",
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvalidateByAccountAsync_ShouldStillInvalidateMemoryCache_WhenRedisThrowsException() {
            // Arrange
            _distributedCacheMock
                .Setup(d => d.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Redis timeout"));

            _distributedCacheMock
                .Setup(d => d.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Redis timeout"));

            var dto = new ConsolidatedStatementDto(
                "99999-1",
                300m,
                300m,
                DateTime.UtcNow,
                new List<ConsolidatedTransactionsDto>()
            );

            await _adapter.SetAsync("consolidated:v1:99999-1:default-10d", dto, TimeSpan.FromSeconds(60));
            Assert.NotNull(await _adapter.GetAsync<ConsolidatedStatementDto>("consolidated:v1:99999-1:default-10d"));

            // Act
            await _adapter.InvalidateByAccountAsync("99999-1");
            var afterInvalidate = await _adapter.GetAsync<ConsolidatedStatementDto>("consolidated:v1:99999-1:default-10d");

            // Assert
            Assert.Null(afterInvalidate);
        }

        [Fact]
        public async Task GetAsync_WithSimpleKeyWithoutPrefix_ShouldUseRawKeyDirectly() {
            // Arrange
            _distributedCacheMock
                .Setup(d => d.GetAsync("simple-key", It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);

            // Act
            var result = await _adapter.GetAsync<ConsolidatedStatementDto>("simple-key");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetAsync_WithThreePartKey_ShouldUseDefaultSuffixAndHandleNullDeserializedJson() {
            // Arrange
            _distributedCacheMock
                .Setup(d => d.GetAsync("account-version:12345-6", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes("v1"));

            _distributedCacheMock
                .Setup(d => d.GetAsync("consolidated:v1:12345-6:ver:v1:default", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes("null"));

            // Act
            var result = await _adapter.GetAsync<ConsolidatedStatementDto>("consolidated:v1:12345-6");

            // Assert
            Assert.Null(result);
        }
    }
}
