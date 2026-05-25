using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Users.Commands.RevokeToken;

public sealed record RevokeTokenCommand(
	string RefreshToken
) : IRequest<Result<Unit, DomainException>>;
