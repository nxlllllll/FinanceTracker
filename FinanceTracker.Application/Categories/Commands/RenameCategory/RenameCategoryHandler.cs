using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using MediatR;

namespace FinanceTracker.Application.Categories.Commands.RenameCategory;

public sealed class RenameCategoryHandler(
	ICategoryRepository categoryRepository
) : IRequestHandler<RenameCategoryCommand>
{
	public async Task Handle(
		RenameCategoryCommand command,
		CancellationToken ct = default)
	{
		Category category = await categoryRepository.GetByIdAsync(categoryId: command.CategoryId, ct: ct)
			?? throw new NotFoundException(message: "Category not found.", id: command.CategoryId);
		
		category.Rename(newName: command.NewName);
		await categoryRepository.RenameAsync(categoryId: command.CategoryId, newName: command.NewName, ct: ct);
	}
}