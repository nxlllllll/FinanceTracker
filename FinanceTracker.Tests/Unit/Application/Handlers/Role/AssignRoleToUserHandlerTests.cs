using FinanceTracker.Application.UseCases.Role.Commands.AssignRoleToUser;
using FinanceTracker.Application.UseCases.UserPermission.Commands.GrantPermission;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Role;

public sealed class AssignRoleToUserHandlerTests
{
	private IRoleRepository _roleRepository = null!;
	private ISender _sender = null!;
	private AssignRoleToUserHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_roleRepository = Substitute.For<IRoleRepository>();
		_sender = Substitute.For<ISender>();
		_sender.Send(
			request: Arg.Any<GrantPermissionCommand>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Success(value: FinanceTracker.Core.Results.Unit.Default));

		_handler = new AssignRoleToUserHandler(
			roleRepository: _roleRepository,
			sender: _sender,
			dateProvider: FakeDateProvider.Default
		);
	}

	private static RoleDto BuildRole(Guid roleId, params Permission[] permissions) => new RoleDto(
		Id: roleId,
		SystemKey: null,
		DisplayName: Name.Create(value: "Test Role").Value!,
		Permissions: permissions.ToHashSet()
	);

	[Test]
	public async Task Handle_WhenRoleNotFound_ShouldReturnFailure()
	{
		Guid roleId = Guid.CreateVersion7();
		_roleRepository.GetByIdAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (RoleDto?)null);

		AssignRoleToUserCommand command = new AssignRoleToUserCommand(
			UserId: Guid.CreateVersion7(),
			RoleId: roleId,
			AssignedBy: Guid.CreateVersion7()
		);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
	}

	[Test]
	public async Task Handle_WhenRoleExists_ShouldAssignRoleToUser()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		_roleRepository.GetByIdAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: BuildRole(roleId: roleId, Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!));

		AssignRoleToUserCommand command = new AssignRoleToUserCommand(
			UserId: userId,
			RoleId: roleId,
			AssignedBy: Guid.CreateVersion7()
		);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _roleRepository.Received(requiredNumberOfCalls: 1).AssignToUserAsync(
			userId: userId,
			roleId: roleId,
			assignedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ShouldGrantEveryPermissionOfTheRoleToTheUser()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		Permission accountRead = Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!;
		Permission budgetWrite = Permission.Create(resource: Resource.Budget, action: PermissionAction.Write).Value!;
		_roleRepository.GetByIdAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: BuildRole(roleId: roleId, accountRead, budgetWrite));

		AssignRoleToUserCommand command = new AssignRoleToUserCommand(
			UserId: userId,
			RoleId: roleId,
			AssignedBy: Guid.CreateVersion7()
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _sender.Received(requiredNumberOfCalls: 2).Send(
			request: Arg.Any<GrantPermissionCommand>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
		await _sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GrantPermissionCommand>(predicate: c => c!.TargetUserId == userId && c.Permission == accountRead),
			cancellationToken: Arg.Any<CancellationToken>()
		);
		await _sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GrantPermissionCommand>(predicate: c => c!.TargetUserId == userId && c.Permission == budgetWrite),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}
