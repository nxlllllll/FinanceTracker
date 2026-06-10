using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;

namespace FinanceTracker.Tests.Unit.Infrastructure.Upcast;

[EventType(name: "account.created.test")]
[EventVersion(version: 1)]
public sealed record AccountCreatedV1(
	Guid Id,
	Guid AccountId,
	Guid UserId,
	string Name,
	string Currency,
	int Version,
	DateTimeOffset OccurredAt
) : IEvent
{
	public IEvent WithVersion(int version) => this with { Version = version };
}