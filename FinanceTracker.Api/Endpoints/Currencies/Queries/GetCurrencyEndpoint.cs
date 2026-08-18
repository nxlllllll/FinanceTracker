using FinanceTracker.Api.Endpoints.Currencies.Contracts;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Currency.Queries.GetCurrency;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels.Currency;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Currencies.Queries;

public sealed class GetCurrencyEndpoint : IEndpoint
{
	public string GroupName => CurrenciesEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/{code}", handler: HandleAsync)
			.RequirePermission(resource: Resource.Currency, action: PermissionAction.Read)
			.WithSummary(summary: "Get a currency by its ISO code")
			.Produces<CurrencyResponse>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	private static async Task<IHttpResult> HandleAsync(
		string code,
		ISender sender,
		CancellationToken ct)
	{
		Result<Currency, DomainException> currency = Currency.Create(value: code);
		if (currency.IsFailure)
			return currency.Error!.ToValidationProblem(fieldName: nameof(code));

		Result<CurrencyInfo, AppException> result = await sender.Send(
			request: new GetCurrencyQuery(Code: currency.Value),
			cancellationToken: ct
		);

		return result.ToHttpResult<CurrencyInfo, CurrencyResponse>();
	}
}
