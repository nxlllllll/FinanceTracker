using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetCategory;

public sealed record GetCategoryQuery(
	Guid CategoryId,
	Guid UserId
) : IRequest<Result<CategoryReadModel, DomainException>>, IUserScopedRequest;
