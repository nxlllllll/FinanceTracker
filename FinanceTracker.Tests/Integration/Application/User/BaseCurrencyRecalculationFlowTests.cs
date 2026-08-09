using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.UseCases.User.Commands.ChangeUserBaseCurrency;
using FinanceTracker.Application.UseCases.User.Queries.GetIncomeExpenseSummary;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.BaseCurrencyRecalculation.Job;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quartz;

namespace FinanceTracker.Tests.Integration.Application.User;

/// <summary>
/// The whole loop: changing base currency withholds totals, and the worker gives them back.
/// </summary>
public sealed class BaseCurrencyRecalculationFlowTests : MediatorFixture
{
	private static readonly Currency Usd = Currency.Create(value: "USD").Value;

	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public async Task SetupDataAsync()
	{
		_userBuilder = new UserBuilder(context: Context);
		await new CurrencyBuilder(context: Context).CreateAsync(code: "RUB");
		await new CurrencyBuilder(context: Context).CreateAsync(code: "USD");
	}

	private async Task<IncomeExpenseSummary> GetSummaryAsync(Guid userId)
	{
		Result<IncomeExpenseSummary, AppException> result = await Mediator.Send(request: new GetIncomeExpenseSummaryQuery(
			UserId: userId,
			Period: new DateOnly(year: DateTimeOffset.UtcNow.Year, month: DateTimeOffset.UtcNow.Month, day: 1)
		));

		await Assert.That(value: result.IsSuccess).IsTrue();
		return result.Value!;
	}

	private async Task RunRecalculationJobAsync()
	{
		await using AsyncServiceScope scope = Host.Services.CreateAsyncScope();

		BaseCurrencyRecalculationJob job = new BaseCurrencyRecalculationJob(
			recalculationWriteRepository: scope.ServiceProvider.GetRequiredService<IBaseCurrencyRecalculationWriteRepository>(),
			categoryTotalWriteRepository: scope.ServiceProvider.GetRequiredService<ICategoryTotalWriteRepository>(),
			userQueryRepository: scope.ServiceProvider.GetRequiredService<IUserQueryRepository>(),
			unitOfWork: scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
			dateProvider: scope.ServiceProvider.GetRequiredService<IDateProvider>(),
			options: new FakeOptionsMonitor<BaseCurrencyRecalculationJobOptions>(value: new BaseCurrencyRecalculationJobOptions()),
			logger: NullLogger<BaseCurrencyRecalculationJob>.Instance
		);

		IJobExecutionContext context = Substitute.For<IJobExecutionContext>();
		context.CancellationToken.Returns(returnThis: CancellationToken.None);

		await job.Execute(context: context);
	}

	[Test]
	public async Task ChangeBaseCurrency_ShouldWithholdTotalsUntilTheWorkerRebuildsThem()
	{
		Guid userId = await _userBuilder.CreateAsync();

		IncomeExpenseSummary before = await GetSummaryAsync(userId: userId);
		await Assert.That(value: before.RecalculationPending).IsFalse()
			.Because(message: "A user who never changed currency has nothing outstanding, and withholding their totals would blank the main screen for everybody.");

		Result<Guid, AppException> changed = await Mediator.Send(request: new ChangeUserBaseCurrencyCommand(
			UserId: userId,
			NewBaseCurrency: Usd
		));
		await Assert.That(value: changed.IsSuccess).IsTrue();

		IncomeExpenseSummary duringRebuild = await GetSummaryAsync(userId: userId);
		await Assert.That(value: duringRebuild.RecalculationPending).IsTrue().Because(message: """
			The command returns as soon as the change is recorded, so at this point the stored totals
			are still denominated in the previous currency. That is the window the flag exists for.
		""");
		await Assert.That(value: duringRebuild.Currency).IsEqualTo(expected: Usd)
			.Because(message: "The currency reported is the new one immediately — it is the amounts that lag, which is why they are withheld rather than relabelled.");

		await RunRecalculationJobAsync();

		IncomeExpenseSummary afterRebuild = await GetSummaryAsync(userId: userId);
		await Assert.That(value: afterRebuild.RecalculationPending).IsFalse()
			.Because(message: "Once the worker has rebuilt the totals they match the current currency, which is the point of the whole exchange.");
	}

	[Test]
	public async Task ChangeBaseCurrency_ShouldNotRebuildInsideTheRequest()
	{
		Guid userId = await _userBuilder.CreateAsync();

		await Mediator.Send(request: new ChangeUserBaseCurrencyCommand(UserId: userId, NewBaseCurrency: Usd));

		IncomeExpenseSummary summary = await GetSummaryAsync(userId: userId);

		await Assert.That(value: summary.RecalculationPending).IsTrue().Because(message: """
			If the request had rebuilt the totals itself this would already be false. Doing that work
			inline means an unbounded walk over the user's whole history while a client waits and a
			transaction stays open, which is what moved it to a worker in the first place.
		""");
	}
}
