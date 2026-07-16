using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core.Domains.Abstractions;

public sealed class AggregateRootEventsCachingTests
{
	private static DateTimeOffset Now => FakeDateProvider.Default.UtcNow;

	[Test]
	public async Task Events_OnRepeatedAccess_ShouldReturnTheSameInstance()
	{
		Account account = AccountFactory.CreateWithArchivation();

		IReadOnlyList<IEvent> first = account.Events;
		IReadOnlyList<IEvent> second = account.Events;

		await Assert.That(value: ReferenceEquals(objA: first, objB: second)).IsTrue()
			.Because(message: "A fresh wrapper on every access is exactly the allocation this caching removes.");
	}

	[Test]
	public async Task Events_AfterRaisingANewEvent_ShouldReflectItThroughTheSameCachedInstance()
	{
		Account account = AccountFactory.CreateWithArchivation(balance: 1000m);
		IReadOnlyList<IEvent> cached = account.Events;
		int countBefore = cached.Count;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = account.Debit(
			occurredAt: Now,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 100m,
			exchangeRate: 1m,
			description: null
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: ReferenceEquals(objA: cached, objB: account.Events)).IsTrue();
		await Assert.That(value: account.Events.Count).IsEqualTo(expected: countBefore + 1);
	}

	[Test]
	public async Task Events_AfterClearEvents_ShouldBeEmpty_ThroughTheSameCachedInstance()
	{
		Account account = AccountFactory.CreateWithArchivation(balance: 1000m);

		account.Debit(
			occurredAt: Now,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 50m,
			exchangeRate: 1m,
			description: null
		);

		IReadOnlyList<IEvent> cached = account.Events;
		await Assert.That(value: cached.Count).IsGreaterThan(minimum: 0);

		account.ClearEvents();

		await Assert.That(value: ReferenceEquals(objA: cached, objB: account.Events)).IsTrue();
		await Assert.That(value: account.Events.Count).IsEqualTo(expected: 0);
	}
}
