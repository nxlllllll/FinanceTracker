using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using FinanceTracker.Worker.Shared.RabbitMQ.Publisher;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Connection;

/// <summary>
/// Creates RabbitMQ <see cref="IConnection"/> instances from <see cref="RabbitMqOptions"/>.
/// Used by both <see cref="RabbitMqPublisher"/> and <see cref="RabbitMqListenerService{TMessage,THandler}"/>
/// to obtain fresh connections on startup or reconnect after a failure.
/// </summary>
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
			Password = _options.Password,
			AutomaticRecoveryEnabled = false,
			TopologyRecoveryEnabled = false
		};

		return await factory.CreateConnectionAsync(cancellationToken: ct);
	}
}