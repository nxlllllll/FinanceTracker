using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;

namespace FinanceTracker.Application.UseCases.Categories.Commands.CreateCategory;

public sealed class CreateCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository,
	IDateProvider dateProvider
) : IRequestHandler<CreateCategoryCommand, Result<Guid, DomainException>>
{
	public async Task<Result<Guid, DomainException>> Handle(
		CreateCategoryCommand command,
		CancellationToken ct = default)
	{
		Result<Category, DomainException> categoryResult = Category.Create(
			createdAt: dateProvider.UtcNow,
			userId: command.UserId,
			name: command.Name,
			parentId: command.ParentId,
			type: command.Type
		);
		if (categoryResult.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: categoryResult.Error!);
 
		Category category = categoryResult.Value!;
		await categoryWriteRepository.CreateAsync(category: category, ct: ct);
		return Result<Guid, DomainException>.Success(value: category.Id);
	}
}