using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Contracts.Events.Budget;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Integration.Chaos;

public sealed class BudgetAlertConnectionDropChaosTests : MediatorFixture
{
	private ConnectionDropInterceptor _connectionDrop = null!;

	protected override void ConfigureHostServices(IServiceCollection services, IConfiguration configuration)
	{
		_connectionDrop = new ConnectionDropInterceptor(
			connectionString: configuration.GetConnectionString(name: nameof(FinanceTrackerContext))!,
			commandTextFragment: "INSERT INTO outbox_messages"
		);

		services.ConfigureDbContext<FinanceTrackerContext>(optionsAction: options => options.AddInterceptors(_connectionDrop));
	}

	[Test]
	public async Task ASpendingCommandWhoseConnectionDropsAtCommit_ShouldStageTheAlertOnce()
	{
		Guid userId = await new UserBuilder(context: Context).CreateAsync();
		await new CurrencyBuilder(context: Context).CreateAsync(code: "RUB");
		Guid accountId = await new AccountFlowBuilder(mediator: Mediator, context: Context).CreateAsync(userId: userId, balance: 10_000m);
		Guid categoryId = await new CategoryBuilder(context: Context).CreateAsync(userId: userId);

		DateOnly today = DateOnly.FromDateTime(dateTime: DateTime.UtcNow);
		Guid budgetId = await new BudgetBuilder(context: Context).CreateAsync(
			userId: userId,
			categoryId: categoryId,
			amount: 5_000m,
			dateFrom: today.AddDays(value: -1),
			dateTo: today.AddDays(value: 30)
		);

		_connectionDrop.Arm();

		Result<Guid, AppException> result = await Mediator.Send(request: new CreateTransactionCommand(
			AccountId: accountId,
			UserId: userId,
			CategoryId: categoryId,
			Amount: 4_500m,
			Currency: Currency.Create(value: "RUB").Value,
			Direction: DirectionType.Debit,
			Description: null,
			OccurredAt: DateTimeOffset.UtcNow
		)
		{ IdempotencyKey = Guid.CreateVersion7() });

		await Assert.That(value: _connectionDrop.Fired).IsTrue().Because(message: """
			Without this the rest of the test is vacuous: the alert would have been saved on a healthy
			connection and proved nothing about an attempt that staged it and then died.
		""");

		await Assert.That(value: result.IsSuccess).IsTrue().Because(message: $"The retried command came back as {result.Error?.GetType().Name}: {result.Error?.Message}");

		await using FinanceTrackerContext read = CreateReadContext();

		List<BudgetThresholdReachedEvent> alerts = await TestBudgetThresholdAlerts.ReadStagedAsync(context: read, budgetId: budgetId);

		await Assert.That(value: alerts).Count().IsEqualTo(expected: 1).Because(message: """
			The first attempt staged the alert and died while saving it. A second alert would mean the
			staged one outlived the rollback and was saved alongside the retry's own.
		""");

		decimal spent = await read.BudgetProgresses
			.Where(predicate: p => p.BudgetId == budgetId)
			.Select(selector: p => p.Spent)
			.SingleAsync();

		await Assert.That(value: spent).IsEqualTo(expected: 4_500m).Because(message: """
			The first attempt's progress update ran before the connection dropped. Twice the amount would
			mean it survived and the retry added the spending again.
		""");
	}
}
