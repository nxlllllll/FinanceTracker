using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Commands.ArchiveCategory;

public sealed record ArchiveCategoryCommand(
	Guid UserId,
	Guid CategoryId
) : IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest;
