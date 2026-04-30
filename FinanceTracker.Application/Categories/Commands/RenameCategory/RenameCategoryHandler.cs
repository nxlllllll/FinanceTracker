using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories;

namespace FinanceTracker.Application.Categories.Commands.RenameCategory;

public sealed class RenameCategoryHandler(
	ICategoryRepository categoryRepository
) : IAuthorizedHandler<RenameCategoryCommand, Category>
{
	public async Task HandleAsync(
		RenameCategoryCommand command,
		Category category,
		CancellationToken ct = default)
	{
		category.Rename(newName: command.NewName);
		await categoryRepository.RenameAsync(categoryId: command.CategoryId, newName: command.NewName, ct: ct);
	}
}