using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories.Category;

namespace FinanceTracker.Application.Categories.Commands.UnarchiveCategory;

public sealed class UnarchiveCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository
) : IAuthorizedHandler<UnarchiveCategoryCommand, Category, Guid>
{
	public async Task<Guid> HandleAsync(
		UnarchiveCategoryCommand command,
		Category category,
		CancellationToken ct = default)
	{
		category.Unarchive();
		await categoryWriteRepository.UnarchiveAsync(categoryId: command.CategoryId, ct: ct);
		
		return category.Id;
	}
}