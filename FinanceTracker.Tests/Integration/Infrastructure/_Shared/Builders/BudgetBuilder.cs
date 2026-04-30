using FinanceTracker.Infrastructure.Database;
using FinanceTracker.Infrastructure.Database.Repositories.Budget;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

public class BudgetBuilder(FinanceTrackerContext context)
{
	private readonly BudgetWriteRepository _writeRepository = new BudgetWriteRepository(context: context);

	public async Task<Guid> CreateAsync(
		Guid userId,
		Guid categoryId,
		string currency = "RUB",
		decimal amount = 10000m,
		DateOnly? dateFrom = null,
		DateOnly? dateTo = null)
	{
		Core.Domains.Budget.Budget budget = Core.Domains.Budget.Budget.Create(
			userId: userId,
			categoryId: categoryId,
			currency: currency,
			amount: amount,
			from: dateFrom ?? new DateOnly(year: 2025, month: 1, day: 1),
			to: dateTo ?? new DateOnly(year: 2025, month: 1, day: 31)
		);
		
		await _writeRepository.CreateAsync(budget: budget);
		return budget.Id;
	}
}