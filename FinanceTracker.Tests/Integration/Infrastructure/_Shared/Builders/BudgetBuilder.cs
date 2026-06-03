using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Repositories.Budget;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

public class BudgetBuilder(FinanceTrackerContext context)
{
	private readonly BudgetWriteRepository _writeRepository = new BudgetWriteRepository(
		context: context,
		dateProvider: FakeDateProvider.Default
	);

	public async Task<Guid> CreateAsync(
		Guid userId,
		Guid categoryId,
		string currency = "RUB",
		decimal amount = 10000m,
		DateOnly? dateFrom = null,
		DateOnly? dateTo = null)
	{
		Result<Core.Domains.Budget.Budget, DomainException> result = Core.Domains.Budget.Budget.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: userId,
			categoryId: categoryId,
			amount: Money.Create(amount: amount, currency: Currency.Create(value: currency).Value).Value,
			from: dateFrom ?? new DateOnly(year: 2025, month: 1, day: 1),
			to: dateTo ?? new DateOnly(year: 2025, month: 1, day: 31)
		);

		Core.Domains.Budget.Budget budget = result.Value!;

		await _writeRepository.CreateAsync(budget: budget);
		await context.SaveChangesAsync();
		return budget.Id;
	}
}