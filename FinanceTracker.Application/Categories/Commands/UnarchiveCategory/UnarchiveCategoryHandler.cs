using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories;

namespace FinanceTracker.Application.Categories.Commands.UnarchiveCategory;

public sealed class UnarchiveCategoryHandler(
	ICategoryRepository categoryRepository
) : IAuthorizedHandler<UnarchiveCategoryCommand, Category>
{
	public async Task HandleAsync(
		UnarchiveCategoryCommand command,
		Category category,
		CancellationToken ct = default)
	{
		category.Unarchive();
		await categoryRepository.UnarchiveAsync(categoryId: command.CategoryId, ct: ct);
	}
}