using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Retry;

public sealed class RedisRetryCounter(
	IConnectionMultiplexer connectionMultiplexer,
	IOptions<RabbitMqOptions> options
) : IRetryCounter
{
	private readonly IDatabase _database = connectionMultiplexer.GetDatabase();
	private readonly TimeSpan _ttl = TimeSpan.FromHours(value: options.Value.RetryCounterTtlHours);
	private readonly string _queueName = options.Value.QueueName ?? "unknown";

	public async Task<int> IncrementAsync(string messageKey, CancellationToken ct = default)
	{
		string key = BuildKey(messageKey: messageKey);

		long count = await _database.StringIncrementAsync(key: key);

		if (count == 1)
			await _database.KeyExpireAsync(key: key, expiry: _ttl);

		return (int)count - 1;
	}

	public async Task RemoveAsync(string messageKey, CancellationToken ct = default)
	{
		string key = BuildKey(messageKey: messageKey);
		await _database.KeyDeleteAsync(key: key);
	}

	private string BuildKey(string messageKey)
		=> $"consumer:retry:{_queueName}:{messageKey}";
}