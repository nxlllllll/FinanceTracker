using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
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
		DateTimeOffset? occurredAt = null)
	{
		Result<Core.Domains.Transfer.Transfer, DomainException> transferResult = Core.Domains.Transfer.Transfer.Create(
			userId: userId,
			fromAccountId: fromAccountId,
			toAccountId: toAccountId,
			amountFrom: amountFrom,
			currencyFrom: Core.ValueObjects.Currency.Create(value: currencyFrom).Value,
			amountTo: amountTo,
			currencyTo: Core.ValueObjects.Currency.Create(value: currencyTo).Value,
			exchangeRate: 1m,
			isRatePending: false,
			description: null,
			occurredAt: occurredAt ?? DateTimeOffset.UtcNow
		);
		
		Core.Domains.Transfer.Transfer transfer = transferResult.Value!;
		
		await _writeRepository.CreateAsync(transfer: transfer);
		return transfer.Id;
	}
}
