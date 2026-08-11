using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;

namespace FinanceTracker.Infrastructure.Database.EventStore;

/// <summary>
/// Checks that every event type declaring a schema version above 1 has an upcaster chain able to
/// carry a version-1 payload all the way to that version.
/// </summary>
public sealed class EventSchemaCompatibilityValidator(
	IEventTypeResolver eventTypeResolver,
	IEventUpcasterRegistry upcasterRegistry)
{
	private const int InitialVersion = 1;

	/// <summary>
	/// Throws <see cref="IncompatibleEventVersionException"/> on the first event type whose declared
	/// version cannot be reached from version 1 by the registered upcasters.
	/// </summary>
	public void Validate()
	{
		foreach ((string eventType, int currentVersion) in eventTypeResolver.CurrentVersions)
		{
			if (currentVersion <= InitialVersion)
				continue;

			EventUpcasterChain? chain = upcasterRegistry.DescribeChain(eventType: eventType);

			if (chain is not { } coverage)
			{
				throw new IncompatibleEventVersionException(
					message: $"""
						[Upcasting] '{eventType}' declares [EventVersion(version: {currentVersion})] but no upcaster is registered for it.
						Events already stored at an earlier version would be deserialized straight into the current shape:
						new fields would silently take their default, renamed fields would come back null, and the aggregate
						would rebuild into a state it was never in. Write the upcaster chain from version 1 to {currentVersion}.
					""",
					eventType: eventType,
					storedVersion: InitialVersion,
					currentVersion: currentVersion
				);
			}

			if (coverage.FromVersion > InitialVersion)
			{
				throw new IncompatibleEventVersionException(
					message: $"""
						[Upcasting] '{eventType}' declares [EventVersion(version: {currentVersion})] but its upcaster chain only
						starts at version {coverage.FromVersion}. A payload stored at version {InitialVersion} has no way to reach the
						current shape. Add the missing step, or confirm no version-{InitialVersion} rows remain and drop the claim.
					""",
					eventType: eventType,
					storedVersion: InitialVersion,
					currentVersion: currentVersion
				);
			}

			if (coverage.ToVersion != currentVersion)
			{
				throw new IncompatibleEventVersionException(
					message: $"""
						[Upcasting] '{eventType}' declares [EventVersion(version: {currentVersion})] but its upcaster chain ends at
						version {coverage.ToVersion}. Whatever the chain produces would not match the type the store deserializes into.
						Either add the step from {coverage.ToVersion} to {currentVersion}, or correct [EventVersion].
					""",
					eventType: eventType,
					storedVersion: coverage.ToVersion,
					currentVersion: currentVersion
				);
			}
		}
	}
}
