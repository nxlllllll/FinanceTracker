using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.User;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetTotalBalance;

public sealed record GetTotalBalanceQuery(Guid UserId) : IRequest<Result<TotalBalanceReadModel, AppException>>, IUserScopedRequest;
