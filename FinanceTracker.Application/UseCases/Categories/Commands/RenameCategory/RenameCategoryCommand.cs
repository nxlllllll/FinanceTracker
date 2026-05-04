using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Categories.Commands.RenameCategory;

public sealed record RenameCategoryCommand(
	Guid UserId,
	Guid CategoryId,
	string NewName
) : IRequest<Result<Guid, DomainException>>, IAuthorizable;