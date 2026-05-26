using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared.Fixtures;

public abstract class RabbitMqFixture
{
	private static RabbitMqContainer _container = null!;

	protected string ConnectionString => _container.GetConnectionString();

	protected async Task<(IConnection Connection, IChannel Channel)> CreateChannelAsync(CancellationToken ct = default)
	{
		Uri uri = new Uri(uriString: ConnectionString);

		ConnectionFactory factory = new ConnectionFactory
		{
			HostName = uri.Host,
			Port = uri.Port,
			UserName = "guest",
			Password = "guest"
		};

		IConnection connection = await factory.CreateConnectionAsync(cancellationToken: ct);
		IChannel channel = await connection.CreateChannelAsync(cancellationToken: ct);
		return (connection, channel);
	}

	[Before(hookType: Assembly)]
	public static async Task StartContainerAsync()
	{
		_container = new RabbitMqBuilder(image: "rabbitmq:4.3.0")
			.WithUsername(username: "guest")
			.WithPassword(password: "guest")
			.Build();

		await _container.StartAsync();
	}

	[After(hookType: Assembly)]
	public static async Task StopContainerAsync()
		=> await _container.DisposeAsync();
}
