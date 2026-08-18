using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Core.ReadModels.Currency;

namespace FinanceTracker.Api.Endpoints.Currencies.Contracts;

/// <summary>HTTP projection of <see cref="CurrencyInfo"/></summary>
public sealed record CurrencyResponse(
	string Code,
	string Name,
	string Symbol,
	bool IsActive
) : IResponseOf<CurrencyInfo, CurrencyResponse>
{
	public static CurrencyResponse FromReadModel(CurrencyInfo readModel) => new CurrencyResponse(
		Code: readModel.Code,
		Name: readModel.Name,
		Symbol: readModel.Symbol,
		IsActive: readModel.IsActive
	);
}
