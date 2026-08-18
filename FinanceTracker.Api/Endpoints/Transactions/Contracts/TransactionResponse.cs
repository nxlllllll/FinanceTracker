using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ReadModels.Transaction;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Api.Endpoints.Transactions.Contracts;

/// <summary>
/// HTTP projection of <see cref="TransactionReadModel"/>.
/// </summary>
public sealed record TransactionResponse(
	Guid Id,
	Guid AccountId,
	Guid CategoryId,
	Money Amount,
	DirectionType Direction,
	decimal ExchangeRate,
	RateStatus RateStatus,
	bool IsExcluded,
	string? Description,
	DateTimeOffset OccurredAt
) : IResponseOf<TransactionReadModel, TransactionResponse>
{
	public static TransactionResponse FromReadModel(TransactionReadModel readModel) => new TransactionResponse(
		Id: readModel.Id,
		AccountId: readModel.AccountId,
		CategoryId: readModel.CategoryId,
		Amount: readModel.Amount,
		Direction: readModel.Direction,
		ExchangeRate: readModel.ExchangeRate,
		RateStatus: readModel.RateStatus,
		IsExcluded: readModel.IsExcluded,
		Description: readModel.Description,
		OccurredAt: readModel.OccurredAt
	);
}
