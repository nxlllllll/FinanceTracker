using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;

/// <summary>
/// Typed DSL for writing event upcasters. Derive from this class,
/// annotate with <see cref="UpcasterVersionAttribute"/>, and implement
/// <see cref="Upcast"/> with a typed mapping between event versions.
/// <code>
/// [UpcasterVersion(from: 1, to: 2)]
/// public sealed class AccountCreatedV1ToV2 : EventUpcaster&lt;AccountCreatedV1, AccountCreated&gt;
/// {
///     public override AccountCreated Upcast(AccountCreatedV1 source) =>
///         new AccountCreated(source.Id, source.AccountId, AccountType.Checking, ...);
/// }
/// </code>
/// </summary>
public abstract class EventUpcaster<TFrom, TTo> : IEventUpcaster
	where TFrom : class
	where TTo : class
{
	public string EventType { get; }
	public int FromVersion { get; }
	public int ToVersion { get; }
	public Type FromType { get; } = typeof(TFrom);
	public Type ToType { get; } = typeof(TTo);

	protected EventUpcaster()
	{
		EventTypeAttribute eventTypeAttr = (EventTypeAttribute?)Attribute.GetCustomAttribute(
			element: typeof(TFrom),
			attributeType: typeof(EventTypeAttribute)
		) ?? throw new InvalidOperationException(message: $"[Upcasting] {typeof(TFrom).Name} is missing [EventType] attribute.");

		UpcasterVersionAttribute versionAttr = (UpcasterVersionAttribute?)Attribute.GetCustomAttribute(
			element: GetType(),
			attributeType: typeof(UpcasterVersionAttribute)
		) ?? throw new InvalidOperationException(message: $"[Upcasting] {GetType().Name} is missing [UpcasterVersion] attribute.");

		EventType = eventTypeAttr.Name;
		FromVersion = versionAttr.From;
		ToVersion = versionAttr.To;
	}

	public abstract TTo Upcast(TFrom source);

	object IEventUpcaster.Upcast(object source)
	{
		if (source is not TFrom sourceAsTFrom)
			throw new InvalidCastException(message: $"[Upcasting] '{nameof(source)}' is not {typeof(TFrom).Name}.");
		
		return Upcast(source: sourceAsTFrom);
	}
}