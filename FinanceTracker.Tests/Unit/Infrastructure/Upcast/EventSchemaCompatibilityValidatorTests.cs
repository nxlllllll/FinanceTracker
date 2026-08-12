using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Upcast;

public sealed class EventSchemaCompatibilityValidatorTests
{
	private const string EventType = "account.created.schema-test";

	private static EventSchemaCompatibilityValidator CreateValidator(
		IReadOnlyDictionary<string, int> currentVersions,
		params (string EventType, EventUpcasterChain Chain)[] chains)
	{
		IEventTypeResolver eventTypeResolver = Substitute.For<IEventTypeResolver>();
		eventTypeResolver.CurrentVersions.Returns(returnThis: currentVersions);

		IEventUpcasterRegistry upcasterRegistry = Substitute.For<IEventUpcasterRegistry>();
		upcasterRegistry.DescribeChain(eventType: Arg.Any<string>()).Returns(returnThis: (EventUpcasterChain?)null);

		foreach ((string eventType, EventUpcasterChain chain) in chains)
			upcasterRegistry.DescribeChain(eventType: eventType).Returns(returnThis: chain);

		return new EventSchemaCompatibilityValidator(
			eventTypeResolver: eventTypeResolver,
			upcasterRegistry: upcasterRegistry
		);
	}

	[Test]
	public async Task Validate_WhenEveryEventStaysAtVersionOne_ShouldPass()
	{
		EventSchemaCompatibilityValidator validator = CreateValidator(currentVersions: new Dictionary<string, int>
		{
			[EventType] = 1,
			["account.debited.schema-test"] = 1
		});

		await Assert.That(action: validator.Validate).ThrowsNothing();
	}

	[Test]
	public async Task Validate_WhenAVersionWasBumpedWithoutAnUpcaster_ShouldThrow()
	{
		EventSchemaCompatibilityValidator validator = CreateValidator(currentVersions: new Dictionary<string, int>
		{
			[EventType] = 2
		});

		IncompatibleEventVersionException? exception = await Assert.That(action: validator.Validate).Throws<IncompatibleEventVersionException>();

		await Assert.That(value: exception?.EventType).IsEqualTo(expected: EventType);
		await Assert.That(value: exception?.CurrentVersion).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task Validate_WhenTheChainDoesNotStartAtVersionOne_ShouldThrow()
	{
		EventSchemaCompatibilityValidator validator = CreateValidator(
			currentVersions: new Dictionary<string, int> { [EventType] = 3 },
			chains: (EventType, new EventUpcasterChain(FromVersion: 2, ToVersion: 3))
		);

		IncompatibleEventVersionException? exception = await Assert.That(action: validator.Validate).Throws<IncompatibleEventVersionException>();

		await Assert.That(value: exception?.EventType).IsEqualTo(expected: EventType);
	}

	[Test]
	public async Task Validate_WhenTheChainStopsShortOfTheDeclaredVersion_ShouldThrow()
	{
		EventSchemaCompatibilityValidator validator = CreateValidator(
			currentVersions: new Dictionary<string, int> { [EventType] = 3 },
			chains: (EventType, new EventUpcasterChain(FromVersion: 1, ToVersion: 2))
		);

		IncompatibleEventVersionException? exception = await Assert.That(action: validator.Validate).Throws<IncompatibleEventVersionException>();

		await Assert.That(value: exception?.StoredVersion).IsEqualTo(expected: 2);
		await Assert.That(value: exception?.CurrentVersion).IsEqualTo(expected: 3);
	}

	[Test]
	public async Task Validate_WhenTheChainSpansTheFullRange_ShouldPass()
	{
		EventSchemaCompatibilityValidator validator = CreateValidator(
			currentVersions: new Dictionary<string, int> { [EventType] = 3 },
			chains: (EventType, new EventUpcasterChain(FromVersion: 1, ToVersion: 3))
		);

		await Assert.That(action: validator.Validate).ThrowsNothing();
	}

	[Test]
	public async Task Validate_WhenOneOfManyEventsIsBroken_ShouldNameThatEvent()
	{
		EventSchemaCompatibilityValidator validator = CreateValidator(
			currentVersions: new Dictionary<string, int>
			{
				["account.debited.schema-test"] = 1,
				["account.credited.schema-test"] = 2,
				[EventType] = 2
			},
			chains: ("account.credited.schema-test", new EventUpcasterChain(FromVersion: 1, ToVersion: 2))
		);

		IncompatibleEventVersionException? exception = await Assert.That(action: validator.Validate).Throws<IncompatibleEventVersionException>();

		await Assert.That(value: exception?.EventType).IsEqualTo(expected: EventType);
	}
}
