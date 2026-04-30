using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories.Category;
using MediatR;

namespace FinanceTracker.Application.Categories.Commands.CreateCategory;

public sealed class CreateCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository
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

		await categoryWriteRepository.CreateAsync(category: category, ct: ct);
	}
}