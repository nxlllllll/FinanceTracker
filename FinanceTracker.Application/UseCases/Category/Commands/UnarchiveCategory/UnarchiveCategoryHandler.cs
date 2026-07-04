using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Category.Commands.UnarchiveCategory;

public sealed class UnarchiveCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<UnarchiveCategoryHandler> logger
) : IAuthorizedHandler<UnarchiveCategoryCommand, Core.Domains.Category.Category, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		UnarchiveCategoryCommand command,
		Core.Domains.Category.Category entity,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = entity.Unarchive();
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		await categoryWriteRepository.UnarchiveAsync(categoryId: command.CategoryId, expectedVersion: entity.RowVersion, ct: ct);

		try
		{
			await publisher.Publish(notification: new CategoryUnarchivedNotification(
				CategoryId: entity.Id,
				UserId: entity.UserId,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish CategoryUnarchivedNotification for category {entity.Id} after successful commit.");
		}

		return Result<Guid, AppException>.Success(value: entity.Id);
	}
}
