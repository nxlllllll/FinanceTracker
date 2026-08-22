using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.User.Commands.ChangeUserBaseCurrency;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class ChangeUserBaseCurrencyHandlerTests
{
	private IUserWriteRepository _userWriteRepository = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private IBaseCurrencyRecalculationWriteRepository _recalculationWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private ChangeUserBaseCurrencyHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userWriteRepository = Substitute.For<IUserWriteRepository>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_recalculationWriteRepository = Substitute.For<IBaseCurrencyRecalculationWriteRepository>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()?.Invoke());

		_handler = new ChangeUserBaseCurrencyHandler(
			userWriteRepository: _userWriteRepository,
			recalculationWriteRepository: _recalculationWriteRepository,
			unitOfWork: _unitOfWork,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldChangeBaseCurrency()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeUserBaseCurrencyCommand(UserId: user.Id, NewBaseCurrency: FinanceTracker.Core.ValueObjects.Currency.Create(value: "USD").Value),
			user: user,
			ct: CancellationToken.None
		);

		await _userWriteRepository.Received(requiredNumberOfCalls: 1).ChangeBaseCurrencyAsync(
			userId: Arg.Is(value: user.Id),
			newBaseCurrencyCode: Arg.Is(value: FinanceTracker.Core.ValueObjects.Currency.Create(value: "USD").Value),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldPublishUserBaseCurrencyChangedNotification()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;

		await _handler.HandleAsync(
			command: new ChangeUserBaseCurrencyCommand(UserId: user.Id, NewBaseCurrency: FinanceTracker.Core.ValueObjects.Currency.Create(value: "USD").Value),
			user: user,
			ct: CancellationToken.None
		);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(notification: Arg.Is<UserBaseCurrencyChangedNotification>(n =>
			n!.UserId == user.Id &&
			n.OldBaseCurrency.Value == "RUB" &&
			n.NewBaseCurrency.Value == "USD"
		));
	}

	[Test]
	public async Task HandleAsync_WithSameCurrency_ShouldNotChangeBaseCurrency()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ChangeUserBaseCurrencyCommand(UserId: user.Id, NewBaseCurrency: FinanceTracker.Core.ValueObjects.Currency.Create(value: "RUB").Value),
			user: user,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userWriteRepository.DidNotReceive().ChangeBaseCurrencyAsync(
			userId: Arg.Any<Guid>(),
			newBaseCurrencyCode: Arg.Any<FinanceTracker.Core.ValueObjects.Currency>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithSameCurrency_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;

		await _handler.HandleAsync(
			command: new ChangeUserBaseCurrencyCommand(UserId: user.Id, NewBaseCurrency: FinanceTracker.Core.ValueObjects.Currency.Create(value: "RUB").Value),
			user: user,
			ct: CancellationToken.None
		);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<INotification>());
	}

	[Test]
	public async Task HandleAsync_ShouldRequestARebuildInsteadOfDoingItInline()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;
		FinanceTracker.Core.ValueObjects.Currency usd = FinanceTracker.Core.ValueObjects.Currency.Create(value: "USD").Value;

		await _handler.HandleAsync(
			command: new ChangeUserBaseCurrencyCommand(UserId: user.Id, NewBaseCurrency: usd),
			user: user
		);

		await _recalculationWriteRepository.Received(requiredNumberOfCalls: 1).RequestAsync(
			userId: user.Id,
			targetCurrency: usd,
			requestedAt: FakeDateProvider.Default.UtcNow,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenTheCurrencyIsUnchanged_ShouldNotRequestARebuild()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;
		FinanceTracker.Core.ValueObjects.Currency usd = FinanceTracker.Core.ValueObjects.Currency.Create(value: "USD").Value;

		await _handler.HandleAsync(
			command: new ChangeUserBaseCurrencyCommand(UserId: user.Id, NewBaseCurrency: user.BaseCurrency),
			user: user
		);

		await _recalculationWriteRepository.DidNotReceive().RequestAsync(
			userId: Arg.Any<Guid>(),
			targetCurrency: Arg.Any<FinanceTracker.Core.ValueObjects.Currency>(),
			requestedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
