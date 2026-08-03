using FinanceTracker.Application.UseCases.Role.Commands.AssignRoleToUser;
using FinanceTracker.Application.UseCases.Role.Commands.CreateRole;
using FinanceTracker.Application.UseCases.Role.Commands.RemoveRoleFromUser;
using FinanceTracker.Application.UseCases.Role.Commands.UpdateRolePermissions;
using FinanceTracker.Application.UseCases.Role.Queries.GetRoles;
using FinanceTracker.Application.UseCases.UserPermission.Commands.GrantPermission;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Repositories.UserPermission;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.E2E;

/// <summary>
/// E2E: CreateRole / AssignRoleToUser / UpdateRolePermissions / RemoveRoleFromUser → outbox →
/// RabbitMQ → UserRoleEventsConsumer → user_roles, and from there into effective permissions.
/// </summary>
public sealed class RoleE2ETests : E2EFixture
{
	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public void SetupData()
		=> _userBuilder = new UserBuilder(context: Context);

	private async Task<Guid> GetRootRoleIdAsync()
	{
		Result<IReadOnlyList<RoleDto>, AppException> roles = await Mediator.Send(request: new GetRolesQuery());
		return roles.Value!.Single(predicate: r => r.SystemKey == SystemRole.Root).Id;
	}

	private static async Task<IReadOnlySet<string>> EffectivePermissionsAsync(
		FinanceTrackerContext ctx,
		Guid userId
	) => await new UserPermissionReadRepository(context: ctx).GetPermissionsAsync(userId: userId, ct: CancellationToken.None);

	[Test]
	public async Task AssignRoleToUser_AfterOutbox_ShouldProjectAllRolePermissionsForMember()
	{
		Guid targetUserId = await _userBuilder.CreateAsync();
		Guid adminId = Guid.CreateVersion7();

		Result<Guid, AppException> roleResult = await Mediator.Send(request: new CreateRoleCommand(
			DisplayName: Name.Create(value: "E2E Viewer").Value!,
			Permissions: new HashSet<Permission>
			{
				Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!,
				Permission.Create(resource: Resource.Budget, action: PermissionAction.Read).Value!
			}
		)
		{ IdempotencyKey = Guid.CreateVersion7() });
		await Assert.That(value: roleResult.IsSuccess).IsTrue();
		Guid roleId = roleResult.Value;

		Result<Core.Results.Unit, AppException> assignResult = await Mediator.Send(request: new AssignRoleToUserCommand(
			UserId: targetUserId,
			RoleId: roleId,
			AssignedBy: adminId
		));
		await Assert.That(value: assignResult.IsSuccess).IsTrue();

		await WaitForConditionAsync(condition: async () =>
		{
			await RunOutboxAsync();
			await using FinanceTrackerContext ctx = CreateReadContext();
			IReadOnlySet<string> permissions = await EffectivePermissionsAsync(ctx: ctx, userId: targetUserId);
			return permissions.Contains(item: "account:read") && permissions.Contains(item: "budget:read");
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();

		IReadOnlySet<string> effective = await EffectivePermissionsAsync(ctx: readCtx, userId: targetUserId);
		await Assert.That(value: effective).Contains(expected: "account:read");
		await Assert.That(value: effective).Contains(expected: "budget:read");

		bool copiedOntoTheUser = await readCtx.UserPermissions.AnyAsync(predicate: p => p.UserId == targetUserId);
		await Assert.That(value: copiedOntoTheUser).IsFalse().Because(message: """
			The role's permissions must stay in the role. Copying them onto the member is what used to make
			removing one role take away access another one still granted.
		""");
	}

	[Test]
	public async Task UpdateRolePermissions_ShouldChangeWhatExistingMembersCanDo()
	{
		Guid memberUserId = await _userBuilder.CreateAsync();
		Guid adminId = Guid.CreateVersion7();
		Permission accountRead = Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!;
		Permission categoryRead = Permission.Create(resource: Resource.Category, action: PermissionAction.Read).Value!;

		Result<Guid, AppException> roleResult = await Mediator.Send(request: new CreateRoleCommand(
			DisplayName: Name.Create(value: "E2E Viewer").Value!,
			Permissions: new HashSet<Permission>
			{
				Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!,
				Permission.Create(resource: Resource.Budget, action: PermissionAction.Read).Value!
			}
		)
		{ IdempotencyKey = Guid.CreateVersion7() });
		Guid roleId = roleResult.Value;

		await Mediator.Send(request: new AssignRoleToUserCommand(
			UserId: memberUserId,
			RoleId: roleId,
			AssignedBy: adminId
		));

		await WaitForConditionAsync(condition: async () =>
		{
			await RunOutboxAsync();
			await using FinanceTrackerContext ctx = CreateReadContext();
			return (await EffectivePermissionsAsync(ctx: ctx, userId: memberUserId)).Contains(item: "account:read");
		});

		Result<Core.Results.Unit, AppException> updateResult = await Mediator.Send(request: new UpdateRolePermissionsCommand(
			RoleId: roleId,
			NewPermissions: new HashSet<Permission> { categoryRead },
			UpdatedBy: adminId
		));
		await Assert.That(value: updateResult.IsSuccess).IsTrue();

		await using FinanceTrackerContext readCtx = CreateReadContext();
		IReadOnlySet<string> effective = await EffectivePermissionsAsync(ctx: readCtx, userId: memberUserId);

		await Assert.That(value: effective).Contains(expected: "category:read");
		await Assert.That(value: effective).DoesNotContain(expected: "account:read").Because(message: """
			Changing a role changes what its members can do immediately — there is nothing to fan out and
			no projection to wait for, because the permissions were never copied anywhere.
		""");
	}

	[Test]
	public async Task RemoveRoleFromUser_AfterOutbox_ShouldRevokeAllRolePermissions()
	{
		Guid memberUserId = await _userBuilder.CreateAsync();
		Guid adminId = Guid.CreateVersion7();

		Result<Guid, AppException> roleResult = await Mediator.Send(request: new CreateRoleCommand(
			DisplayName: Name.Create(value: "E2E Viewer").Value!,
			Permissions: new HashSet<Permission>
			{
				Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!,
				Permission.Create(resource: Resource.Budget, action: PermissionAction.Read).Value!
			}
		)
		{ IdempotencyKey = Guid.CreateVersion7() });
		Guid roleId = roleResult.Value;

		await Mediator.Send(request: new AssignRoleToUserCommand(
			UserId: memberUserId,
			RoleId: roleId,
			AssignedBy: adminId
		));
		await WaitForConditionAsync(condition: async () =>
		{
			await RunOutboxAsync();
			await using FinanceTrackerContext ctx = CreateReadContext();
			return (await EffectivePermissionsAsync(ctx: ctx, userId: memberUserId)).Count == 2;
		});

		Result<Core.Results.Unit, AppException> removeResult = await Mediator.Send(request: new RemoveRoleFromUserCommand(
			UserId: memberUserId,
			RoleId: roleId,
			RemovedBy: adminId
		));
		await Assert.That(value: removeResult.IsSuccess).IsTrue();

		await WaitForConditionAsync(condition: async () =>
		{
			await RunOutboxAsync();
			await using FinanceTrackerContext ctx = CreateReadContext();
			return (await EffectivePermissionsAsync(ctx: ctx, userId: memberUserId)).Count == 0;
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();
		IReadOnlySet<string> effective = await EffectivePermissionsAsync(ctx: readCtx, userId: memberUserId);
		await Assert.That(value: effective).IsEmpty();
	}

	[Test]
	public async Task RemoveRoleFromUser_ForLastRootHolder_ShouldFailAndProjectNothingChanged()
	{
		Guid soleRootUserId = await _userBuilder.CreateAsync();
		Guid rootRoleId = await GetRootRoleIdAsync();

		Result<Core.Results.Unit, AppException> assignResult = await Mediator.Send(request: new AssignRoleToUserCommand(
			UserId: soleRootUserId,
			RoleId: rootRoleId,
			AssignedBy: Guid.CreateVersion7()
		));
		await Assert.That(value: assignResult.IsSuccess).IsTrue();

		Result<Core.Results.Unit, AppException> removeResult = await Mediator.Send(request: new RemoveRoleFromUserCommand(
			UserId: soleRootUserId,
			RoleId: rootRoleId,
			RemovedBy: soleRootUserId
		));

		await Assert.That(value: removeResult.IsFailure).IsTrue();
		await Assert.That(value: removeResult.Error).IsTypeOf<LastRootRoleException>();
	}

	[Test]
	public async Task RemoveRoleFromUser_ShouldKeepAPermissionTheUserAlsoHoldsDirectly()
	{
		Guid memberUserId = await _userBuilder.CreateAsync();
		Guid adminId = Guid.CreateVersion7();
		Permission accountRead = Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!;

		Result<Guid, AppException> roleResult = await Mediator.Send(request: new CreateRoleCommand(
			DisplayName: Name.Create(value: "E2E Reader").Value!,
			Permissions: new HashSet<Permission> { accountRead }
		) { IdempotencyKey = Guid.CreateVersion7() });
		Guid roleId = roleResult.Value;

		await Mediator.Send(request: new GrantPermissionCommand(
			TargetUserId: memberUserId,
			Permission: accountRead,
			GrantedBy: adminId
		));

		await Mediator.Send(request: new AssignRoleToUserCommand(
			UserId: memberUserId,
			RoleId: roleId,
			AssignedBy: adminId
		));

		await WaitForConditionAsync(condition: async () =>
		{
			await RunOutboxAsync();
			await using FinanceTrackerContext ctx = CreateReadContext();
			return (await EffectivePermissionsAsync(ctx: ctx, userId: memberUserId)).Contains(item: "account:read");
		});

		Result<Core.Results.Unit, AppException> removeResult = await Mediator.Send(request: new RemoveRoleFromUserCommand(
			UserId: memberUserId,
			RoleId: roleId,
			RemovedBy: adminId
		));
		await Assert.That(value: removeResult.IsSuccess).IsTrue();

		await WaitForConditionAsync(condition: async () =>
		{
			await RunOutboxAsync();
			await using FinanceTrackerContext ctx = CreateReadContext();
			return !await ctx.UserRoles.AnyAsync(predicate: ur => ur.UserId == memberUserId && ur.IsActive);
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();
		IReadOnlySet<string> effective = await EffectivePermissionsAsync(ctx: readCtx, userId: memberUserId);

		await Assert.That(value: effective).Contains(expected: "account:read").Because(message: """
			The permission was granted to this user personally as well. Losing the role must not take it
			away — that silent loss of access is the entire reason the two sources were separated.
		""");
	}
}
