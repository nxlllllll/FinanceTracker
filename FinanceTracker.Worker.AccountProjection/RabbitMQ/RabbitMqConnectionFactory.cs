using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FinanceTracker.Worker.AccountProjection.RabbitMQ;

public sealed class RabbitMqConnectionFactory(IOptions<RabbitMqOptions> options)
{
	public async Task<IConnection> CreateConnectionAsync(CancellationToken ct = default)
	{
		ConnectionFactory factory = new ConnectionFactory
		{
			HostName = options.Value.Host,
			Port = options.Value.Port,
			UserName = options.Value.Username,
			Password = options.Value.Password
		};

		return await factory.CreateConnectionAsync(cancellationToken: ct);
	}
}