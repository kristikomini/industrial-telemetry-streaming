using System.Collections.Concurrent;

namespace MotorValley.Backend.Services;

/// <summary>
/// Process-local <see cref="ICacheService"/> that stands in for Redis when the stack is
/// run without any external cache (the no-Docker path). It preserves the cache-aside
/// contract — TTL expiry and value semantics — so the read path behaves identically; it
/// just does not survive a restart or span multiple backend replicas, which Redis does.
/// Selected in <c>Program.cs</c> when <c>Cache:Provider</c> is <c>Memory</c> or no Redis
/// connection string is configured.
/// </summary>
public class InMemoryCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, (string Json, DateTimeOffset ExpiresAt)> _store = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        if (_store.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
                return Task.FromResult(System.Text.Json.JsonSerializer.Deserialize<T>(entry.Json));

            // Expired — evict lazily on read.
            _store.TryRemove(key, out _);
        }

        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry ?? TimeSpan.FromMinutes(5));
        _store[key] = (json, expiresAt);
        return Task.CompletedTask;
    }
}
