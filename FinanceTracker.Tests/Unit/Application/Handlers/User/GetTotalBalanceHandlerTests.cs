using FinanceTracker.Application.UseCases.User.Queries.GetTotalBalance;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class GetTotalBalanceHandlerTests
{
	private IUserQueryRepository _userQueryRepository = null!;
	private GetTotalBalanceHandler _handler = null!;

	private static UserReadModel CreateUserReadModel(string currency = "RUB") => new UserReadModel(
		Id: Guid.CreateVersion7(),
		Email: Email.Create(value: "test@test.com").Value!,
		BaseCurrency: Currency.Create(value: currency).Value,
		CreatedAt: FakeDateProvider.Default.UtcNow
	);

	[Before(hookType: Test)]
	public void Setup()
	{
		_userQueryRepository = Substitute.For<IUserQueryRepository>();

		_handler = new GetTotalBalanceHandler(
			userQueryRepository: _userQueryRepository,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task Handle_WithSingleAccountInBaseCurrency_ShouldReturnBalance()
	{
		UserReadModel user = CreateUserReadModel(currency: "RUB");

		_userQueryRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: user);
		_userQueryRepository.GetTotalBalanceAsync(
			userId: Arg.Any<Guid>(),
			baseCurrency: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 5000m);

		Money result = await _handler.Handle(
			query: new GetTotalBalanceQuery(UserId: user.Id),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Amount).IsEqualTo(expected: 5000m);
		await Assert.That(value: result.Currency.Value).IsEqualTo(expected: "RUB");
	}

	[Test]
	public async Task Handle_ShouldPassCorrectCurrencyAndDate()
	{
		UserReadModel user = CreateUserReadModel(currency: "RUB");

		_userQueryRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: user);
		_userQueryRepository.GetTotalBalanceAsync(
			userId: Arg.Any<Guid>(),
			baseCurrency: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 0m);

		await _handler.Handle(
			query: new GetTotalBalanceQuery(UserId: user.Id),
			ct: CancellationToken.None
		);

		await _userQueryRepository.Received(requiredNumberOfCalls: 1).GetTotalBalanceAsync(
			userId: user.Id,
			baseCurrency: user.BaseCurrency,
			date: DateOnly.FromDateTime(dateTime: FakeDateProvider.Default.UtcNow.UtcDateTime),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenUserNotFound_ShouldThrow()
	{
		_userQueryRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (UserReadModel?)null);

		await Assert.ThrowsAsync<NotFoundException>(
			action: async () => await _handler.Handle(
				query: new GetTotalBalanceQuery(UserId: Guid.CreateVersion7()),
				ct: CancellationToken.None
			)
		);
	}
}
