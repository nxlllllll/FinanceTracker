using FinanceTracker.Application.UseCases.User.Commands.ChangeUserBaseCurrency;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class ChangeUserBaseCurrencyHandlerTests
{
	private IUserWriteRepository _userWriteRepository = null!;
	private ICategoryTotalWriteRepository _categoryTotalWriteRepository = null!;
	private IPublisher _publisher = null!;
	private IUnitOfWork _unitOfWork = null!;
	private ChangeUserBaseCurrencyHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userWriteRepository = Substitute.For<IUserWriteRepository>();
		_categoryTotalWriteRepository = Substitute.For<ICategoryTotalWriteRepository>();
		_publisher = Substitute.For<IPublisher>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());

		_handler = new ChangeUserBaseCurrencyHandler(
			userWriteRepository: _userWriteRepository,
			categoryTotalWriteRepository: _categoryTotalWriteRepository,
			unitOfWork: _unitOfWork,
			publisher: _publisher,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<ChangeUserBaseCurrencyHandler>>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldChangeBaseCurrency()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeUserBaseCurrencyCommand(UserId: user.Id, NewBaseCurrency: Currency.Create(value: "USD").Value),
			user: user,
			ct: CancellationToken.None
		);

		await _userWriteRepository.Received(requiredNumberOfCalls: 1).ChangeBaseCurrencyAsync(
			userId: Arg.Is(value: user.Id),
			newBaseCurrencyCode: Arg.Is<Currency>(value: Currency.Create(value: "USD").Value),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldRecalculateCategoryTotals()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;
		Currency newCurrency = Currency.Create(value: "USD").Value;

		await _handler.HandleAsync(
			command: new ChangeUserBaseCurrencyCommand(UserId: user.Id, NewBaseCurrency: newCurrency),
			user: user,
			ct: CancellationToken.None
		);

		await _categoryTotalWriteRepository.Received(requiredNumberOfCalls: 1).RecalculateAllForUserAsync(
			userId: user.Id,
			baseCurrency: newCurrency,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldPublishUserBaseCurrencyChangedNotification()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;

		await _handler.HandleAsync(
			command: new ChangeUserBaseCurrencyCommand(UserId: user.Id, NewBaseCurrency: Currency.Create(value: "USD").Value),
			user: user,
			ct: CancellationToken.None
		);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(notification: Arg.Is<UserBaseCurrencyChangedNotification>(n =>
			n.UserId == user.Id &&
			n.OldBaseCurrency.Value == "RUB" &&
			n.NewBaseCurrency.Value == "USD"
		), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task HandleAsync_WithSameCurrency_ShouldNotChangeBaseCurrency()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new ChangeUserBaseCurrencyCommand(UserId: user.Id, NewBaseCurrency: Currency.Create(value: "RUB").Value),
			user: user,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userWriteRepository.DidNotReceive().ChangeBaseCurrencyAsync(
			userId: Arg.Any<Guid>(),
			newBaseCurrencyCode: Arg.Any<Currency>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithSameCurrency_ShouldNotRecalculateCategoryTotals()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;

		await _handler.HandleAsync(
			command: new ChangeUserBaseCurrencyCommand(UserId: user.Id, NewBaseCurrency: Currency.Create(value: "RUB").Value),
			user: user,
			ct: CancellationToken.None
		);

		await _categoryTotalWriteRepository.DidNotReceive().RecalculateAllForUserAsync(
			userId: Arg.Any<Guid>(),
			baseCurrency: Arg.Any<Currency>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithSameCurrency_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;

		await _handler.HandleAsync(
			command: new ChangeUserBaseCurrencyCommand(UserId: user.Id, NewBaseCurrency: Currency.Create(value: "RUB").Value),
			user: user,
			ct: CancellationToken.None
		);

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<INotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}