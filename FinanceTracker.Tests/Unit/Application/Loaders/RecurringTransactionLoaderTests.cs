using FinanceTracker.Application.UseCases.RecurringTransaction.Authorization;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ActivateRecurringTransaction;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.CreateRecurringTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Loaders;

public sealed class RecurringTransactionLoaderTests
{
	private IRecurringTransactionRepository _readRepository = null!;
	private IAccountReadRepository _accountReadRepository = null!;
	private RecurringTransactionLoader _loader = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_readRepository = Substitute.For<IRecurringTransactionRepository>();
		_accountReadRepository = Substitute.For<IAccountReadRepository>();
		_loader = new RecurringTransactionLoader(
			recurringTransactionRepository: _readRepository,
			accountRepository: _accountReadRepository
		);
	}

	[Test]
	public async Task LoadAsync_WhenNotFound_ShouldThrowNotFoundException()
	{
		_readRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<RecurringTransaction?>(result: null));

		Result<RecurringTransaction, DomainException> result = await _loader.LoadAsync(
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

		Result<RecurringTransaction, DomainException> result = await _loader.LoadAsync(
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

		Result<RecurringTransaction, DomainException> result = await _loader.LoadAsync(
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

		_accountReadRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<AccountReadModel?>(result: null));

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await _loader.LoadAsync(request: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_CreateCommand_WhenAccountBelongsToAnotherUser_ShouldReturnNotFoundException()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create();
		AccountReadModel account = new AccountReadModel(
			Id: command.AccountId,
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Main").Value!,
			Type: AccountType.Checking,
			Balance: Money.Reconstitute(amount: 1000m, currency: Currency.Reconstitute(value: "RUB")),
			IsArchived: false,
			CreatedAt: DateTimeOffset.UtcNow
		);

		_accountReadRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await _loader.LoadAsync(request: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_CreateCommand_WhenOwner_ShouldReturnSuccess()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create();
		AccountReadModel account = new AccountReadModel(
			Id: command.AccountId,
			UserId: command.UserId,
			Name: Name.Create(value: "Main").Value!,
			Type: AccountType.Checking,
			Balance: Money.Reconstitute(amount: 1000m, currency: Currency.Reconstitute(value: "RUB")),
			IsArchived: false,
			CreatedAt: DateTimeOffset.UtcNow
		);

		_accountReadRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await _loader.LoadAsync(request: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}
}
