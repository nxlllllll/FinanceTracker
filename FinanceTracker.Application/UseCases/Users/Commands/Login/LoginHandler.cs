using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.Services.Password;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Commands.Login;

public sealed class LoginHandler(
	IUserReadRepository userReadRepository,
	IPasswordHasher passwordHasher,
	ISessionIssuer sessionIssuer
) : IRequestHandler<LoginCommand, Result<TokenResponse, DomainException>>
{
	public async Task<Result<TokenResponse, DomainException>> Handle(
		LoginCommand command,
		CancellationToken ct = default)
	{
		Core.Domains.User.User? user = await userReadRepository.GetByEmailAsync(email: command.Email.Value, ct: ct);

		if (user is null || !await passwordHasher.Verify(password: command.Password, hash: user.PasswordHash))
			return Result<TokenResponse, DomainException>.Failure(error: new InvalidCredentialsException());

		TokenResponse response = await sessionIssuer.IssueAsync(user: user, ct: ct);
		return Result<TokenResponse, DomainException>.Success(value: response);
	}
}