using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Category.Commands.UnarchiveCategory;

public sealed class UnarchiveCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider
) : IAuthorizedHandler<UnarchiveCategoryCommand, Core.Domains.Category.Category, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		UnarchiveCategoryCommand command,
		Core.Domains.Category.Category accounts,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = accounts.Unarchive();
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);
		
		await categoryWriteRepository.UnarchiveAsync(categoryId: command.CategoryId, expectedVersion: accounts.RowVersion, ct: ct);
		
		await publisher.Publish(notification: new CategoryUnarchivedNotification(
			CategoryId: accounts.Id,
			UserId: accounts.UserId,
			OccurredAt: dateProvider.UtcNow
		), cancellationToken: ct);
		
		return Result<Guid, DomainException>.Success(value: accounts.Id);
	}
}
