using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.User.Commands.ChangeUserNotificationType;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class ChangeUserNotificationTypeHandlerTests
{
	private IUserWriteRepository _userWriteRepository = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private IUnitOfWork _unitOfWork = null!;
	private ChangeUserNotificationTypeHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userWriteRepository = Substitute.For<IUserWriteRepository>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()?.Invoke());

		_handler = new ChangeUserNotificationTypeHandler(
			userWriteRepository: _userWriteRepository,
			unitOfWork: _unitOfWork,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_TurningNotificationsOff_ShouldWriteNull()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeUserNotificationTypeCommand(UserId: user.Id, NewNotificationType: null),
			user: user,
			ct: CancellationToken.None
		);

		await _userWriteRepository.Received(requiredNumberOfCalls: 1).ChangeNotificationTypeAsync(
			userId: user.Id,
			newNotificationType: null,
			expectedVersion: user.RowVersion,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_TurningNotificationsOff_ShouldPublishTheChange()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeUserNotificationTypeCommand(UserId: user.Id, NewNotificationType: null),
			user: user,
			ct: CancellationToken.None
		);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(notification: Arg.Is<UserNotificationTypeChangedNotification>(n =>
			n!.UserId == user.Id &&
			n.OldNotificationType == NotificationType.Email &&
			n.NewNotificationType == null
		));
	}

	[Test]
	public async Task HandleAsync_WithTheCurrentType_ShouldNeitherWriteNorPublish()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ChangeUserNotificationTypeCommand(UserId: user.Id, NewNotificationType: NotificationType.Email),
			user: user,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userWriteRepository.DidNotReceive().ChangeNotificationTypeAsync(
			userId: Arg.Any<Guid>(),
			newNotificationType: Arg.Any<NotificationType?>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<INotification>());
	}
}
