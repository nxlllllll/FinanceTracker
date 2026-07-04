using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

/// <summary>
/// Replaying an account's full event history must produce the exact
/// same balance as applying the same events incrementally as deltas
/// </summary>
public sealed class AccountReplayProjectionConsistencyTests
{
	private const int SequenceCount = 200;
	private const int OperationsPerSequence = 20;
	private const int Seed = 20260704;

	private enum OperationType
	{
		Debit,
		Credit
	}

	private readonly record struct Operation(OperationType Type, decimal Amount, decimal ExchangeRate);

	[Test]
	public async Task ReplayedBalance_ShouldMatchIndependentlyAccumulatedProjectionBalance()
	{
		Random random = new Random(Seed: Seed);

		for (int sequenceIndex = 0; sequenceIndex < SequenceCount; sequenceIndex++)
		{
			const decimal initialBalance = 100_000m;

			Account account = AccountFactory.Create(balance: initialBalance).Value!;
			account.ClearEvents();

			decimal simulatedProjectionBalance = initialBalance;
			List<string> appliedOperationsLog = [];

			for (int operationIndex = 0; operationIndex < OperationsPerSequence; operationIndex++)
			{
				Operation operation = GenerateRandomOperation(random: random);

				Result<FinanceTracker.Core.Results.Unit, DomainException> result = ApplyToAggregate(account: account, operation: operation);

				if (result.IsFailure)
					continue; // random draw exceeded the balance — skip on both sides so they stay comparable

				decimal convertedDelta = Money.ConvertedAmount(amount: operation.Amount, rate: operation.ExchangeRate);
				simulatedProjectionBalance += operation.Type == OperationType.Debit ? -convertedDelta : convertedDelta;
				appliedOperationsLog.Add(item: $"{operation.Type}({operation.Amount}@{operation.ExchangeRate})");
			}

			await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: simulatedProjectionBalance)
				.Because(message: $"Sequence #{sequenceIndex} (seed {Seed}) diverged. Applied operations: {String.Join(separator: " | ", values: appliedOperationsLog)}");
		}
	}

	private static Result<FinanceTracker.Core.Results.Unit, DomainException> ApplyToAggregate(Account account, Operation operation) => operation.Type switch
	{
		OperationType.Debit => account.Debit(
			occurredAt: FakeDateProvider.Default.UtcNow,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: operation.Amount,
			exchangeRate: operation.ExchangeRate,
			description: null
		),
		_ => account.Credit(
			occurredAt: FakeDateProvider.Default.UtcNow,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: operation.Amount,
			exchangeRate: operation.ExchangeRate,
			description: null
		)
	};

	private static Operation GenerateRandomOperation(Random random)
	{
		OperationType type = random.Next(maxValue: 2) == 0 ? OperationType.Debit : OperationType.Credit;
		decimal amount = Math.Round(d: (decimal)(random.NextDouble() * 5000d) + 0.01m, decimals: 2);
		decimal rate = Math.Round(d: (decimal)(random.NextDouble() * 4d) + 0.01m, decimals: 6);

		return new Operation(Type: type, Amount: amount, ExchangeRate: rate);
	}
}
