using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Categories.Commands.UnarchiveCategory;

public sealed record UnarchiveCategoryCommand(
	Guid UserId,
	Guid CategoryId
) : IRequest<Result<Guid, DomainException>>, IAuthorizable;