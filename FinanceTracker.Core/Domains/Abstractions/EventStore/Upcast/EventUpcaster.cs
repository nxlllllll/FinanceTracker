using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;

/// <summary>
/// Typed DSL base class for writing event schema migrations.
/// Derive from this class to map an old event version (<typeparamref name="TFrom"/>)
/// to the current version (<typeparamref name="TTo"/>).
/// </summary>
/// <remarks>
/// <para>
/// Annotate the subclass with <see cref="UpcasterVersionAttribute"/> to declare
/// the version range. <c>EventType</c> is derived automatically from
/// <c>[EventType]</c> on <typeparamref name="TFrom"/> — no manual wiring needed.
/// </para>
/// <para>
/// Register upcasters in <c>Infrastructure</c>; DI will pick them up automatically
/// via <c>IEventUpcaster</c> and wire them into <c>EventUpcasterRegistry</c>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // 1. Freeze the old shape. The attribute is what keys the upcaster chain.
/// [EventType(name: "account.created")]
/// public sealed record AccountCreatedV1(Guid Id, Guid AccountId, string Currency, ...);
///
/// // 2. Add the new field to the current event, which keeps the same [EventType] name and
/// //    stays the only IEvent carrying it. Bump its version:
/// [EventType(name: "account.created")]
/// [EventVersion(version: 2)]
/// public sealed record AccountCreated(Guid Id, Guid AccountId, string Currency, AccountType Type, ...) : IEvent { ... }
///
/// // 3. Write the upcaster:
/// [UpcasterVersion(from: 1, to: 2)]
/// public sealed class AccountCreatedV1ToV2 : EventUpcaster&lt;AccountCreatedV1, AccountCreated&gt;
/// {
///     public override AccountCreated Upcast(AccountCreatedV1 source) =&gt;
///         new AccountCreated(source.Id, source.AccountId, source.Currency, AccountType.Checking, ...);
/// }
/// </code>
/// </example>
/// <typeparam name="TFrom">
/// Old event shape, annotated with <see cref="EventTypeAttribute"/>. A plain record — it is
/// deserialized as an object and never resolved as an event, so it must not implement
/// <see cref="Event.IEvent"/>.
/// </typeparam>
/// <typeparam name="TTo">Current event record returned after migration.</typeparam>
public abstract class EventUpcaster<TFrom, TTo> : IEventUpcaster
	where TFrom : class
	where TTo : class
{
	/// <inheritdoc/>
	public string EventType { get; }

	/// <inheritdoc/>
	public int FromVersion { get; }

	/// <inheritdoc/>
	public int ToVersion { get; }

	/// <inheritdoc/>
	public Type FromType { get; } = typeof(TFrom);

	/// <inheritdoc/>
	public Type ToType { get; } = typeof(TTo);

	protected EventUpcaster()
	{
		EventTypeAttribute eventTypeAttr = (EventTypeAttribute?)Attribute.GetCustomAttribute(element: typeof(TFrom), attributeType: typeof(EventTypeAttribute))
			?? throw new InvalidOperationException(message: $"[Upcasting] {typeof(TFrom).Name} is missing [EventType] attribute.");

		UpcasterVersionAttribute versionAttr = (UpcasterVersionAttribute?)Attribute.GetCustomAttribute(element: GetType(), attributeType: typeof(UpcasterVersionAttribute))
			?? throw new InvalidOperationException(message: $"[Upcasting] {GetType().Name} is missing [UpcasterVersion] attribute.");

		EventType = eventTypeAttr.Name;
		FromVersion = versionAttr.From;
		ToVersion = versionAttr.To;
	}

	/// <summary>
	/// Maps a deserialized <typeparamref name="TFrom"/> instance to <typeparamref name="TTo"/>.
	/// Implement field-by-field mapping and provide defaults for newly added fields.
	/// </summary>
	public abstract TTo Upcast(TFrom source);

	object IEventUpcaster.Upcast(object source) => Upcast(source: (TFrom)source);
}
