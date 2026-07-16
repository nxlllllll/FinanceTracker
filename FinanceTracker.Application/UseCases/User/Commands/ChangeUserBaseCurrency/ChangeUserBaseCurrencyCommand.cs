using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Commands.ChangeUserBaseCurrency;

public sealed record ChangeUserBaseCurrencyCommand(
	Guid UserId,
	Core.ValueObjects.Currency NewBaseCurrency
) : IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest;
