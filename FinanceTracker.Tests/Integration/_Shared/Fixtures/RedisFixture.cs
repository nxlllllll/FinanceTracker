using StackExchange.Redis;
using Testcontainers.Redis;

namespace FinanceTracker.Tests.Integration._Shared.Fixtures;

public abstract class RedisFixture
{
	private static RedisContainer _container = null!;
	protected IConnectionMultiplexer Redis = null!;

	[Before(hookType: Assembly)]
	public static async Task StartContainerAsync()
	{
		_container = new RedisBuilder(image: "redis:7").Build();
		await _container.StartAsync();
	}

	[Before(hookType: Test)]
	public async Task ConnectAsync()
	{
		Redis = await ConnectionMultiplexer.ConnectAsync(configuration: _container.GetConnectionString() + ",allowAdmin=true");
	}

	[After(hookType: Test)]
	public async Task FlushAsync()
	{
		IServer server = Redis.GetServer(endpoint: Redis.GetEndPoints().First());
		await server.FlushAllDatabasesAsync();
		await Redis.DisposeAsync();
	}

	[After(hookType: Assembly)]
	public static async Task StopContainerAsync()
		=> await _container.DisposeAsync();
}
