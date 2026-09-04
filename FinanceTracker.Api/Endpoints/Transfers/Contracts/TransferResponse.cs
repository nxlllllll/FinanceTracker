using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.ReadModels.Transfer;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Api.Endpoints.Transfers.Contracts;

/// <summary>
/// HTTP projection of <see cref="TransferReadModel"/>.
/// </summary>
public sealed record TransferResponse(
	Guid Id,
	Guid FromAccountId,
	Guid ToAccountId,
	Money AmountFrom,
	Money AmountTo,
	decimal ExchangeRate,
	RateStatus RateStatus,
	TransferStatus Status,
	string? Description,
	DateTimeOffset OccurredAt
) : IResponseOf<TransferReadModel, TransferResponse>
{
	public static TransferResponse FromReadModel(TransferReadModel readModel) => new TransferResponse(
		Id: readModel.Id,
		FromAccountId: readModel.FromAccountId,
		ToAccountId: readModel.ToAccountId,
		AmountFrom: readModel.AmountFrom,
		AmountTo: readModel.AmountTo,
		ExchangeRate: readModel.ExchangeRate,
		RateStatus: readModel.RateStatus,
		Status: readModel.Status,
		Description: readModel.Description,
		OccurredAt: readModel.OccurredAt
	);
}
