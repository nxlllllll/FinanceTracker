using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database;
using FinanceTracker.Infrastructure.Database.Repositories.Budget;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

public class BudgetBuilder(FinanceTrackerContext context)
{
	private readonly BudgetWriteRepository _writeRepository = new BudgetWriteRepository(context: context, dateProvider: FakeDateProvider.Default);

	public async Task<Guid> CreateAsync(
		Guid userId,
		Guid categoryId,
		string currency = "RUB",
		decimal amount = 10000m,
		DateOnly? dateFrom = null,
		DateOnly? dateTo = null)
	{
		Core.Domains.Budget.Budget budget = Core.Domains.Budget.Budget.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: userId,
			categoryId: categoryId,
			amount: new Money(amount: amount, currency: currency),
			from: dateFrom ?? new DateOnly(year: 2025, month: 1, day: 1),
			to: dateTo ?? new DateOnly(year: 2025, month: 1, day: 31)
		);
		
		await _writeRepository.CreateAsync(budget: budget);
		return budget.Id;
	}
}