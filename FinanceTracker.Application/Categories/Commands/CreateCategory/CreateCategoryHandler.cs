using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories;
using MediatR;

namespace FinanceTracker.Application.Categories.Commands.CreateCategory;

public sealed class CreateCategoryHandler(
	ICategoryRepository categoryRepository
) : IRequestHandler<CreateCategoryCommand>
{
	public async Task Handle(
		CreateCategoryCommand command,
		CancellationToken ct = default)
	{
		Category category = Category.Create(
			userId: command.UserId,
			name: command.Name,
			parentId: command.ParentId,
			type: command.Type
		);
		
		await categoryRepository.CreateAsync(category: category, ct: ct);
	}
}
