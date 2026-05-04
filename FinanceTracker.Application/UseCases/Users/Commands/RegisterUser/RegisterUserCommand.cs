using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Commands.RegisterUser;

public sealed record RegisterUserCommand(
	string Email,
	string PasswordHash,
	string BaseCurrencyCode
) : IRequest<Result<Guid, DomainException>>;