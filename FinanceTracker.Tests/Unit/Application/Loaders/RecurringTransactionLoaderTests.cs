using FinanceTracker.Application.UseCases.RecurringTransaction.Authorization;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ActivateRecurringTransaction;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.CreateRecurringTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Loaders;

public sealed class RecurringTransactionLoaderTests
{
	private IRecurringTransactionRepository _readRepository = null!;
	private IAccountRepository _accountRepository = null!;
	private RecurringTransactionLoader _loader = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_readRepository = Substitute.For<IRecurringTransactionRepository>();
		_accountRepository = Substitute.For<IAccountRepository>();
		_loader = new RecurringTransactionLoader(
			recurringTransactionRepository: _readRepository,
			accountRepository: _accountRepository
		);
	}

	[Test]
	public async Task LoadAsync_WhenNotFound_ShouldThrowNotFoundException()
	{
		_readRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<RecurringTransaction?>(result: null));

		Result<RecurringTransaction, AppException> result = await _loader.LoadAsync(
			request: new ActivateRecurringTransactionCommand(UserId: Guid.CreateVersion7(), RecurringTransactionId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenBelongsToAnotherUser_ShouldThrowNotFoundException()
	{
		RecurringTransaction recurringTransaction = RecurringTransactionFactory.Create().Value!;
		_readRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: recurringTransaction);

		Result<RecurringTransaction, AppException> result = await _loader.LoadAsync(
			request: new ActivateRecurringTransactionCommand(UserId: Guid.CreateVersion7(), RecurringTransactionId: recurringTransaction.Id),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenOwner_ShouldReturnDto()
	{
		RecurringTransaction recurringTransaction = RecurringTransactionFactory.Create().Value!;
		_readRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: recurringTransaction);

		Result<RecurringTransaction, AppException> result = await _loader.LoadAsync(
			request: new ActivateRecurringTransactionCommand(UserId: recurringTransaction.UserId, RecurringTransactionId: recurringTransaction.Id),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.Id).IsEqualTo(expected: recurringTransaction.Id);
	}

	[Test]
	public async Task LoadAsync_CreateCommand_WhenAccountNotFound_ShouldReturnNotFoundException()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create();

		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<Account?>(result: null));

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _loader.LoadAsync(request: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_CreateCommand_WhenAccountBelongsToAnotherUser_ShouldReturnNotFoundException()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create();
		Account account = AccountFactory.CreateWithArchivation(userId: Guid.CreateVersion7());

		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _loader.LoadAsync(request: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_CreateCommand_WhenOwner_ShouldReturnSuccess()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create();
		Account account = AccountFactory.CreateWithArchivation(userId: command.UserId);

		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _loader.LoadAsync(request: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}
}
