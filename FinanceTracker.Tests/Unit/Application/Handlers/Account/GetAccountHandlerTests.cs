using FinanceTracker.Application.Accounts.Queries.GetAccount;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class GetAccountHandlerTests
{
	private IAccountReadRepository _accountReadRepository = null!;
	private GetAccountHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountReadRepository = Substitute.For<IAccountReadRepository>();
		_handler = new GetAccountHandler(accountReadRepository: _accountReadRepository);
	}
	
	[Test]
	public async Task Handle_WhenAccountExists_ShouldReturnAccountDto()
	{
		AccountDto dto = AccountFactory.CreateAccountDto();

		_accountReadRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: dto);

		GetAccountQuery query = new GetAccountQuery(AccountId: dto.Id);
		AccountDto? result = await _handler.Handle(query: query, ct: CancellationToken.None);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: dto.Id);
	}


	[Test]
	public async Task Handle_WhenAccountNotFound_ShouldReturnNull()
	{
		_accountReadRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<AccountDto?>(null));

		GetAccountQuery query = new GetAccountQuery(AccountId: Guid.NewGuid());
		AccountDto? result = await _handler.Handle(query: query, ct: CancellationToken.None);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task Handle_ShouldCallReadRepositoryOnce()
	{
		_accountReadRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<AccountDto?>(result: null));

		GetAccountQuery query = new GetAccountQuery(AccountId: Guid.NewGuid());
		await _handler.Handle(query: query, ct: CancellationToken.None);

		await _accountReadRepository.Received(requiredNumberOfCalls: 1).GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}