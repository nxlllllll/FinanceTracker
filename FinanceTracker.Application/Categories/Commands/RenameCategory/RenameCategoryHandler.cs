using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories.Category;

namespace FinanceTracker.Application.Categories.Commands.RenameCategory;

public sealed class RenameCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository
) : IAuthorizedHandler<RenameCategoryCommand, Category>
{
	public async Task HandleAsync(
		RenameCategoryCommand command,
		Category category,
		CancellationToken ct = default)
	{
		category.Rename(newName: command.NewName);
		await categoryWriteRepository.RenameAsync(categoryId: command.CategoryId, newName: command.NewName, ct: ct);
	}
}