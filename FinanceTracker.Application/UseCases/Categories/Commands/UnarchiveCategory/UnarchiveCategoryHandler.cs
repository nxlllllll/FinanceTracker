using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Categories.Commands.UnarchiveCategory;

public sealed class UnarchiveCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository
) : IAuthorizedHandler<UnarchiveCategoryCommand, Category, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		UnarchiveCategoryCommand command,
		Category category,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = category.Unarchive();
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);
		
		await categoryWriteRepository.UnarchiveAsync(categoryId: command.CategoryId, ct: ct);
		return Result<Guid, DomainException>.Success(value: category.Id);
	}
}