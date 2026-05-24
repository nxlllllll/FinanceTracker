using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Categories.Commands.RenameCategory;

public sealed record RenameCategoryCommand(
	Guid UserId,
	Guid CategoryId,
	Name NewName
) : IRequest<Result<Guid, DomainException>>, IAuthorizable, IUserScopedRequest;