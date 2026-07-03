namespace FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;

/// <summary>
/// Non-generic contract used by <c>EventUpcasterRegistry</c> to store a heterogeneous
/// chain of upcasters. Do not implement directly — extend
/// <see cref="EventUpcaster{TFrom,TTo}"/> instead.
/// </summary>
public interface IEventUpcaster
{
	/// <summary>
	/// The <see cref="EventTypeAttribute.Name"/> of the event type this upcaster handles.
	/// Derived automatically from <c>[EventType]</c> on <c>TFrom</c>.
	/// </summary>
	string EventType { get; }

	/// <summary>Schema version this upcaster migrates <b><em>from</em></b>.</summary>
	int FromVersion { get; }

	/// <summary>Schema version this upcaster migrates <b><em>to</em></b>.</summary>
	int ToVersion { get; }

	/// <summary>
	/// The CLR type of the old event version (<c>TFrom</c>).
	/// Used by <c>EventUpcasterRegistry</c> to deserialize the stored payload
	/// into the correct type before calling <see cref="Upcast"/>.
	/// </summary>
	Type FromType { get; }

	/// <summary>
	/// The CLR type of the new event version (<c>TTo</c>).
	/// Available for introspection and diagnostics.
	/// </summary>
	Type ToType { get; }

	/// <summary>
	/// Migrates a deserialized event object from the old schema to the new one.
	/// The argument is guaranteed to be of type <c>TFrom</c> at runtime.
	/// </summary>
	object Upcast(object source);
}
