using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Category.Commands.RenameCategory;

public sealed class RenameCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IAuthorizedHandler<RenameCategoryCommand, Core.Domains.Category.Category, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		RenameCategoryCommand command,
		Core.Domains.Category.Category user,
		CancellationToken ct = default)
	{
		Result<Name, DomainException> nameResult = Name.Create(value: command.NewName);
		if (nameResult.IsFailure)
			return Result<Guid, AppException>.Failure(error: nameResult.Error!);

		string oldName = user.Name;

		Result<Unit, DomainException> result = user.Rename(newName: nameResult.Value);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		await categoryWriteRepository.RenameAsync(
			categoryId: command.CategoryId,
			newName: nameResult.Value,
			expectedVersion: user.RowVersion,
			ct: ct
		);

		postCommitNotifications.Stage(notification: new CategoryRenamedNotification(
			CategoryId: user.Id,
			UserId: user.UserId,
			OldName: oldName,
			NewName: nameResult.Value,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
