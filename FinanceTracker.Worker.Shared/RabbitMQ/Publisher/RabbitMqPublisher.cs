using System.Text;
using System.Text.Json;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Publish;

public sealed class RabbitMqPublisher(
	RabbitMqConnectionFactory connectionFactory,
	IOptions<RabbitMqOptions> options
) : IRabbitMqPublisher
{
	private readonly RabbitMqOptions _options = options.Value;

	private IConnection? _connection;
	private IChannel? _channel;

	public async Task PublishAsync<TMessage>(
		TMessage message,
		CancellationToken ct = default) where TMessage : class
	{
		IChannel channel = await GetOrCreateChannelAsync(ct: ct);

		byte[] body = Encoding.UTF8.GetBytes(s: JsonSerializer.Serialize(value: message, options: FinanceTrackerJsonOptions.Payload));

		await channel.BasicPublishAsync(
			exchange: _options.ExchangeName,
			routingKey: String.Empty,
			body: body,
			cancellationToken: ct
		);
	}

	private async Task<IChannel> GetOrCreateChannelAsync(CancellationToken ct)
	{
		if (_channel is not null)
			return _channel;

		_connection = await connectionFactory.CreateConnectionAsync(ct: ct);
		_channel = await _connection.CreateChannelAsync(cancellationToken: ct);

		await _channel.ExchangeDeclareAsync(
			exchange: _options.ExchangeName,
			type: ExchangeType.Fanout,
			durable: true,
			cancellationToken: ct
		);

		return _channel;
	}

	public async ValueTask DisposeAsync()
	{
		if (_channel is not null)
			await _channel.DisposeAsync();

		if (_connection is not null)
			await _connection.DisposeAsync();
	}
}