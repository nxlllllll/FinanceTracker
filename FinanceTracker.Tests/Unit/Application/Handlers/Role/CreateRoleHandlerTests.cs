using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Role.Commands.CreateRole;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Role;

public sealed class CreateRoleHandlerTests
{
	private IRoleRepository _roleRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private CreateRoleHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_roleRepository = Substitute.For<IRoleRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task<Guid>>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task<Guid>>>()?.Invoke());

		_handler = new CreateRoleHandler(
			roleRepository: _roleRepository,
			unitOfWork: _unitOfWork,
			dateProvider: FakeDateProvider.Default,
			postCommitNotifications: _postCommitNotifications
		);
	}

	[Test]
	public async Task Handle_ShouldCreateRoleWithGivenNameAndPermissions()
	{
		Guid expectedId = Guid.CreateVersion7();
		_roleRepository.CreateAsync(
			displayName: Arg.Any<Name>(),
			permissions: Arg.Any<IReadOnlySet<Permission>>(),
			createdAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: expectedId);

		Name name = Name.Create(value: "Accountant").Value!;
		IReadOnlySet<Permission> permissions = new HashSet<Permission>
		{
			Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!
		};

		CreateRoleCommand command = new CreateRoleCommand(
			DisplayName: name,
			Permissions: permissions
		);

		Result<Guid, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEqualTo(expected: expectedId);
	}

	[Test]
	public async Task Handle_ShouldCreateTheRoleInsideATransaction()
	{
		_roleRepository.CreateAsync(
			displayName: Arg.Any<Name>(),
			permissions: Arg.Any<IReadOnlySet<Permission>>(),
			createdAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Guid.CreateVersion7());

		CreateRoleCommand command = new CreateRoleCommand(
			DisplayName: Name.Create(value: "Support").Value!,
			Permissions: new HashSet<Permission>()
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _unitOfWork.Received(requiredNumberOfCalls: 1).ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task<Guid>>>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ShouldPassCurrentDateAsCreatedAt()
	{
		_roleRepository.CreateAsync(
			displayName: Arg.Any<Name>(),
			permissions: Arg.Any<IReadOnlySet<Permission>>(),
			createdAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Guid.CreateVersion7());

		CreateRoleCommand command = new CreateRoleCommand(
			DisplayName: Name.Create(value: "Support").Value!,
			Permissions: new HashSet<Permission>()
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _roleRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			displayName: Arg.Any<Name>(),
			permissions: Arg.Any<IReadOnlySet<Permission>>(),
			createdAt: FakeDateProvider.Default.UtcNow,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ShouldStageRoleCreatedNotification()
	{
		Guid expectedId = Guid.CreateVersion7();
		_roleRepository.CreateAsync(
			displayName: Arg.Any<Name>(),
			permissions: Arg.Any<IReadOnlySet<Permission>>(),
			createdAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: expectedId);

		CreateRoleCommand command = new CreateRoleCommand(
			DisplayName: Name.Create(value: "Support").Value!,
			Permissions: new HashSet<Permission>()
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(
			notification: Arg.Any<FinanceTracker.Application.UseCases.Role.Notifications.RoleCreatedNotification>()
		);
	}
}
