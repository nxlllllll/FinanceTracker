using FinanceTracker.Application.UseCases.Users.Queries.GetTotalBalance;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class GetTotalBalanceHandlerTests
{
    private IUserReadRepository _userReadRepository = null!;
    private IDateProvider _dateProvider = null!;
    private GetTotalBalanceHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _userReadRepository = Substitute.For<IUserReadRepository>();
        _dateProvider = Substitute.For<IDateProvider>();
        _dateProvider.UtcNow.Returns(returnThis: FakeDateProvider.Default.UtcNow);

        _handler = new GetTotalBalanceHandler(
            userReadRepository: _userReadRepository,
            dateProvider: _dateProvider
        );
    }

    [Test]
    public async Task Handle_WithSingleAccountInBaseCurrency_ShouldReturnBalance()
    {
        FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;

        _userReadRepository.GetByIdAsync(userId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>())
            .Returns(returnThis: user);
        _userReadRepository.GetTotalBalanceAsync(
            userId: Arg.Any<Guid>(),
            baseCurrency: Arg.Any<Currency>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: 5000m);

        TotalBalanceDto result = await _handler.Handle(
            query: new GetTotalBalanceQuery(UserId: user.Id),
            ct: CancellationToken.None
        );

        await Assert.That(value: result.Balance).IsEqualTo(expected: 5000m);
        await Assert.That(value: result.Currency.Value).IsEqualTo(expected: "RUB");
    }

    [Test]
    public async Task Handle_ShouldPassCorrectCurrencyAndDate()
    {
        FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;

        _userReadRepository.GetByIdAsync(userId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>())
            .Returns(returnThis: user);
        _userReadRepository.GetTotalBalanceAsync(
            userId: Arg.Any<Guid>(),
            baseCurrency: Arg.Any<Currency>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: 0m);

        await _handler.Handle(
            query: new GetTotalBalanceQuery(UserId: user.Id),
            ct: CancellationToken.None
        );

        await _userReadRepository.Received(requiredNumberOfCalls: 1).GetTotalBalanceAsync(
            userId: user.Id,
            baseCurrency: user.BaseCurrency,
            date: DateOnly.FromDateTime(dateTime: FakeDateProvider.Default.UtcNow),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenUserNotFound_ShouldThrow()
    {
        _userReadRepository.GetByIdAsync(userId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>())
            .Returns(returnThis: (FinanceTracker.Core.Domains.User.User?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            action: async () => await _handler.Handle(
                query: new GetTotalBalanceQuery(UserId: Guid.CreateVersion7()),
                ct: CancellationToken.None
            )
        );
    }
}