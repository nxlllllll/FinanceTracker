using FinanceTracker.Api.Endpoints.Currencies.Queries;
using FinanceTracker.Application.UseCases.Currency.Queries.GetCurrencies;
using FinanceTracker.Application.UseCases.Currency.Queries.GetCurrency;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels.Currency;
using FinanceTracker.Core.Results;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Api.Endpoints;

public class CurrencyEndpointTests
{
	[Test]
	public async Task GetCurrencies_ShouldQueryForTheWholeCatalogue()
	{
		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<GetCurrenciesQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<IReadOnlyList<CurrencyInfo>, AppException>.Success(value: []));

		await GetCurrenciesEndpoint.HandleAsync(sender: sender, ct: CancellationToken.None);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Any<GetCurrenciesQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetCurrency_WithAnInvalidCode_ShouldNotReachTheHandler()
	{
		ISender sender = Substitute.For<ISender>();

		await GetCurrencyEndpoint.HandleAsync(
			code: "RUBLE",
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<GetCurrencyQuery>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GetCurrency_WithAValidCode_ShouldQueryForTheParsedValue()
	{
		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<GetCurrencyQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<CurrencyInfo, AppException>.Failure(
			error: new NotFoundException(message: "Currency not found.", id: Guid.Empty)
		));

		await GetCurrencyEndpoint.HandleAsync(
			code: "usd",
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetCurrencyQuery>(predicate: query => query!.Code.Value == "USD"),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}
