using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.UserPermission.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.UserPermission;

/// <summary>
/// The set of permissions granted to a single user. One
/// aggregate instance per user. Grant/Revoke are idempotent.
/// </summary>
public sealed class UserPermission : AggregateRoot
{
	private readonly HashSet<string> _permissions = [];

	public Guid UserId { get; private set; }

	/// <summary>Currently held permissions, in their persisted "resource:action" form.</summary>
	public IReadOnlySet<string> Permissions => _permissions;

	private UserPermission() { }

	public static Result<UserPermission, DomainException> Create(
		DateTimeOffset occurredAt,
		Guid userId)
	{
		UserPermission userPermission = new UserPermission();

		userPermission.RaiseEvent(@event: new UserPermissionCreated(
			Id: Guid.CreateVersion7(),
			UserId: userId,
			Version: 0,
			OccurredAt: occurredAt
		));

		return Result<UserPermission, DomainException>.Success(value: userPermission);
	}

	public static UserPermission ReconstituteFromHistory(IReadOnlyList<IEvent> history)
	{
		UserPermission userPermission = new UserPermission();
		userPermission.LoadEventsFromHistory(history: history);
		return userPermission;
	}

	private void Apply(UserPermissionCreated @event)
	{
		Id = @event.UserId;
		UserId = @event.UserId;
	}

	private void Apply(PermissionGranted @event) => _permissions.Add(item: @event.Permission);

	private void Apply(PermissionRevoked @event) => _permissions.Remove(item: @event.Permission);

	protected override void Apply(IEvent @event)
	{
		switch (@event)
		{
			case UserPermissionCreated e: Apply(@event: e); break;
			case PermissionGranted e: Apply(@event: e); break;
			case PermissionRevoked e: Apply(@event: e); break;
			default: throw new UnknownEventException(message: "Event is unknown.", eventType: @event.GetType());
		}
	}

	public Result<Unit, DomainException> Grant(
		DateTimeOffset occurredAt,
		Guid grantedBy,
		Permission permission)
	{
		if (_permissions.Contains(item: permission.ToString()))
			return Result<Unit, DomainException>.Success(value: Unit.Default);

		RaiseEvent(@event: new PermissionGranted(
			Id: Guid.CreateVersion7(),
			UserId: UserId,
			GrantedBy: grantedBy,
			Permission: permission.ToString(),
			Version: 0,
			OccurredAt: occurredAt
		));

		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	public Result<Unit, DomainException> Revoke(
		DateTimeOffset occurredAt,
		Guid revokedBy,
		Permission permission)
	{
		if (!_permissions.Contains(item: permission.ToString()))
			return Result<Unit, DomainException>.Success(value: Unit.Default);

		RaiseEvent(@event: new PermissionRevoked(
			Id: Guid.CreateVersion7(),
			UserId: UserId,
			RevokedBy: revokedBy,
			Permission: permission.ToString(),
			Version: 0,
			OccurredAt: occurredAt
		));

		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
}
