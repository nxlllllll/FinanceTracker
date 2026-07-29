using FinanceTracker.Application.Services.Permissions;
using FinanceTracker.Application.UseCases.Role.Commands.DeleteRole;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using NSubstitute;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Role;

public sealed class DeleteRoleHandlerTests
{
	private static readonly Permission AccountRead = Permission.Create(
		resource: Resource.Account,
		action: PermissionAction.Read
	).Value!;
	private static readonly Permission BudgetWrite = Permission.Create(
		resource: Resource.Budget,
		action: PermissionAction.Write
	).Value!;

	private IRoleRepository _roleRepository = null!;
	private IUserPermissionService _userPermissionService = null!;
	private IUnitOfWork _unitOfWork = null!;
	private DeleteRoleHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_roleRepository = Substitute.For<IRoleRepository>();
		_userPermissionService = Substitute.For<IUserPermissionService>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task<Result<FinanceTracker.Core.Results.Unit, AppException>>>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task<Result<FinanceTracker.Core.Results.Unit, AppException>>>>()?.Invoke());

		_userPermissionService.RevokeAsync(
			targetUserId: Arg.Any<Guid>(),
			revokedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Success(value: FinanceTracker.Core.Results.Unit.Default));

		_handler = new DeleteRoleHandler(
			roleRepository: _roleRepository,
			userPermissionService: _userPermissionService,
			unitOfWork: _unitOfWork
		);
	}

	private static RoleDto BuildRole(
		Guid roleId,
		SystemRole? systemKey,
		params Permission[] permissions
	) => new RoleDto(
		Id: roleId,
		SystemKey: systemKey,
		DisplayName: Name.Create(value: "Test Role").Value!,
		Permissions: permissions.ToHashSet()
	);

	private void Arrange(Guid roleId, RoleDto? role, params Guid[] memberUserIds)
	{
		_roleRepository.GetByIdAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: role);
		_roleRepository.GetMemberUserIdsAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [..memberUserIds]);
	}

	private static DeleteRoleCommand Command(Guid roleId) => new DeleteRoleCommand(
		RoleId: roleId,
		DeletedBy: Guid.CreateVersion7()
	);

	[Test]
	public async Task Handle_WhenRoleNotFound_ShouldReturnFailure()
	{
		Guid roleId = Guid.CreateVersion7();
		Arrange(roleId: roleId, role: null);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: Command(roleId: roleId), ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task Handle_ForSystemRole_ShouldFailAndDeleteNothing()
	{
		Guid roleId = Guid.CreateVersion7();
		Arrange(
			roleId: roleId,
			role: BuildRole(roleId: roleId, systemKey: SystemRole.User, AccountRead),
			memberUserIds: Guid.CreateVersion7()
		);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: Command(roleId: roleId), ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<CannotDeleteSystemRoleException>();

		await _roleRepository.DidNotReceive().DeleteAsync(
			roleId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _userPermissionService.DidNotReceive().RevokeAsync(
			targetUserId: Arg.Any<Guid>(),
			revokedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ShouldRevokeTheRolesPermissionsOncePerMemberThenDelete()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid firstMember = Guid.CreateVersion7();
		Guid secondMember = Guid.CreateVersion7();
		Arrange(
			roleId: roleId,
			role: BuildRole(roleId: roleId, systemKey: null, AccountRead, BudgetWrite),
			memberUserIds: [firstMember, secondMember]
		);

		DeleteRoleCommand command = Command(roleId: roleId);
		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();

		await _userPermissionService.Received(requiredNumberOfCalls: 1).RevokeAsync(
			targetUserId: firstMember,
			revokedBy: command.DeletedBy,
			permissions: Arg.Is<IReadOnlyCollection<Permission>>(predicate: p => p!.Count == 2),
			ct: Arg.Any<CancellationToken>()
		);
		await _userPermissionService.Received(requiredNumberOfCalls: 1).RevokeAsync(
			targetUserId: secondMember,
			revokedBy: command.DeletedBy,
			permissions: Arg.Is<IReadOnlyCollection<Permission>>(predicate: p => p!.Count == 2),
			ct: Arg.Any<CancellationToken>()
		);
		await _roleRepository.Received(requiredNumberOfCalls: 1).DeleteAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WithNoMembers_ShouldStillDeleteTheRole()
	{
		Guid roleId = Guid.CreateVersion7();
		Arrange(
			roleId: roleId,
			role: BuildRole(roleId: roleId, systemKey: null, AccountRead)
		);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: Command(roleId: roleId), ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _roleRepository.Received(requiredNumberOfCalls: 1).DeleteAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenRevokingFails_ShouldReturnFailureAndNotDeleteTheRole()
	{
		Guid roleId = Guid.CreateVersion7();
		Arrange(
			roleId: roleId,
			role: BuildRole(roleId: roleId, systemKey: null, AccountRead),
			memberUserIds: Guid.CreateVersion7()
		);

		_userPermissionService.RevokeAsync(
			targetUserId: Arg.Any<Guid>(),
			revokedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Failure(error: new NotFoundException(message: "gone", id: Guid.Empty)));

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: Command(roleId: roleId), ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await _roleRepository.DidNotReceive().DeleteAsync(
			roleId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
