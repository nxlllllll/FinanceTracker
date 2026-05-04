using FinanceTracker.Infrastructure.Database;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Repositories.Transfers;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

public sealed class TransferBuilder(FinanceTrackerContext context)
{
	private readonly TransferWriteRepository _writeRepository = new TransferWriteRepository(context: context);

	public async Task<Guid> CreateAsync(
		Guid userId,
		Guid fromAccountId,
		string currencyFrom,
		Guid toAccountId,
		string currencyTo,
		decimal amountFrom = 1000m,
		decimal amountTo = 1000m,
		DateTime? occurredAt = null)
	{
		Core.Domains.Transfer.Transfer transfer = Core.Domains.Transfer.Transfer.Create(
			userId: userId,
			fromAccountId: fromAccountId,
			toAccountId: toAccountId,
			amountFrom: amountFrom,
			currencyFrom: currencyFrom,
			amountTo: amountTo,
			currencyTo: currencyTo,
			exchangeRate: 1m,
			isRatePending: false,
			description: null,
			occurredAt: occurredAt ?? DateTime.UtcNow
		);

		await _writeRepository.CreateAsync(transfer: transfer);
		return transfer.Id;
	}
}