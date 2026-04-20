using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using MediatR;

namespace FinanceTracker.Application.Categories.Commands.ArchiveCategory;

public sealed class ArchiveCategoryHandler(
	ICategoryRepository categoryRepository
) : IRequestHandler<ArchiveCategoryCommand>
{
	public async Task Handle(
		ArchiveCategoryCommand command,
		CancellationToken ct = default)
	{
		Category category = await categoryRepository.GetByIdAsync(categoryId: command.CategoryId, ct: ct)
			?? throw new NotFoundException(message: "Category not found.", id: command.CategoryId);
		
		category.Archive();
		await categoryRepository.ArchiveAsync(categoryId: command.CategoryId, ct: ct);
	}
}