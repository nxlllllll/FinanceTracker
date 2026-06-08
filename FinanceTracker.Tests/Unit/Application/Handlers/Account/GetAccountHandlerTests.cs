using FinanceTracker.Application.UseCases.Account.Queries.GetAccount;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
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
	public async Task Handle_WhenAccountExists_ShouldReturnSuccess()
	{
		AccountReadModel model = AccountFactory.CreateReadModel();
		GetAccountQuery query = new GetAccountQuery(
			AccountId: model.Id,
			UserId: model.UserId
		);

		_accountReadRepository
			.GetByIdAsync(accountId: model.Id, userId: model.UserId, ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: model);

		Result<AccountReadModel, DomainException> result = await _handler.Handle(
			query: query,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEqualTo(expected: model);
	}

	[Test]
	public async Task Handle_WhenAccountNotFound_ShouldReturnNotFound()
	{
		Guid accountId = Guid.CreateVersion7();
		GetAccountQuery query = new GetAccountQuery(
			AccountId: accountId,
			UserId: Guid.CreateVersion7()
		);

		_accountReadRepository
			.GetByIdAsync(accountId: accountId, userId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: (AccountReadModel?)null);

		Result<AccountReadModel, DomainException> result = await _handler.Handle(
			query: query,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}
}