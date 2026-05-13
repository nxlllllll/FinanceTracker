using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Connection;

public sealed class RabbitMqConnectionFactory(IOptions<RabbitMqOptions> options)
{
	private readonly RabbitMqOptions _options = options.Value;
	
	public async Task<IConnection> CreateConnectionAsync(CancellationToken ct = default)
	{
		ConnectionFactory factory = new ConnectionFactory
		{
			HostName = _options.Host,
			Port = _options.Port,
			UserName = _options.Username,
			Password = _options.Password
		};

		return await factory.CreateConnectionAsync(cancellationToken: ct);
	}
}