using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.ReadModels;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetUser;

public sealed record GetUserQuery(Guid UserId) : IRequest<UserReadModel?>, IUserScopedRequest;