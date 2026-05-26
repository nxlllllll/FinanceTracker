using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Application.UseCases.Category.Commands.RenameCategory;

public sealed class RenameCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository
) : IAuthorizedHandler<RenameCategoryCommand, Core.Domains.Category.Category, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		RenameCategoryCommand command,
		Core.Domains.Category.Category category,
		CancellationToken ct = default)
	{
		Result<Name, DomainException> nameResult = Name.Create(value: command.NewName);
		if (nameResult.IsFailure)
			return Result<Guid, DomainException>.Failure(error: nameResult.Error!);
		
		Result<Unit, DomainException> result = category.Rename(newName: nameResult.Value);
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await categoryWriteRepository.RenameAsync(categoryId: command.CategoryId, newName: nameResult.Value, ct: ct);
		return Result<Guid, DomainException>.Success(value: category.Id);
	}
}
