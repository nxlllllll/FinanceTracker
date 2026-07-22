using FinanceTracker.Application.UseCases.Role.Commands.AssignRoleToUser;
using FinanceTracker.Application.UseCases.Role.Commands.CreateRole;
using FinanceTracker.Application.UseCases.Role.Commands.RemoveRoleFromUser;
using FinanceTracker.Application.UseCases.Role.Commands.UpdateRolePermissions;
using FinanceTracker.Application.UseCases.Role.Queries.GetRoles;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.E2E;

/// <summary>
/// E2E: CreateRole / AssignRoleToUser / UpdateRolePermissions / RemoveRoleFromUser → fan-out →
/// outbox → RabbitMQ → PermissionEventsConsumer → user_permissions read model.
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
		));
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
			return await ctx.UserPermissions.CountAsync(predicate: p => p.UserId == targetUserId) == 2;
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();
		List<string> permissions = await readCtx.UserPermissions
			.Where(predicate: p => p.UserId == targetUserId)
			.Select(selector: p => p.Permission)
			.ToListAsync();

		await Assert.That(value: permissions).Contains(expected: "account:read");
		await Assert.That(value: permissions).Contains(expected: "budget:read");
	}

	[Test]
	public async Task UpdateRolePermissions_AfterOutbox_ShouldFanOutAddAndRemoveToExistingMember()
	{
		Guid memberUserId = await _userBuilder.CreateAsync();
		Guid adminId = Guid.CreateVersion7();
		Permission accountRead = Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!;
		Permission categoryRead = Permission.Create(resource: Resource.Category, action: PermissionAction.Read).Value!;

		Result<Guid, AppException> roleResult = await Mediator.Send(request: new CreateRoleCommand(
			DisplayName: Name.Create(value: "E2E Updatable").Value!,
			Permissions: new HashSet<Permission> { accountRead }
		));
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
			return await ctx.UserPermissions.AnyAsync(predicate: p => p.UserId == memberUserId && p.Permission == "account:read");
		});

		Result<Core.Results.Unit, AppException> updateResult = await Mediator.Send(request: new UpdateRolePermissionsCommand(
			RoleId: roleId,
			NewPermissions: new HashSet<Permission> { categoryRead },
			UpdatedBy: adminId
		));
		await Assert.That(value: updateResult.IsSuccess).IsTrue();

		await WaitForConditionAsync(condition: async () =>
		{
			await RunOutboxAsync();
			await using FinanceTrackerContext ctx = CreateReadContext();
			bool hasNew = await ctx.UserPermissions.AnyAsync(predicate: p => p.UserId == memberUserId && p.Permission == "category:read");
			bool lacksOld = !await ctx.UserPermissions.AnyAsync(predicate: p => p.UserId == memberUserId && p.Permission == "account:read");
			return hasNew && lacksOld;
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();
		List<string> permissions = await readCtx.UserPermissions.Where(predicate: p => p.UserId == memberUserId)
			.Select(selector: p => p.Permission).ToListAsync();

		await Assert.That(value: permissions).Contains(expected: "category:read");
		await Assert.That(value: permissions).DoesNotContain(expected: "account:read");
	}

	[Test]
	public async Task RemoveRoleFromUser_AfterOutbox_ShouldRevokeAllRolePermissions()
	{
		Guid memberUserId = await _userBuilder.CreateAsync();
		Guid adminId = Guid.CreateVersion7();

		Result<Guid, AppException> roleResult = await Mediator.Send(request: new CreateRoleCommand(
			DisplayName: Name.Create(value: "E2E Removable").Value!,
			Permissions: new HashSet<Permission> { Permission.Create(resource: Resource.Transaction, action: PermissionAction.Read).Value! }
		));
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
			return await ctx.UserPermissions.AnyAsync(predicate: p => p.UserId == memberUserId);
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
			return !await ctx.UserPermissions.AnyAsync(predicate: p => p.UserId == memberUserId);
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();
		bool stillHasAny = await readCtx.UserPermissions.AnyAsync(predicate: p => p.UserId == memberUserId);
		await Assert.That(value: stillHasAny).IsFalse();
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
}
