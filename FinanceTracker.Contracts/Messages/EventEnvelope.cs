using FinanceTracker.Contracts.Messages.Account;

namespace FinanceTracker.Contracts.Messages;

/// <summary>
/// Wraps a single serialized integration event inside an <see cref="AccountEventsMessage"/>.
/// </summary>
/// <param name="EventType">Integration event type discriminator (e.g. <c>"account.created"</c>).</param>
/// <param name="EventPayload">JSON-serialized integration event payload.</param>
public sealed record EventEnvelope(
	string EventType,
	string EventPayload
);
