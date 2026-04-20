using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using MediatR;

namespace FinanceTracker.Application.Categories.Commands.UnarchiveCategory;

public sealed class UnarchiveCategoryHandler(
	ICategoryRepository categoryRepository
) : IRequestHandler<UnarchiveCategoryCommand>
{
	public async Task Handle(
		UnarchiveCategoryCommand command,
		CancellationToken ct = default)
	{
		Category category = await categoryRepository.GetByIdAsync(categoryId: command.CategoryId, ct: ct)
			?? throw new NotFoundException(message: "Category not found.", id: command.CategoryId);
		
		category.Unarchive();
		await categoryRepository.UnarchiveAsync(categoryId: command.CategoryId, ct: ct);
	}
}