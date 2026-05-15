using FinanceTracker.Application.UseCases.Accounts.Queries.GetAccount;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class GetAccountHandlerTests
{
	private IAccountReadRepository _accountReadRepository = null!;
	private GetAccountHandler _handler = null!;

	private static readonly Guid UserId = Guid.CreateVersion7();

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountReadRepository = Substitute.For<IAccountReadRepository>();
		_handler = new GetAccountHandler(accountReadRepository: _accountReadRepository);
	}

	[Test]
	public async Task Handle_WhenAccountExists_ShouldReturnAccountDto()
	{
		AccountDto dto = AccountFactory.CreateAccountDto(userId: UserId);

		_accountReadRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			userId: UserId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: dto);

		AccountDto? result = await _handler.Handle(
			query: new GetAccountQuery(AccountId: dto.Id, UserId: UserId),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: dto.Id);
	}

	[Test]
	public async Task Handle_WhenAccountNotFound_ShouldReturnNull()
	{
		_accountReadRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<AccountDto?>(null));

		AccountDto? result = await _handler.Handle(
			query: new GetAccountQuery(AccountId: Guid.CreateVersion7(), UserId: UserId),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task Handle_ShouldPassBothAccountIdAndUserIdToRepository()
	{
		Guid accountId = Guid.CreateVersion7();

		_accountReadRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<AccountDto?>(null));

		await _handler.Handle(
			query: new GetAccountQuery(AccountId: accountId, UserId: UserId),
			ct: CancellationToken.None
		);

		await _accountReadRepository.Received(requiredNumberOfCalls: 1).GetByIdAsync(
			accountId: accountId,
			userId: UserId,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenAccountBelongsToDifferentUser_ShouldReturnNull()
	{
		_accountReadRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<AccountDto?>(null));

		AccountDto? result = await _handler.Handle(
			query: new GetAccountQuery(AccountId: Guid.CreateVersion7(), UserId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNull();
	}
}