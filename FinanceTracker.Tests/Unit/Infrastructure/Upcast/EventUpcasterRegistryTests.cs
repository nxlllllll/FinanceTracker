using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;
using FinanceTracker.Infrastructure.Database.EventStore;

namespace FinanceTracker.Tests.Unit.Infrastructure.Upcast;

public sealed class EventUpcasterRegistryTests
{
	private const string EventType = "test.event";

	public sealed record V1(Guid Id, string Name);
	public sealed record V2(Guid Id, string Name, int Count);
	public sealed record V3(Guid Id, string Name, int Count, bool Flag) : IEvent
	{
		public int Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; }
		public IEvent WithVersion(int version) => this with { Version = version };
	}

	public sealed class Step(
		int fromVersion,
		int toVersion,
		Type fromType,
		Type toType,
		Func<object, object> upcast,
		string eventType = EventType
	) : IEventUpcaster
	{
		public string EventType { get; } = eventType;
		public int FromVersion { get; } = fromVersion;
		public int ToVersion { get; } = toVersion;
		public Type FromType { get; } = fromType;
		public Type ToType { get; } = toType;

		public int Invocations { get; private set; }

		public object Upcast(object source)
		{
			Invocations++;
			return upcast(arg: source);
		}
	}

	private static Step V1ToV2() => new Step(
		fromVersion: 1,
		toVersion: 2,
		fromType: typeof(V1),
		toType: typeof(V2),
		upcast: source => new V2(Id: ((V1)source).Id, Name: ((V1)source).Name, Count: 0)
	);

	private static Step V2ToV3() => new Step(
		fromVersion: 2,
		toVersion: 3,
		fromType: typeof(V2),
		toType: typeof(V3),
		upcast: source => new V3(Id: ((V2)source).Id, Name: ((V2)source).Name, Count: ((V2)source).Count, Flag: true)
	);

	private static string PayloadV1(Guid id) => $$"""{"Id":"{{id}}","Name":"stored"}""";

	[Test]
	public async Task AnUpcasterThatDoesNotMoveForwardIsRejected()
	{
		await Assert.That(action: () => _ = new EventUpcasterRegistry(upcasters:
		[
			new Step(
				fromVersion: 2,
				toVersion: 2,
				fromType: typeof(V1),
				toType: typeof(V2),
				upcast: source => source
			)
		])).Throws<InvalidOperationException>().Because(message: "a step that stands still would loop or silently do nothing on replay");
	}

	[Test]
	public async Task TwoUpcastersLeavingTheSameVersionAreRejected()
	{
		await Assert.That(action: () => _ = new EventUpcasterRegistry(upcasters:
		[
			V1ToV2(),
			new Step(
				fromVersion: 1,
				toVersion: 3,
				fromType: typeof(V1),
				toType: typeof(V3),
				upcast: source => source
			)
		])).Throws<InvalidOperationException>().Because(message: "with two ways out of version 1 the migration a payload takes would depend on registration order");
	}

	[Test]
	public async Task AGapBetweenStepsIsRejected()
	{
		await Assert.That(action: () => _ = new EventUpcasterRegistry(upcasters:
		[
			V1ToV2(),
			new Step(
				fromVersion: 3,
				toVersion: 4,
				fromType: typeof(V3),
				toType: typeof(V3),
				upcast: source => source
			)
		])).Throws<InvalidOperationException>().Because(message: "nothing carries a payload across the missing step, so anything older than the gap can never be read");
	}

	[Test]
	public async Task AStepProducingSomethingTheNextCannotConsumeIsRejected()
	{
		await Assert.That(action: () => _ = new EventUpcasterRegistry(upcasters:
		[
			new Step(
				fromVersion: 1,
				toVersion: 2,
				fromType: typeof(V1),
				toType: typeof(V1),
				upcast: source => source
			),
			V2ToV3()
		])).Throws<InvalidOperationException>().Because(message: "the versions line up but the types do not, which would fail as a cast at replay time instead of at startup");
	}

	[Test]
	public async Task StepsRegisteredOutOfOrderStillFormAValidChain()
	{
		EventUpcasterRegistry registry = new EventUpcasterRegistry(upcasters: [V2ToV3(), V1ToV2()]);

		await Assert.That(value: registry.DescribeChain(eventType: EventType))
			.IsEqualTo(expected: new EventUpcasterChain(FromVersion: 1, ToVersion: 3))
			.Because(message: "DI hands over whatever order it discovered types in, which is not the order the chain runs in");
	}

	[Test]
	public async Task AnEventTypeWithNoUpcasterHasNoChain()
	{
		EventUpcasterRegistry registry = new EventUpcasterRegistry(upcasters: [V1ToV2()]);

		await Assert.That(value: registry.HasChain(eventType: EventType)).IsTrue();
		await Assert.That(value: registry.HasChain(eventType: "other.event")).IsFalse();
		await Assert.That(value: registry.DescribeChain(eventType: "other.event")).IsNull();
	}

	[Test]
	public async Task ChainsForDifferentEventTypesDoNotMix()
	{
		EventUpcasterRegistry registry = new EventUpcasterRegistry(upcasters: [
			V1ToV2(),
			new Step(
				fromVersion: 1,
				toVersion: 5,
				fromType: typeof(V1),
				toType: typeof(V3),
				upcast: source => source, eventType: "other.event"
			)
		]);

		await Assert.That(value: registry.DescribeChain(eventType: EventType)).IsEqualTo(expected: new EventUpcasterChain(FromVersion: 1, ToVersion: 2));
		await Assert.That(value: registry.DescribeChain(eventType: "other.event")).IsEqualTo(expected: new EventUpcasterChain(FromVersion: 1, ToVersion: 5));
	}

	[Test]
	public async Task AStoredPayloadIsCarriedAllTheWayToTheCurrentShape()
	{
		Guid id = Guid.CreateVersion7();
		EventUpcasterRegistry registry = new EventUpcasterRegistry(upcasters: [V1ToV2(), V2ToV3()]);

		IEvent result = registry.Apply(eventType: EventType, payload: PayloadV1(id: id), storedVersion: 1, currentVersion: 3);

		V3 migrated = (V3)result;

		await Assert.That(value: migrated.Id).IsEqualTo(expected: id);
		await Assert.That(value: migrated.Name).IsEqualTo(expected: "stored")
			.Because(message: "fields the old shape already carried must survive every step untouched");
		await Assert.That(value: migrated.Flag).IsTrue();
	}

	[Test]
	public async Task APayloadStoredMidChainEntersAtItsOwnVersion()
	{
		Guid id = Guid.CreateVersion7();
		Step first = V1ToV2();
		EventUpcasterRegistry registry = new EventUpcasterRegistry(upcasters: [first, V2ToV3()]);

		IEvent result = registry.Apply(
			eventType: EventType,
			payload: $$"""{"Id":"{{id}}","Name":"stored","Count":7}""",
			storedVersion: 2,
			currentVersion: 3
		);

		await Assert.That(value: ((V3)result).Count).IsEqualTo(expected: 7);
		await Assert.That(value: first.Invocations).IsEqualTo(expected: 0)
			.Because(message: "a payload already at version 2 must not be run through the step that produces version 2");
	}

	[Test]
	public async Task AChainThatOutrunsTheRunningBuildFailsWithAnExplanation()
	{
		Guid id = Guid.CreateVersion7();
		Step second = V2ToV3();
		EventUpcasterRegistry registry = new EventUpcasterRegistry(upcasters: [V1ToV2(), second]);

		await Assert.That(action: () => registry.Apply(
			eventType: EventType,
			payload: PayloadV1(id: id),
			storedVersion: 1,
			currentVersion: 2
		)).Throws<InvalidOperationException>();

		await Assert.That(value: second.Invocations).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task ApplyingToAnUnknownEventTypeFailsLoudly()
	{
		EventUpcasterRegistry registry = new EventUpcasterRegistry(upcasters: [V1ToV2()]);

		await Assert.That(action: () => registry.Apply(eventType: "other.event", payload: "{}", storedVersion: 1, currentVersion: 2))
			.Throws<InvalidOperationException>();
	}

	[Test]
	public async Task ApplyingFromAVersionNoStepAcceptsFailsLoudly()
	{
		EventUpcasterRegistry registry = new EventUpcasterRegistry(upcasters: [V1ToV2()]);

		await Assert.That(action: () => registry.Apply(eventType: EventType, payload: "{}", storedVersion: 7, currentVersion: 8))
			.Throws<InvalidOperationException>()
			.Because(message: "silently returning the payload as-is would hand a half-migrated object to an aggregate");
	}
}
