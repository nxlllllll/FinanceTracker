namespace FinanceTracker.Core.Exceptions.ConfigurationExceptions;

/// <summary>
/// Thrown when the connected RabbitMQ broker reports a version older than the minimum required
/// </summary>
public sealed class UnsupportedRabbitMqVersionException(
	string message,
	string connectedVersion,
	string minimumRequiredVersion
) : ConfigurationException(message: message)
{
	public string ConnectedVersion { get; init; } = connectedVersion;
	public string MinimumRequiredVersion { get; init; } = minimumRequiredVersion;
}
