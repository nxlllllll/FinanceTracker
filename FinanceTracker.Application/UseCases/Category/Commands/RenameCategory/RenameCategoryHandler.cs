using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Category.Commands.RenameCategory;

public sealed class RenameCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<RenameCategoryHandler> logger
) : IAuthorizedHandler<RenameCategoryCommand, Core.Domains.Category.Category, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		RenameCategoryCommand command,
		Core.Domains.Category.Category entity,
		CancellationToken ct = default)
	{
		Result<Name, DomainException> nameResult = Name.Create(value: command.NewName);
		if (nameResult.IsFailure)
			return Result<Guid, DomainException>.Failure(error: nameResult.Error!);
		
		string oldName = entity.Name;
		
		Result<Unit, DomainException> result = entity.Rename(newName: nameResult.Value);
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await categoryWriteRepository.RenameAsync(
			categoryId: command.CategoryId,
			newName: nameResult.Value,
			expectedVersion: entity.RowVersion, 
			ct: ct
		);

		try
		{
			await publisher.Publish(notification: new CategoryRenamedNotification(
				CategoryId: entity.Id,
				UserId: entity.UserId,
				OldName: oldName,
				NewName: nameResult.Value,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish CategoryRenamedNotification for category {entity.Id} after successful commit.");
		}

		return Result<Guid, DomainException>.Success(value: entity.Id);
	}
}