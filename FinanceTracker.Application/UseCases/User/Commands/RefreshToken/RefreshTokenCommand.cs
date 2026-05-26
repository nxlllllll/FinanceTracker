using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Commands.RefreshToken;

public sealed record RefreshTokenCommand(
	string RefreshToken
) : IRequest<Result<TokenResponse, DomainException>>;
