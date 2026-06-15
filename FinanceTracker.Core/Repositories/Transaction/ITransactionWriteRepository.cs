namespace FinanceTracker.Core.Repositories.Transaction;

public interface ITransactionWriteRepository
{
	Task CreateAsync(
		Domains.Transaction.Transaction transaction,
		CancellationToken ct = default
	);

	Task ChangeCategoryAsync(
		Guid transactionId,
		Guid userId,
		Guid categoryId,
		int expectedVersion,
		CancellationToken ct = default
	);

	Task ChangeDescriptionAsync(
		Guid transactionId,
		string? description,
		int expectedVersion,
		CancellationToken ct = default
	);

	Task ExcludeAsync(
		Guid transactionId,
		Guid userId,
		int expectedVersion,
		CancellationToken ct = default
	);

	Task IncludeAsync(
		Guid transactionId,
		Guid userId,
		int expectedVersion,
		CancellationToken ct = default
	);

	Task UpdateRateAsync(
		Guid transactionId,
		decimal newRate,
		int expectedVersion,
		CancellationToken ct = default
	);
}