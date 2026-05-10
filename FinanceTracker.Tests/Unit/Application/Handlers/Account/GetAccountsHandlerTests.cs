using FinanceTracker.Application.UseCases.Users.Queries.GetAccounts;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class GetAccountsHandlerTests
{
    private IAccountReadRepository _accountReadRepository = null!;
    private GetAccountsHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _accountReadRepository = Substitute.For<IAccountReadRepository>();
        _handler = new GetAccountsHandler(accountReadRepository: _accountReadRepository);
    }

    [Test]
    public async Task Handle_ShouldReturnAllAccounts()
    {
        Guid userId = Guid.CreateVersion7();
        IReadOnlyList<AccountDto> accounts = [AccountFactory.CreateAccountDto(), AccountFactory.CreateAccountDto()];

        _accountReadRepository.GetAllAsync(
            userId: Arg.Any<Guid>(),
            isArchived: Arg.Any<bool?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: accounts);

        GetAccountsQuery query = new GetAccountsQuery(UserId: userId);
        IReadOnlyList<AccountDto> result = await _handler.Handle(query: query, ct: CancellationToken.None);

        await Assert.That(value: result.Count).IsEqualTo(expected: 2);
    }

    [Test]
    public async Task Handle_ShouldPassIsArchivedFilterToRepository()
    {
        _accountReadRepository.GetAllAsync(
            userId: Arg.Any<Guid>(),
            isArchived: Arg.Any<bool?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        GetAccountsQuery query = new GetAccountsQuery(UserId: Guid.CreateVersion7(), IsArchived: false);

        await _handler.Handle(query: query, ct: CancellationToken.None);

        await _accountReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
            userId: Arg.Any<Guid>(),
            isArchived: false,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WithNullIsArchived_ShouldPassNullToRepository()
    {
        _accountReadRepository.GetAllAsync(
            userId: Arg.Any<Guid>(),
            isArchived: Arg.Any<bool?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        GetAccountsQuery query = new GetAccountsQuery(UserId: Guid.CreateVersion7(), IsArchived: null);

        await _handler.Handle(query: query, ct: CancellationToken.None);

        await _accountReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
            userId: Arg.Any<Guid>(),
            isArchived: null,
            ct: Arg.Any<CancellationToken>()
        );
    }
}