using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Transactions;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.Repositories.Transaction;

namespace FinanceTracker.Tests.Integration.Infrastructure.Transaction;

public sealed class TransactionRepositoryTests : DatabaseFixture
{
	private TransactionRepository _transactionRepository = null!;

	[Before(hookType: Test)]
	public void SetupRepository()
	{
		_transactionRepository = new TransactionRepository(eventStore: new PostgresEventStore(
			context: Context,
			eventTypeRegistry: new EventTypeRegistry(assembly: typeof(IEvent).Assembly)
		));
	}

	private static FinanceTracker.Core.Domains.Transactions.Transaction CreateTransaction()
	{
		return FinanceTracker.Core.Domains.Transactions.Transaction.Create(
			accountId: Guid.NewGuid(),
			userId: Guid.NewGuid(),
			categoryId: Guid.NewGuid(),
			amount: 1000m,
			direction: DirectionType.Debit,
			exchangeRate: 1m,
			description: "Обед",
			occurredAt: DateTime.UtcNow
		);
	}
	
	[Test]
	public async Task GetByIdAsync_WithNonExistentTransaction_ShouldReturnNull()
	{
		FinanceTracker.Core.Domains.Transactions.Transaction? result = await _transactionRepository.GetByIdAsync(transactionId: Guid.NewGuid());

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task SaveAsync_ThenGetByIdAsync_ShouldRestoreTransaction()
	{
		FinanceTracker.Core.Domains.Transactions.Transaction transaction = CreateTransaction();
		await _transactionRepository.SaveAsync(transaction: transaction);

		FinanceTracker.Core.Domains.Transactions.Transaction? loaded = await _transactionRepository.GetByIdAsync(transactionId: transaction.Id);

		await Assert.That(value: loaded).IsNotNull();
		await Assert.That(value: loaded!.Id).IsEqualTo(expected: transaction.Id);
		await Assert.That(value: loaded.Amount).IsEqualTo(expected: 1000m);
		await Assert.That(value: loaded.Direction).IsEqualTo(expected: DirectionType.Debit);
		await Assert.That(value: loaded.IsExcluded).IsFalse();
		await Assert.That(value: loaded.Description).IsEqualTo(expected: "Обед");
	}

	[Test]
	public async Task SaveAsync_ShouldClearEventsAfterSave()
	{
		FinanceTracker.Core.Domains.Transactions.Transaction transaction = CreateTransaction();
		await _transactionRepository.SaveAsync(transaction: transaction);

		await Assert.That(value: transaction.Events.Count).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task SaveAsync_WithMultipleEvents_ShouldRestoreCorrectState()
	{
		FinanceTracker.Core.Domains.Transactions.Transaction transaction = CreateTransaction();
		await _transactionRepository.SaveAsync(transaction: transaction);

		FinanceTracker.Core.Domains.Transactions.Transaction? loaded = await _transactionRepository.GetByIdAsync(transactionId: transaction.Id);
		loaded!.Exclude();
		await _transactionRepository.SaveAsync(transaction: loaded);

		FinanceTracker.Core.Domains.Transactions.Transaction? final = await _transactionRepository.GetByIdAsync(transactionId: transaction.Id);

		await Assert.That(value: final).IsNotNull();
		await Assert.That(value: final.IsExcluded).IsTrue();
		await Assert.That(value: final.Version).IsEqualTo(expected: 2);
	}
}