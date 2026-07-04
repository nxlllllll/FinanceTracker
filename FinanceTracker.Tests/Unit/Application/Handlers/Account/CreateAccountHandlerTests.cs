using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class CreateAccountHandlerTests
{
	private IAccountRepository _accountRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private CreateAccountHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());

		_handler = new CreateAccountHandler(
			accountRepository: _accountRepository,
			unitOfWork: _unitOfWork,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldReturnAccountId()
	{
		CreateAccountCommand command = CreateAccountCommandFactory.Create();

		Result<Guid, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsNotEqualTo(notExpected: Guid.Empty);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldSaveAccount()
	{
		CreateAccountCommand command = CreateAccountCommandFactory.Create();

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(account =>
				account.Name == command.Name &&
				account.UserId == command.UserId &&
				account.Type == command.Type &&
				account.Currency == command.Currency
			), ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WithNegativeBalance_ShouldReturnFailure()
	{
		CreateAccountCommand command = CreateAccountCommandFactory.Create(initialBalance: -100);

		Result<Guid, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
