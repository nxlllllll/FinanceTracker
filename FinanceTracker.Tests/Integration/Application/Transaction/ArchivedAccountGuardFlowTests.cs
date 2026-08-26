using FinanceTracker.Application.UseCases.Account.Commands.ArchiveAccount;
using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Application.UseCases.Account.Commands.UnarchiveAccount;
using FinanceTracker.Application.UseCases.Transaction.Commands.CancelTransaction;
using FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionCategory;
using FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionDescription;
using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transaction.Commands.ExcludeTransaction;
using FinanceTracker.Application.UseCases.Transaction.Commands.IncludeTransaction;
using FinanceTracker.Application.UseCases.Transaction.Queries.GetTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Account;
using FinanceTracker.Core.ReadModels.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context.Account;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;

namespace FinanceTracker.Tests.Integration.Application.Transaction;

/// <summary>
/// Archiving an account closes it to changes, not to its own history.
/// </summary>
public sealed class ArchivedAccountGuardFlowTests : MediatorFixture
{
	private UserBuilder _userBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;

	[Before(hookType: Test)]
	public async Task SetupDataAsync()
	{
		_userBuilder = new UserBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
		await new CurrencyBuilder(context: Context).CreateAsync(code: "RUB");
	}

	private async Task<Guid> CreateAccountAsync(Guid userId, decimal balance)
	{
		Result<Guid, AppException> result = await Mediator.Send(request: new CreateAccountCommand(
			UserId: userId,
			Name: Name.Create(value: "Основной счёт").Value,
			Type: AccountType.Checking,
			Currency: Currency.Create(value: "RUB").Value,
			InitialBalance: balance
		)
		{ IdempotencyKey = Guid.CreateVersion7() });

		Guid accountId = result.Value!;

		await Context.Accounts.AddAsync(new AccountEntity
		{
			Id = accountId,
			UserId = userId,
			Name = Name.Create(value: "Основной счёт").Value,
			AccountType = AccountType.Checking,
			Currency = Currency.Create(value: "RUB").Value,
			IsArchived = false,
			LastVersion = 1,
			CreatedAt = DateTimeOffset.UtcNow
		});
		await Context.AccountBalances.AddAsync(new AccountBalanceEntity
		{
			AccountId = accountId,
			Balance = balance,
			UpdatedAt = DateTimeOffset.UtcNow
		});
		await Context.SaveChangesAsync();

		return accountId;
	}

	private async Task<(Guid accountId, Guid categoryId, Guid transactionId, Guid userId)> CreateArchivedAccountWithTransactionAsync()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid accountId = await CreateAccountAsync(userId: userId, balance: 5_000m);

		Result<Guid, AppException> transaction = await Mediator.Send(request: new CreateTransactionCommand(
			AccountId: accountId,
			UserId: userId,
			CategoryId: categoryId,
			Amount: 5_000m,
			Currency: Currency.Create(value: "RUB").Value,
			Direction: DirectionType.Debit,
			Description: "Перед закрытием",
			OccurredAt: DateTimeOffset.UtcNow
		)
		{ IdempotencyKey = Guid.CreateVersion7() });

		Result<Guid, AppException> archived = await Mediator.Send(request: new ArchiveAccountCommand(
			UserId: userId,
			AccountId: accountId
		));

		if (archived.IsFailure)
			throw archived.Error!;

		return (accountId, categoryId, transaction.Value!, userId);
	}

	[Test]
	public async Task Exclude_OnAnArchivedAccount_ShouldFail()
	{
		(_, _, Guid transactionId, Guid userId) = await CreateArchivedAccountWithTransactionAsync();

		Result<Guid, AppException> result = await Mediator.Send(request: new ExcludeTransactionCommand(
			UserId: userId,
			TransactionId: transactionId
		));

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivedOperationException>();
	}

	[Test]
	public async Task Include_OnAnArchivedAccount_ShouldFail()
	{
		(_, _, Guid transactionId, Guid userId) = await CreateArchivedAccountWithTransactionAsync();

		Result<Guid, AppException> result = await Mediator.Send(request: new IncludeTransactionCommand(
			UserId: userId,
			TransactionId: transactionId
		));

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivedOperationException>();
	}

	[Test]
	public async Task ChangeDescription_OnAnArchivedAccount_ShouldFail()
	{
		(_, _, Guid transactionId, Guid userId) = await CreateArchivedAccountWithTransactionAsync();

		Result<Guid, AppException> result = await Mediator.Send(request: new ChangeTransactionDescriptionCommand(
			UserId: userId,
			TransactionId: transactionId,
			Description: "после закрытия счёта"
		));

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivedOperationException>();
	}

	[Test]
	public async Task ChangeCategory_OnAnArchivedAccount_ShouldFail()
	{
		(_, _, Guid transactionId, Guid userId) = await CreateArchivedAccountWithTransactionAsync();
		Guid anotherCategory = await _categoryBuilder.CreateAsync(userId: userId, name: "Другая");

		Result<Guid, AppException> result = await Mediator.Send(request: new ChangeTransactionCategoryCommand(
			UserId: userId,
			TransactionId: transactionId,
			CategoryId: anotherCategory
		));

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivedOperationException>()
			.Because(message: "The archived account has to be reported before the category rules are consulted: a caller cannot act on either answer, and naming the category first would send them looking for the wrong problem.");
	}

	[Test]
	public async Task Cancel_OnAnArchivedAccount_ShouldFail()
	{
		(_, _, Guid transactionId, Guid userId) = await CreateArchivedAccountWithTransactionAsync();

		Result<Guid, AppException> result = await Mediator.Send(request: new CancelTransactionCommand(
			UserId: userId,
			TransactionId: transactionId
		)
		{ IdempotencyKey = Guid.CreateVersion7() });

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivedOperationException>()
			.Because(message: "Cancellation already refused an archived account through Account.RevertTransaction. Asserting it here pins the loader as the place that answers first, so the message names the account rather than the movement it was trying to make.");
	}

	[Test]
	public async Task Read_OnAnArchivedAccount_ShouldStillWork()
	{
		(_, _, Guid transactionId, Guid userId) = await CreateArchivedAccountWithTransactionAsync();

		Result<TransactionReadModel, AppException> result = await Mediator.Send(request: new GetTransactionQuery(
			TransactionId: transactionId,
			UserId: userId
		));

		await Assert.That(value: result.IsSuccess).IsTrue()
			.Because(message: "Queries do not pass through the loader, and they must not: archiving closes an account to changes, not to its own records. A user who closed an account still owns its history.");
		await Assert.That(value: result.Value!.Description).IsEqualTo(expected: "Перед закрытием");
	}

	[Test]
	public async Task Unarchive_ShouldRestoreTheAbilityToChangeTransactions()
	{
		(Guid accountId, _, Guid transactionId, Guid userId) = await CreateArchivedAccountWithTransactionAsync();

		await Mediator.Send(request: new UnarchiveAccountCommand(UserId: userId, AccountId: accountId));

		Result<Guid, AppException> result = await Mediator.Send(request: new ExcludeTransactionCommand(
			UserId: userId,
			TransactionId: transactionId
		));

		await Assert.That(value: result.IsSuccess).IsTrue()
			.Because(message: "The guard reads the account's current state, so reopening it lifts the refusal. A rule that stuck to the transaction instead would leave records permanently frozen by an archiving that was undone.");
	}
}
