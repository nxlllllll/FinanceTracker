using System.Text.Json;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;
using FinanceTracker.Infrastructure.Database.EventStore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Upcaster;

public sealed class EventUpcasterRegistryTests
{
	private static EventUpcasterRegistry CreateRegistry(params IEventUpcaster[] upcasters)
	{
		return new EventUpcasterRegistry(
			upcasters: upcasters,
			logger: Substitute.For<ILogger<EventUpcasterRegistry>>()
		);
	}
	
	[Test]
	public async Task Apply_WhenNoUpcasters_ShouldReturnOriginalDocument()
	{
		EventUpcasterRegistry registry = CreateRegistry();
		using JsonDocument original = JsonDocument.Parse(json: """{"Id":"test"}""");

		using JsonDocument result = registry.Apply(
			eventType: "account.created",
			source: original,
			storedVersion: 1,
			currentVersion: 1
		);

		await Assert.That(value: result.RootElement.GetProperty(propertyName: "Id").GetString()).IsEqualTo(expected: "test");
	}

	[Test]
	public async Task Apply_WhenStoredVersionEqualsCurrentVersion_ShouldNotApplyUpcaster()
	{
		bool upcasterCalled = false;
		IEventUpcaster upcaster = Substitute.For<IEventUpcaster>();
		upcaster.EventType.Returns(returnThis: "account.created");
		upcaster.FromVersion.Returns(returnThis: 1);
		upcaster.ToVersion.Returns(returnThis: 2);
		upcaster.Upcast(source: Arg.Any<JsonDocument>()).Returns(returnThis: _ =>
		{
			upcasterCalled = true;
			return JsonDocument.Parse(json: "{}");
		});

		EventUpcasterRegistry registry = CreateRegistry(upcaster);
		using JsonDocument original = JsonDocument.Parse(json: "{}");

		registry.Apply(
			eventType: "account.created",
			source: original,
			storedVersion: 2,
			currentVersion: 2
		);

		await Assert.That(value: upcasterCalled).IsFalse();
	}

	[Test]
	public async Task Apply_WhenOneUpcasterNeeded_ShouldApplyIt()
	{
		IEventUpcaster upcaster = Substitute.For<IEventUpcaster>();
		upcaster.EventType.Returns(returnThis: "account.created");
		upcaster.FromVersion.Returns(returnThis: 1);
		upcaster.ToVersion.Returns(returnThis: 2);
		upcaster.Upcast(source: Arg.Any<JsonDocument>()).Returns(returnThis: _ => JsonDocument.Parse(json: """{"NewField":"added"}"""));

		EventUpcasterRegistry registry = CreateRegistry(upcaster);
		using JsonDocument original = JsonDocument.Parse(json: """{"OldField":"value"}""");

		using JsonDocument result = registry.Apply(
			eventType: "account.created",
			source: original,
			storedVersion: 1,
			currentVersion: 2
		);

		await Assert.That(value: result.RootElement.TryGetProperty(propertyName: "NewField", out _)).IsTrue();
	}
	
	[Test]
	public async Task Apply_WhenChainedUpcastersNeeded_ShouldApplyAllInOrder()
	{
		List<int> callOrder = [];

		IEventUpcaster v1ToV2 = Substitute.For<IEventUpcaster>();
		v1ToV2.EventType.Returns(returnThis: "account.created");
		v1ToV2.FromVersion.Returns(returnThis: 1);
		v1ToV2.ToVersion.Returns(returnThis: 2);
		v1ToV2.Upcast(source: Arg.Any<JsonDocument>()).Returns(returnThis: _ =>
		{
			callOrder.Add(item: 1);
			return JsonDocument.Parse(json: """{"Step":"v2"}""");
		});

		IEventUpcaster v2ToV3 = Substitute.For<IEventUpcaster>();
		v2ToV3.EventType.Returns(returnThis: "account.created");
		v2ToV3.FromVersion.Returns(returnThis: 2);
		v2ToV3.ToVersion.Returns(returnThis: 3);
		v2ToV3.Upcast(source: Arg.Any<JsonDocument>()).Returns(returnThis: _ =>
		{
			callOrder.Add(item: 2);
			return JsonDocument.Parse(json: """{"Step":"v3"}""");
		});

		EventUpcasterRegistry registry = CreateRegistry(v2ToV3, v1ToV2);
		using JsonDocument original = JsonDocument.Parse(json: """{"Step":"v1"}""");

		using JsonDocument result = registry.Apply(
			eventType: "account.created",
			source: original,
			storedVersion: 1,
			currentVersion: 3
		);

		await Assert.That(value: callOrder).IsEquivalentTo(expected: [1, 2]);
		await Assert.That(value: result.RootElement.GetProperty(propertyName: "Step").GetString()).IsEqualTo(expected: "v3");
	}
	
	[Test]
	public async Task Apply_WhenPartialUpcastNeeded_ShouldSkipAlreadyAppliedUpcasters()
	{
		bool v1ToV2Called = false;
		bool v2ToV3Called = false;

		IEventUpcaster v1ToV2 = Substitute.For<IEventUpcaster>();
		v1ToV2.EventType.Returns(returnThis: "account.created");
		v1ToV2.FromVersion.Returns(returnThis: 1);
		v1ToV2.ToVersion.Returns(returnThis: 2);
		v1ToV2.Upcast(source: Arg.Any<JsonDocument>()).Returns(returnThis: _ =>
		{
			v1ToV2Called = true;
			return JsonDocument.Parse(json: "{}");
		});

		IEventUpcaster v2ToV3 = Substitute.For<IEventUpcaster>();
		v2ToV3.EventType.Returns(returnThis: "account.created");
		v2ToV3.FromVersion.Returns(returnThis: 2);
		v2ToV3.ToVersion.Returns(returnThis: 3);
		v2ToV3.Upcast(source: Arg.Any<JsonDocument>()).Returns(returnThis: _ =>
		{
			v2ToV3Called = true;
			return JsonDocument.Parse(json: """{"Step":"v3"}""");
		});

		EventUpcasterRegistry registry = CreateRegistry(v1ToV2, v2ToV3);
		using JsonDocument original = JsonDocument.Parse(json: "{}");

		registry.Apply(
			eventType: "account.created",
			source: original,
			storedVersion: 2,
			currentVersion: 3
		);

		await Assert.That(value: v1ToV2Called).IsFalse();
		await Assert.That(value: v2ToV3Called).IsTrue();
	}
}
