using FinanceTracker.Application.Behaviours.RateLimit;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetUser;

public sealed record GetUserQuery(Guid UserId) : IRequest<Core.Domains.User.User?>, IUserScopedRequest;
