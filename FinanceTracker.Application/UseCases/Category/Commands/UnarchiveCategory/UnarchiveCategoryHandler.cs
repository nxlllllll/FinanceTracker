using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Category.Commands.UnarchiveCategory;

public sealed class UnarchiveCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IAuthorizedHandler<UnarchiveCategoryCommand, Core.Domains.Category.Category, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		UnarchiveCategoryCommand command,
		Core.Domains.Category.Category user,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = user.Unarchive();
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		await categoryWriteRepository.UnarchiveAsync(categoryId: command.CategoryId, expectedVersion: user.RowVersion, ct: ct);

		postCommitNotifications.Stage(notification: new CategoryUnarchivedNotification(
			CategoryId: user.Id,
			UserId: user.UserId,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
