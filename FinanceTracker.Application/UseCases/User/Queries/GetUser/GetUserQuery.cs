using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetUser;

public sealed record GetUserQuery(Guid UserId) : IRequest<Result<UserReadModel, AppException>>, IUserScopedRequest;
