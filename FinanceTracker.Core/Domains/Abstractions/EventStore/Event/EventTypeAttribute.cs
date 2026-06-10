namespace FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

/// <summary>
/// Marks an <see cref="IEvent"/> implementation with a stable, human-readable type name
/// used as the discriminator when persisting and deserializing events.
/// <para>
/// The name must be unique across the entire application and must never change once
/// events have been written to the store — changing it will break event replay.
/// </para>
/// </summary>
/// <example>
/// <code>
/// [EventType(name: "account.created")]
/// public sealed record AccountCreated(...) : IEvent { ... }
/// </code>
/// </example>
[AttributeUsage(validOn: AttributeTargets.Class, Inherited = false)]
public sealed class EventTypeAttribute(string name) : Attribute
{
	/// <summary>Stable type discriminator stored alongside the event payload.</summary>
	public string Name { get; } = name;
}