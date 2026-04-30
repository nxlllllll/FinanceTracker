using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.RecurringTransaction;

namespace FinanceTracker.Application.Categories.Commands.ArchiveCategory;

public sealed class ArchiveCategoryHandler(
	ICategoryRepository categoryRepository,
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IUnitOfWork unitOfWork
) : IAuthorizedHandler<ArchiveCategoryCommand, Category>
{
	public async Task HandleAsync(
		ArchiveCategoryCommand command,
		Category category,
		CancellationToken ct = default)
	{
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