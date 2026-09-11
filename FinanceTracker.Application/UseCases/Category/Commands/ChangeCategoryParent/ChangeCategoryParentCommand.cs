using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Commands.ChangeCategoryParent;

public sealed record ChangeCategoryParentCommand(
	Guid UserId,
	Guid CategoryId,
	Guid? NewParentId
) : IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest;
