using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using MediatR;

namespace FinanceTracker.Application.Categories.Commands.ArchiveCategory;

public sealed class ArchiveCategoryHandler(
	ICategoryRepository categoryRepository,
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IUnitOfWork unitOfWork
) : IRequestHandler<ArchiveCategoryCommand>
{
	public async Task Handle(
		ArchiveCategoryCommand command,
		CancellationToken ct = default)
	{
		Category category = await categoryRepository.GetByIdAsync(categoryId: command.CategoryId, ct: ct)
			?? throw new NotFoundException(message: "Category not found.", id: command.CategoryId);

		if (category.UserId != command.UserId)
			throw new NotFoundException(message: "Category not found.", id: command.CategoryId);
		
		category.Archive();

		await unitOfWork.BeginTransactionAsync(ct: ct);

		try
		{
			await categoryRepository.ArchiveAsync(categoryId: command.CategoryId, ct: ct);
			await recurringTransactionWriteRepository.DeactivateByCategoryIdAsync(categoryId: command.CategoryId, ct: ct);

			await unitOfWork.CommitAsync(ct: ct);
		}
		catch
		{
			await unitOfWork.RollbackAsync(ct: ct);
			throw;
		}
	}
}