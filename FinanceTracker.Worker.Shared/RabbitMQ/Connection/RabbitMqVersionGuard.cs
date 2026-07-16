using System.Text;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using RabbitMQ.Client;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Connection;

/// <summary>
/// Verifies the connected RabbitMQ broker meets the minimum version this application requires.
/// </summary>
public static class RabbitMqVersionGuard
{
	private static readonly Version MinimumRequiredVersion = new Version(major: 4, minor: 3, build: 0);

	/// <summary>
	/// Throws <see cref="UnsupportedRabbitMqVersionException"/> if the broker's reported version
	/// is older than <see cref="MinimumRequiredVersion"/>, or if the version cannot be determined
	/// at all (a missing/unparseable "version" server property is treated as unsupported rather
	/// than silently allowed through).
	/// </summary>
	public static void EnsureSupportedVersion(IConnection connection)
	{
		string rawVersion = ReadRawVersion(connection: connection);
		Version parsedVersion = ParseLeadingVersion(rawVersion: rawVersion);

		if (parsedVersion < MinimumRequiredVersion)
		{
			throw new UnsupportedRabbitMqVersionException(
				message: $"""
					Connected RabbitMQ broker version {rawVersion} is older than the minimum required version {MinimumRequiredVersion}.
					Native x-delayed-retry-* queue arguments require RabbitMQ 4.3.0 or later; on older brokers they are silently ignored, disabling retry backoff.
				""",
				connectedVersion: rawVersion,
				minimumRequiredVersion: MinimumRequiredVersion.ToString()
			);
		}
	}

	private static string ReadRawVersion(IConnection connection)
	{
		if (connection.ServerProperties?.TryGetValue(key: "version", value: out object? versionValue) == true &&
			versionValue is byte[] versionBytes)
		{
			return Encoding.UTF8.GetString(bytes: versionBytes);
		}

		throw new UnsupportedRabbitMqVersionException(
			message: "The connected RabbitMQ broker did not report a version in its server properties.",
			connectedVersion: "unknown",
			minimumRequiredVersion: MinimumRequiredVersion.ToString()
		);
	}

	private static Version ParseLeadingVersion(string rawVersion)
	{
		int endIndex = 0;
		while (endIndex < rawVersion.Length && (Char.IsAsciiDigit(c: rawVersion[endIndex]) || rawVersion[endIndex] == '.'))
			++endIndex;

		string numericPart = rawVersion[..endIndex].Trim(trimChar: '.');

		if (!Version.TryParse(input: numericPart, result: out Version? parsed))
		{
			throw new UnsupportedRabbitMqVersionException(
				message: $"Could not parse RabbitMQ broker version string '{rawVersion}'.",
				connectedVersion: rawVersion,
				minimumRequiredVersion: MinimumRequiredVersion.ToString()
			);
		}

		return parsed;
	}
}
