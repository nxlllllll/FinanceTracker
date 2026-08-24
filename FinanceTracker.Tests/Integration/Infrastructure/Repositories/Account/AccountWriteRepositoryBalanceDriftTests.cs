using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Account;

public sealed class AccountWriteRepositoryBalanceDriftTests : DatabaseFixture
{
	private const int SequenceCount = 15;
	private const int OperationsPerSequence = 20;
	private const int Seed = 20260711;

	private enum OperationKind
	{
		Debit,
		Credit,
		AdjustBalance,
		DebitTransfer,
		CreditTransfer,
		RefundTransfer,
		Rename,
		ArchiveAndUnarchive
	}

	private AccountWriteRepository _writeRepository = null!;
	private CurrencyBuilder _currencyBuilder = null!;
	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_writeRepository = new AccountWriteRepository(
			context: Context,
			dateProvider: FakeDateProvider.Default
		);
		_currencyBuilder = new CurrencyBuilder(context: Context);
		_userBuilder = new UserBuilder(context: Context);
	}

	[Test]
	public async Task ProjectedAccount_ShouldMatchAggregate_AfterRandomOperationSequences()
	{
		Random random = new Random(Seed: Seed);

		Core.ValueObjects.Currency currencyCode = await _currencyBuilder.CreateAsync();
		Guid userId = await _userBuilder.CreateAsync(currencyCode: currencyCode);

		for (int sequenceIndex = 0; sequenceIndex < SequenceCount; sequenceIndex++)
		{
			const decimal initialBalance = 100_000m;

			Core.Domains.Account.Account account = Core.Domains.Account.Account.Create(
				occurredAt: FakeDateProvider.Default.UtcNow,
				userId: userId,
				name: Name.Create(value: "Drift test account").Value,
				type: AccountType.Checking,
				currency: currencyCode,
				balance: initialBalance
			).Value!;

			await ApplyAndClearAsync(account: account);

			List<string> appliedOperationsLog = [];

			for (int operationIndex = 0; operationIndex < OperationsPerSequence; operationIndex++)
			{
				Result<Core.Results.Unit, DomainException> result = TryApplyRandomOperation(
					account: account,
					random: random,
					log: appliedOperationsLog
				);

				if (result.IsFailure)
					continue;

				await ApplyAndClearAsync(account: account);
			}

			decimal projectedBalance = await Context.AccountBalances
				.Where(predicate: b => b.AccountId == account.Id)
				.Select(selector: b => b.Balance)
				.FirstAsync();

			await Assert.That(value: projectedBalance).IsEqualTo(expected: account.Balance.Amount)
				.Because(message: $"Sequence #{sequenceIndex} (seed {Seed}) diverged. Applied operations: {String.Join(separator: " | ", values: appliedOperationsLog)}");

			int projectedVersion = await Context.Accounts
				.Where(predicate: a => a.Id == account.Id)
				.Select(selector: a => a.LastVersion)
				.FirstAsync();

			await Assert.That(value: projectedVersion).IsEqualTo(expected: account.Version)
				.Because(message: $"Sequence #{sequenceIndex} (seed {Seed}): accounts.last_version is the single counter behind both the ETag and the If-Match check, so it has to track every event the aggregate raised, not just the ones its own projection handles. Applied operations: {String.Join(separator: " | ", values: appliedOperationsLog)}");
		}
	}

	/// <summary>
	/// Dispatches every event the aggregate has raised since the last call to the matching
	/// <see cref="AccountWriteRepository"/> method — the exact object the aggregate produced,
	/// not a value reconstructed from the operation's inputs — then clears them, mirroring how
	/// a real command handler hands events off to the repository after <c>SaveAsync</c>.
	/// </summary>
	private async Task ApplyAndClearAsync(Core.Domains.Account.Account account)
	{
		foreach (IEvent @event in account.Events)
		{
			await (@event switch
			{
				AccountCreated e => _writeRepository.CreateAsync(@event: e),
				AccountDebited e => _writeRepository.DebitAsync(@event: e),
				AccountCredited e => _writeRepository.CreditAsync(@event: e),
				AccountBalanceAdjusted e => _writeRepository.AdjustBalanceAsync(@event: e),
				AccountTransferDebited e => _writeRepository.TransferDebitAsync(@event: e),
				AccountTransferCredited e => _writeRepository.TransferCreditAsync(@event: e),
				AccountTransferRefunded e => _writeRepository.RefundTransferAsync(@event: e),
				AccountRenamed e => _writeRepository.RenameAsync(@event: e),
				AccountArchived e => _writeRepository.ArchiveAsync(@event: e),
				AccountUnarchived e => _writeRepository.UnarchiveAsync(@event: e),
				_ => throw new InvalidOperationException(message: $"Unexpected event type in balance-drift test: {@event.GetType().Name}")
			});
		}

		await Context.SaveChangesAsync();
		account.ClearEvents();
	}

	private static Result<Core.Results.Unit, DomainException> TryApplyRandomOperation(Core.Domains.Account.Account account, Random random, List<string> log)
	{
		OperationKind kind = (OperationKind)random.Next(maxValue: Enum.GetValues<OperationKind>().Length);
		decimal amount = RandomAmount(random: random);
		decimal rate = RandomRate(random: random);
		DateTimeOffset now = FakeDateProvider.Default.UtcNow;

		Result<Core.Results.Unit, DomainException> result = kind switch
		{
			OperationKind.Debit => account.Debit(
				occurredAt: now,
				transactionId: Guid.CreateVersion7(),
				categoryId: Guid.CreateVersion7(),
				amount: amount,
				exchangeRate: rate,
				description: null
			),
			OperationKind.Credit => account.Credit(
				occurredAt: now,
				transactionId: Guid.CreateVersion7(),
				categoryId: Guid.CreateVersion7(),
				amount: amount,
				exchangeRate: rate,
				description: null
			),
			OperationKind.AdjustBalance => account.AdjustBalance(
				occurredAt: now,
				sourceId: Guid.CreateVersion7(),
				sourceType: AggregateTypeNames.Transaction,
				direction: random.Next(maxValue: 2) == 0 ? DirectionType.Debit : DirectionType.Credit,
				oldRate: RandomRate(random: random),
				newRate: rate,
				amount: amount
			),
			OperationKind.DebitTransfer => account.DebitTransfer(
				occurredAt: now,
				transferId: Guid.CreateVersion7(),
				toAccountId: Guid.CreateVersion7(),
				amount: amount,
				forexRate: rate,
				description: null
			),
			OperationKind.CreditTransfer => account.CreditTransfer(
				occurredAt: now,
				transferId: Guid.CreateVersion7(),
				fromAccountId: Guid.CreateVersion7(),
				amount: amount,
				exchangeRate: rate,
				description: null
			),
			OperationKind.RefundTransfer => account.RefundTransfer(
				occurredAt: now,
				transferId: Guid.CreateVersion7(),
				amount: amount,
				description: null
			),
			OperationKind.Rename => account.Rename(
				occurredAt: now,
				newName: Name.Create(value: $"Drift test account #{log.Count + 1}").Value
			),
			OperationKind.ArchiveAndUnarchive => ArchiveAndUnarchive(account: account, occurredAt: now),
			_ => throw new InvalidOperationException(message: $"Unhandled operation kind: {kind}")
		};

		if (result.IsSuccess)
			log.Add(item: $"{kind}({amount}@{rate})");

		return result;
	}

	private static Result<Core.Results.Unit, DomainException> ArchiveAndUnarchive(Core.Domains.Account.Account account, DateTimeOffset occurredAt)
	{
		Result<Core.Results.Unit, DomainException> archived = account.Archive(occurredAt: occurredAt);
		if (archived.IsFailure)
			return archived;

		return account.Unarchive(occurredAt: occurredAt);
	}

	private static decimal RandomAmount(Random random)
		=> Math.Round(d: (decimal)(random.NextDouble() * 5000d) + 0.01m, decimals: 2);

	private static decimal RandomRate(Random random)
		=> Math.Round(d: (decimal)(random.NextDouble() * 4d) + 0.01m, decimals: 6);
}
