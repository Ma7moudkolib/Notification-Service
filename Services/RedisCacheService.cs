using Microsoft.Extensions.Options;
using NotificationService.Configuration;
using StackExchange.Redis;

namespace NotificationService.Services;

public sealed class RedisCacheService(
    IConnectionMultiplexer connectionMultiplexer,
    IOptions<RedisOptions> redisOptions)
    : IRedisCacheService
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();
    private readonly RedisOptions _redisOptions = redisOptions.Value;

    public async Task<bool> TryMarkAsProcessedAsync(string messageId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return false;
        }

        var key = BuildKey(messageId);

        return await _database.StringSetAsync(
            key,
            "1",
            _redisOptions.Expiry,
            when: When.NotExists).ConfigureAwait(false);
    }

    public async Task RemoveProcessedMarkerAsync(string messageId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return;
        }

        await _database.KeyDeleteAsync(BuildKey(messageId)).ConfigureAwait(false);
    }

    private string BuildKey(string messageId) => $"{_redisOptions.KeyPrefix}{messageId}";
}
