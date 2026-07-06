using System.Text;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using NSubstitute;
using RabbitMQ.Client;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class RabbitMqVersionGuardTests
{
	private static IConnection CreateConnectionWithVersion(string? version)
	{
		IConnection connection = Substitute.For<IConnection>();

		IDictionary<string, object?> serverProperties = version is null
			? new Dictionary<string, object?>()
			: new Dictionary<string, object?> { ["version"] = Encoding.UTF8.GetBytes(s: version) };

		connection.ServerProperties.Returns(returnThis: serverProperties);
		return connection;
	}

	private static Task InvokeGuard(IConnection connection)
	{
		RabbitMqVersionGuard.EnsureSupportedVersion(connection: connection);
		return Task.CompletedTask;
	}

	[Test]
	public async Task EnsureSupportedVersion_WithVersionEqualToMinimum_ShouldNotThrow()
	{
		IConnection connection = CreateConnectionWithVersion(version: "4.3.0");

		await Assert.That(action: async () => await InvokeGuard(connection: connection)).ThrowsNothing();
	}

	[Test]
	public async Task EnsureSupportedVersion_WithNewerPatchVersion_ShouldNotThrow()
	{
		IConnection connection = CreateConnectionWithVersion(version: "4.3.7");

		await Assert.That(action: async () => await InvokeGuard(connection: connection)).ThrowsNothing();
	}

	[Test]
	public async Task EnsureSupportedVersion_WithNewerMajorVersion_ShouldNotThrow()
	{
		IConnection connection = CreateConnectionWithVersion(version: "5.0.0");

		await Assert.That(action: async () => await InvokeGuard(connection: connection)).ThrowsNothing();
	}

	[Test]
	public async Task EnsureSupportedVersion_WithBetaSuffixAboveMinimum_ShouldParseLeadingNumericPartAndNotThrow()
	{
		IConnection connection = CreateConnectionWithVersion(version: "4.3.0-beta.1");

		await Assert.That(action: async () => await InvokeGuard(connection: connection)).ThrowsNothing();
	}

	[Test]
	public async Task EnsureSupportedVersion_WithBuildMetadataSuffix_ShouldParseLeadingNumericPartAndNotThrow()
	{
		IConnection connection = CreateConnectionWithVersion(version: "4.3.0+build.123");

		await Assert.That(action: async () => await InvokeGuard(connection: connection)).ThrowsNothing();
	}

	[Test]
	public async Task EnsureSupportedVersion_WithVersionBelowMinimumMinor_ShouldThrow()
	{
		IConnection connection = CreateConnectionWithVersion(version: "4.2.9");

		await Assert.ThrowsAsync<UnsupportedRabbitMqVersionException>(action: async () => await InvokeGuard(connection: connection));
	}

	[Test]
	public async Task EnsureSupportedVersion_WithOlderMajorVersion_ShouldThrow()
	{
		IConnection connection = CreateConnectionWithVersion(version: "3.13.7");

		await Assert.ThrowsAsync<UnsupportedRabbitMqVersionException>(action: async () => await InvokeGuard(connection: connection));
	}

	[Test]
	public async Task EnsureSupportedVersion_WithMissingVersionProperty_ShouldThrow()
	{
		IConnection connection = CreateConnectionWithVersion(version: null);

		await Assert.ThrowsAsync<UnsupportedRabbitMqVersionException>(action: async () => await InvokeGuard(connection: connection));
	}

	[Test]
	public async Task EnsureSupportedVersion_WithUnparseableVersionString_ShouldThrow()
	{
		IConnection connection = CreateConnectionWithVersion(version: "not-a-version");

		await Assert.ThrowsAsync<UnsupportedRabbitMqVersionException>(action: async () => await InvokeGuard(connection: connection));
	}

	[Test]
	public async Task EnsureSupportedVersion_WhenThrowing_ShouldIncludeConnectedAndMinimumVersionInException()
	{
		IConnection connection = CreateConnectionWithVersion(version: "3.13.7");

		UnsupportedRabbitMqVersionException? exception = await Assert.ThrowsAsync<UnsupportedRabbitMqVersionException>(
			action: async () => await InvokeGuard(connection: connection)
		);

		await Assert.That(value: exception).IsNotNull();
		await Assert.That(value: exception!.ConnectedVersion).IsEqualTo(expected: "3.13.7");
		await Assert.That(value: exception.MinimumRequiredVersion).IsEqualTo(expected: "4.3.0");
	}
}
