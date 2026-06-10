using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Category.Commands.RenameCategory;

public sealed class RenameCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider
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
		
		string oldName = category.Name;
		
		Result<Unit, DomainException> result = category.Rename(newName: nameResult.Value);
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await categoryWriteRepository.RenameAsync(categoryId: command.CategoryId, newName: nameResult.Value, ct: ct);
		
		await publisher.Publish(notification: new CategoryRenamedNotification(
			CategoryId: category.Id,
			UserId: category.UserId,
			OldName: oldName,
			NewName: nameResult.Value,
			OccurredAt: dateProvider.UtcNow
		), cancellationToken: ct);
		
		return Result<Guid, DomainException>.Success(value: category.Id);
	}
}
