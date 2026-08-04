using FinanceTracker.Application.UseCases.UserPermission.Commands.GrantPermission;
using FinanceTracker.Application.UseCases.UserPermission.Commands.RevokePermission;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.E2E;

/// <summary>
/// E2E: GrantPermission / RevokePermission → outbox → RabbitMQ →
/// PermissionEventsConsumer → PermissionProjection → user_permissions read model.
/// </summary>
public sealed class UserPermissionProjectionE2ETests : E2EFixture
{
	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public void SetupData()
		=> _userBuilder = new UserBuilder(context: Context);

	[Test]
	public async Task GrantPermission_AfterOutbox_ShouldProjectPermissionRow()
	{
		Guid targetUserId = await _userBuilder.CreateAsync();
		Guid adminId = Guid.CreateVersion7();

		Result<Core.Results.Unit, AppException> result = await Mediator.Send(request: new GrantPermissionCommand(
			TargetUserId: targetUserId,
			Permission: Permission.Create(resource: Resource.Account, action: PermissionAction.Write).Value!,
			GrantedBy: adminId
		));
		await Assert.That(value: result.IsSuccess).IsTrue();

		await RunOutboxAsync();

		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.UserPermissions.AnyAsync(predicate: p => p.UserId == targetUserId && p.Permission == "account:write");
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();
		bool exists = await readCtx.UserPermissions.AnyAsync(predicate: p => p.UserId == targetUserId && p.Permission == "account:write");

		await Assert.That(value: exists).IsTrue();
	}

	[Test]
	public async Task GrantPermission_Twice_ShouldProjectExactlyOneRow()
	{
		Guid targetUserId = await _userBuilder.CreateAsync();
		Permission permission = Permission.Create(resource: Resource.Balance, action: PermissionAction.Read).Value!;

		await Mediator.Send(request: new GrantPermissionCommand(TargetUserId: targetUserId, Permission: permission, GrantedBy: Guid.CreateVersion7()));
		await RunOutboxAsync();
		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.UserPermissions.AnyAsync(predicate: p => p.UserId == targetUserId);
		});

		// Second grant of the same permission is a domain no-op — no new event, nothing new to project.
		await Mediator.Send(request: new GrantPermissionCommand(TargetUserId: targetUserId, Permission: permission, GrantedBy: Guid.CreateVersion7()));
		await RunOutboxAsync();

		await using FinanceTrackerContext readCtx = CreateReadContext();
		int count = await readCtx.UserPermissions.CountAsync(predicate: p => p.UserId == targetUserId);

		await Assert.That(value: count).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task GrantThenRevokePermission_AfterOutbox_ShouldRemoveProjectedRow()
	{
		Guid targetUserId = await _userBuilder.CreateAsync();
		Permission permission = Permission.Create(resource: Resource.Transaction, action: PermissionAction.Delete).Value!;

		await Mediator.Send(request: new GrantPermissionCommand(TargetUserId: targetUserId, Permission: permission, GrantedBy: Guid.CreateVersion7()));
		await RunOutboxAsync();
		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.UserPermissions.AnyAsync(predicate: p => p.UserId == targetUserId && p.Permission == "transaction:delete");
		});

		Result<Core.Results.Unit, AppException> revokeResult = await Mediator.Send(request: new RevokePermissionCommand(
			TargetUserId: targetUserId,
			Permission: permission,
			RevokedBy: Guid.CreateVersion7()
		));
		await Assert.That(value: revokeResult.IsSuccess).IsTrue();

		await RunOutboxAsync();

		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return !await ctx.UserPermissions.AnyAsync(predicate: p => p.UserId == targetUserId && p.Permission == "transaction:delete" && p.IsActive);
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();
		bool stillActive = await readCtx.UserPermissions.AnyAsync(
			predicate: p => p.UserId == targetUserId && p.Permission == "transaction:delete" && p.IsActive
		);

		await Assert.That(value: stillActive).IsFalse();

		bool tombstoneKept = await readCtx.UserPermissions.AnyAsync(predicate: p => p.UserId == targetUserId && p.Permission == "transaction:delete");
		await Assert.That(value: tombstoneKept).IsTrue().Because(message: """
			The row stays behind on purpose: it records which version revoked the permission, which is
			what stops a grant delivered out of order from putting it back.
		""");
	}

	[Test]
	public async Task GrantMultiplePermissions_AfterOutbox_ShouldProjectAllOfThem()
	{
		Guid targetUserId = await _userBuilder.CreateAsync();

		await Mediator.Send(request: new GrantPermissionCommand(
			TargetUserId: targetUserId,
			Permission: Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!,
			GrantedBy: Guid.CreateVersion7()
		));
		await RunOutboxAsync();
		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.UserPermissions.AnyAsync(predicate: p => p.UserId == targetUserId && p.Permission == "account:read");
		});

		await Mediator.Send(request: new GrantPermissionCommand(
			TargetUserId: targetUserId,
			Permission: Permission.Create(resource: Resource.Budget, action: PermissionAction.Write).Value!,
			GrantedBy: Guid.CreateVersion7()
		));
		await RunOutboxAsync();

		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.UserPermissions.CountAsync(predicate: p => p.UserId == targetUserId) == 2;
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();
		List<string> permissions = await readCtx.UserPermissions
			.Where(predicate: p => p.UserId == targetUserId)
			.Select(selector: p => p.Permission)
			.ToListAsync();

		await Assert.That(value: permissions).Contains(expected: "account:read");
		await Assert.That(value: permissions).Contains(expected: "budget:write");
	}

	[Test]
	public async Task GrantPermission_ToSelf_ShouldFailAndProjectNothing()
	{
		Guid userId = await _userBuilder.CreateAsync();

		Result<Core.Results.Unit, AppException> result = await Mediator.Send(request: new GrantPermissionCommand(
			TargetUserId: userId,
			Permission: Permission.Create(resource: Resource.Account, action: PermissionAction.Write).Value!,
			GrantedBy: userId
		));

		await Assert.That(value: result.IsFailure).IsTrue();

		await RunOutboxAsync();

		await using FinanceTrackerContext readCtx = CreateReadContext();
		bool exists = await readCtx.UserPermissions.AnyAsync(predicate: p => p.UserId == userId);

		await Assert.That(value: exists).IsFalse();
	}
}
