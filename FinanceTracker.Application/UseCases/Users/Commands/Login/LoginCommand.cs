using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Commands.Login;

public sealed record LoginCommand(
	Email Email,
	string Password
) : IRequest<Result<TokenResponse, DomainException>>;