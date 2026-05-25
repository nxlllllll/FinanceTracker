using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Domains.User;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Queries.GetUser;

public sealed record GetUserQuery(Guid UserId) : IRequest<User?>, IUserScopedRequest;
