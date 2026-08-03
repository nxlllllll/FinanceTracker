using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.UserRole;
using FinanceTracker.Core.Domains.UserRole.Events;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core.Domains;

public sealed class UserRoleTests
{
	private static DateTimeOffset Now => FakeDateProvider.Default.UtcNow;

	[Test]
	public async Task Create_WithValidData_ShouldRaiseUserRoleCreatedEvent()
	{
		UserRole userRole = UserRoleFactory.Create();

		await Assert.That(value: userRole.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: userRole.Events[0]).IsTypeOf<UserRoleCreated>();
	}

	[Test]
	public async Task Create_WithValidData_ShouldSetCorrectState()
	{
		Guid userId = Guid.CreateVersion7();

		UserRole userRole = UserRoleFactory.Create(userId: userId);

		await Assert.That(value: userRole.UserId).IsEqualTo(expected: userId);
		await Assert.That(value: userRole.Id).IsEqualTo(expected: userId);
		await Assert.That(value: userRole.RoleIds).IsEmpty();
		await Assert.That(value: userRole.Version).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Assign_WithNewRole_ShouldRaiseRoleAssignedEvent()
	{
		UserRole userRole = UserRoleFactory.Create();
		userRole.ClearEvents();
		Guid roleId = Guid.CreateVersion7();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = userRole.Assign(
			occurredAt: Now,
			roleId: roleId,
			assignedBy: Guid.CreateVersion7()
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: userRole.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: userRole.Events[0]).IsTypeOf<RoleAssigned>();
		await Assert.That(value: userRole.RoleIds).Contains(expected: roleId);
	}

	[Test]
	public async Task Assign_ShouldRecordWhoAssignedTheRole()
	{
		UserRole userRole = UserRoleFactory.Create();
		userRole.ClearEvents();
		Guid assignedBy = Guid.CreateVersion7();

		userRole.Assign(occurredAt: Now, roleId: Guid.CreateVersion7(), assignedBy: assignedBy);

		RoleAssigned raised = (RoleAssigned)userRole.Events[0];
		await Assert.That(value: raised.AssignedBy).IsEqualTo(expected: assignedBy).Because(message: """
			Membership is the only audit trail left for role changes, so the actor has to travel in the
			event itself — there is nowhere else to recover it from.
		""");
	}

	[Test]
	public async Task Assign_AlreadyHeldRole_ShouldBeIdempotentAndRaiseNoEvent()
	{
		Guid roleId = Guid.CreateVersion7();
		UserRole userRole = UserRoleFactory.CreateWithRole(roleId: roleId);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = userRole.Assign(
			occurredAt: Now,
			roleId: roleId,
			assignedBy: Guid.CreateVersion7()
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: userRole.Events).IsEmpty().Because(message: """
			A retried assignment must not append a second event. The concurrency retry behaviour replays
			handlers after a version conflict, so this path is taken in normal operation, not just on
			double-clicks.
		""");
		await Assert.That(value: userRole.RoleIds).Count().IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Remove_HeldRole_ShouldRaiseRoleRemovedEvent()
	{
		Guid roleId = Guid.CreateVersion7();
		UserRole userRole = UserRoleFactory.CreateWithRole(roleId: roleId);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = userRole.Remove(
			occurredAt: Now,
			roleId: roleId,
			removedBy: Guid.CreateVersion7()
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: userRole.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: userRole.Events[0]).IsTypeOf<RoleRemoved>();
		await Assert.That(value: userRole.RoleIds).IsEmpty();
	}

	[Test]
	public async Task Remove_RoleNotHeld_ShouldBeIdempotentAndRaiseNoEvent()
	{
		UserRole userRole = UserRoleFactory.Create();
		userRole.ClearEvents();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = userRole.Remove(
			occurredAt: Now,
			roleId: Guid.CreateVersion7(),
			removedBy: Guid.CreateVersion7()
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: userRole.Events).IsEmpty();
	}

	[Test]
	public async Task Remove_ShouldOnlyDropTheNamedRole()
	{
		Guid kept = Guid.CreateVersion7();
		Guid dropped = Guid.CreateVersion7();

		UserRole userRole = UserRoleFactory.CreateWithRole(roleId: kept);
		userRole.Assign(occurredAt: Now, roleId: dropped, assignedBy: Guid.CreateVersion7());
		userRole.ClearEvents();

		userRole.Remove(occurredAt: Now, roleId: dropped, removedBy: Guid.CreateVersion7());

		await Assert.That(value: userRole.RoleIds).Contains(expected: kept);
		await Assert.That(value: userRole.RoleIds).DoesNotContain(expected: dropped);
	}

	[Test]
	public async Task ReconstituteFromHistory_ShouldRebuildMembershipSet()
	{
		Guid userId = Guid.CreateVersion7();
		Guid kept = Guid.CreateVersion7();
		Guid dropped = Guid.CreateVersion7();

		IReadOnlyList<IEvent> history =
		[
			new UserRoleCreated(
				Id: Guid.CreateVersion7(),
				UserId: userId,
				Version: 1,
				OccurredAt: Now
			),
			new RoleAssigned(
				Id: Guid.CreateVersion7(),
				UserId: userId,
				RoleId: kept,
				AssignedBy: Guid.CreateVersion7(),
				Version: 2,
				OccurredAt: Now
			),
			new RoleAssigned(
				Id: Guid.CreateVersion7(),
				UserId: userId,
				RoleId: dropped,
				AssignedBy: Guid.CreateVersion7(),
				Version: 3,
				OccurredAt: Now
			),
			new RoleRemoved(
				Id: Guid.CreateVersion7(),
				UserId: userId,
				RoleId: dropped,
				RemovedBy: Guid.CreateVersion7(),
				Version: 4,
				OccurredAt: Now
			)
		];

		UserRole reconstituted = UserRole.ReconstituteFromHistory(history: history);

		await Assert.That(value: reconstituted.UserId).IsEqualTo(expected: userId);
		await Assert.That(value: reconstituted.RoleIds).Contains(expected: kept);
		await Assert.That(value: reconstituted.RoleIds).DoesNotContain(expected: dropped);
		await Assert.That(value: reconstituted.Version).IsEqualTo(expected: 4);
	}
}
