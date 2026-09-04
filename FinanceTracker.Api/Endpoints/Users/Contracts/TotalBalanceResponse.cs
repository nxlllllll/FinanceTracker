using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Core.ReadModels.User;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Api.Endpoints.Users.Contracts;

public sealed record TotalBalanceResponse(
	decimal Amount,
	Currency Currency,
	bool IsApproximate
) : IResponseOf<TotalBalanceReadModel, TotalBalanceResponse>
{
	public static TotalBalanceResponse FromReadModel(TotalBalanceReadModel readModel) => new TotalBalanceResponse(
		Amount: readModel.Total.Amount,
		Currency: readModel.Total.Currency,
		IsApproximate: readModel.IsApproximate
	);
}
