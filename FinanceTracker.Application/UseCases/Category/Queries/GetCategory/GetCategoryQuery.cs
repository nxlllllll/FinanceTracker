using FinanceTracker.Application.Behaviours.RateLimit;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Queries.GetCategory;

public sealed record GetCategoryQuery(Guid CategoryId, Guid UserId) : IRequest<Core.Domains.Category.Category?>, IUserScopedRequest;
