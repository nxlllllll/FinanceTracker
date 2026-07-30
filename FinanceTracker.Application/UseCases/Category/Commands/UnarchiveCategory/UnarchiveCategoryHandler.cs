using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Application.UseCases.Category.Commands.UnarchiveCategory;

public sealed class UnarchiveCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IAuthorizedHandler<UnarchiveCategoryCommand, Core.Domains.Category.Category, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		UnarchiveCategoryCommand command,
		Core.Domains.Category.Category category,
		CancellationToken ct = default)
	{
		Result<bool, DomainException> result = category.Unarchive();
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		if (!result.Value)
			return Result<Guid, AppException>.Success(value: category.Id);

		await categoryWriteRepository.UnarchiveAsync(categoryId: command.CategoryId, expectedVersion: category.RowVersion, ct: ct);

		postCommitNotifications.Stage(notification: new CategoryUnarchivedNotification(
			CategoryId: category.Id,
			UserId: category.UserId,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: category.Id);
	}
}
