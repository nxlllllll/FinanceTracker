using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Repositories.Transfer;

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
		decimal amount = 1000m,
		decimal exchangeRate = 1m,
		DateTimeOffset? occurredAt = null)
	{
		Result<Core.Domains.Transfer.Transfer, DomainException> transferResult = Core.Domains.Transfer.Transfer.Create(
			userId: userId,
			fromAccountId: fromAccountId,
			toAccountId: toAccountId,
			amount: amount,
			currencyFrom: Core.ValueObjects.Currency.Create(value: currencyFrom).Value,
			currencyTo: Core.ValueObjects.Currency.Create(value: currencyTo).Value,
			exchangeRate: exchangeRate,
			isRatePending: false,
			description: null,
			occurredAt: occurredAt ?? DateTimeOffset.UtcNow
		);

		if (transferResult.IsFailure)
			throw new InvalidOperationException(message: $"TransferBuilder.CreateAsync failed: {transferResult.Error!.Message}");

		Core.Domains.Transfer.Transfer transfer = transferResult.Value!;

		await _writeRepository.CreateAsync(transfer: transfer);
		await context.SaveChangesAsync();
		return transfer.Id;
	}
}