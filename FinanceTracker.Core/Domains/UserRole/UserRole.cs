using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.UserRole.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.Domains.UserRole;

/// <summary>The roles a single user belongs to. One aggregate instance per user.</summary>
public sealed class UserRole : AggregateRoot
{
	private readonly HashSet<Guid> _roleIds = [];

	public Guid UserId { get; private set; }
	public IReadOnlySet<Guid> RoleIds => _roleIds;

	private UserRole() { }

	public static Result<UserRole, DomainException> Create(
		DateTimeOffset occurredAt,
		Guid userId)
	{
		UserRole userRole = new UserRole();

		userRole.RaiseEvent(@event: new UserRoleCreated(
			Id: Guid.CreateVersion7(),
			UserId: userId,
			Version: 0,
			OccurredAt: occurredAt
		));

		return Result<UserRole, DomainException>.Success(value: userRole);
	}

	public static UserRole ReconstituteFromHistory(IReadOnlyList<IEvent> history)
	{
		UserRole userRole = new UserRole();
		userRole.LoadEventsFromHistory(history: history);
		return userRole;
	}

	private void Apply(UserRoleCreated @event)
	{
		Id = @event.UserId;
		UserId = @event.UserId;
	}

	private void Apply(RoleAssigned @event) => _roleIds.Add(item: @event.RoleId);

	private void Apply(RoleRemoved @event) => _roleIds.Remove(item: @event.RoleId);

	protected override void Apply(IEvent @event)
	{
		switch (@event)
		{
			case UserRoleCreated e: Apply(@event: e); break;
			case RoleAssigned e: Apply(@event: e); break;
			case RoleRemoved e: Apply(@event: e); break;
			default: throw new UnknownEventException(message: "Event is unknown.", eventType: @event.GetType());
		}
	}

	/// <summary>Adds <paramref name="roleId"/> to the user's memberships.</summary>
	public Result<Unit, DomainException> Assign(
		DateTimeOffset occurredAt,
		Guid roleId,
		Guid assignedBy)
	{
		if (_roleIds.Contains(item: roleId))
			return Result<Unit, DomainException>.Success(value: Unit.Default);

		RaiseEvent(@event: new RoleAssigned(
			Id: Guid.CreateVersion7(),
			UserId: UserId,
			RoleId: roleId,
			AssignedBy: assignedBy,
			Version: 0,
			OccurredAt: occurredAt
		));

		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	/// <summary>Drops <paramref name="roleId"/> from the user's memberships.</summary>
	public Result<Unit, DomainException> Remove(
		DateTimeOffset occurredAt,
		Guid roleId,
		Guid removedBy)
	{
		if (!_roleIds.Contains(item: roleId))
			return Result<Unit, DomainException>.Success(value: Unit.Default);

		RaiseEvent(@event: new RoleRemoved(
			Id: Guid.CreateVersion7(),
			UserId: UserId,
			RoleId: roleId,
			RemovedBy: removedBy,
			Version: 0,
			OccurredAt: occurredAt
		));

		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
}
