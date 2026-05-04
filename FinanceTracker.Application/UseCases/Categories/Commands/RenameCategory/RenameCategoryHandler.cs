using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Categories.Commands.RenameCategory;

public sealed class RenameCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository
) : IAuthorizedHandler<RenameCategoryCommand, Category, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		RenameCategoryCommand command,
		Category category,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = category.Rename(newName: command.NewName);
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await categoryWriteRepository.RenameAsync(categoryId: command.CategoryId, newName: command.NewName, ct: ct);
		return Result<Guid, DomainException>.Success(value: category.Id);
	}
}