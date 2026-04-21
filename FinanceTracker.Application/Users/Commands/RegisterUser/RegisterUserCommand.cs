using MediatR;

namespace FinanceTracker.Application.Users.Commands.RegisterUser;

public sealed record RegisterUserCommand(
	string Email,
	string PasswordHash,
	string BaseCurrencyCode
) : IRequest<Guid>;