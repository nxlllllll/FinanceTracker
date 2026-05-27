using FinanceTracker.Application.UseCases.User.Queries.GetAccounts;
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
		IReadOnlyList<FinanceTracker.Core.Domains.Account.Account> accounts =
		[
			AccountFactory.CreateForReadModel(),
			AccountFactory.CreateForReadModel()
		];

		_accountReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			isArchived: Arg.Any<bool?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: accounts);

		IReadOnlyList<FinanceTracker.Core.Domains.Account.Account> result = await _handler.Handle(
			query: new GetAccountsQuery(UserId: userId),
			ct: CancellationToken.None
		);

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

		await _handler.Handle(
			query: new GetAccountsQuery(UserId: Guid.CreateVersion7(), IsArchived: false),
			ct: CancellationToken.None
		);

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

		await _handler.Handle(
			query: new GetAccountsQuery(UserId: Guid.CreateVersion7(), IsArchived: null),
			ct: CancellationToken.None
		);

		await _accountReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
			userId: Arg.Any<Guid>(),
			isArchived: null,
			ct: Arg.Any<CancellationToken>()
		);
	}
}