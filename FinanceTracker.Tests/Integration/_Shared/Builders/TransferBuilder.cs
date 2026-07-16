using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Repositories.Operation;
using FinanceTracker.Infrastructure.Database.Repositories.Transfer;

namespace FinanceTracker.Tests.Integration._Shared.Builders;

public sealed class TransferBuilder(FinanceTrackerContext context)
{
	private readonly TransferWriteRepository _writeRepository = new TransferWriteRepository(
		context: context,
		operationRepository: new OperationWriteRepository(context: context)
	);

	public async Task<Guid> CreateAsync(
		Guid userId,
		Guid fromAccountId,
		string currencyFrom,
		Guid toAccountId,
		string currencyTo,
		decimal amount = 1000m,
		decimal exchangeRate = 1m,
		RateStatus rateStatus = RateStatus.Exact,
		TransferStatus status = TransferStatus.PendingCredit,
		DateTimeOffset? occurredAt = null,
		DateTimeOffset? createdAt = null)
	{
		Result<Transfer, DomainException> transferResult = Core.Domains.Transfer.Transfer.Create(
			createdAt: createdAt ?? DateTimeOffset.UtcNow,
			userId: userId,
			fromAccountId: fromAccountId,
			toAccountId: toAccountId,
			amount: amount,
			currencyFrom: Core.ValueObjects.Currency.Create(value: currencyFrom).Value,
			currencyTo: Core.ValueObjects.Currency.Create(value: currencyTo).Value,
			exchangeRate: exchangeRate,
			rateStatus: rateStatus,
			description: null,
			occurredAt: occurredAt ?? DateTimeOffset.UtcNow
		);

		if (transferResult.IsFailure)
			throw new InvalidOperationException(message: $"TransferBuilder.CreateAsync failed: {transferResult.Error!.Message}");

		Transfer transfer = transferResult.Value!;

		if (status == TransferStatus.Completed)
			transfer.Complete();

		await _writeRepository.CreateAsync(transfer: transfer);
		await context.SaveChangesAsync();
		return transfer.Id;
	}
}
