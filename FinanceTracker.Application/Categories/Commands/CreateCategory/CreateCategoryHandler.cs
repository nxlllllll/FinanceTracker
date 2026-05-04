using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;

namespace FinanceTracker.Application.Categories.Commands.CreateCategory;

public sealed class CreateCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository,
	IDateProvider dateProvider
) : IRequestHandler<CreateCategoryCommand, Guid>
{
	public async Task<Guid> Handle(
		CreateCategoryCommand command,
		CancellationToken ct = default)
	{
		Category category = Category.Create(
			createdAt: dateProvider.UtcNow,
			userId: command.UserId,
			name: command.Name,
			parentId: command.ParentId,
			type: command.Type
		);

		await categoryWriteRepository.CreateAsync(category: category, ct: ct);
		
		return category.Id;
	}
}