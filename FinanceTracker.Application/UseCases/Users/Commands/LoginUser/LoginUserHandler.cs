using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.Services.Password;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Commands.LoginUser;

public sealed class LoginUserHandler(
	IUserReadRepository userReadRepository,
	IPasswordHasher passwordHasher,
	ISessionIssuer sessionIssuer
) : IRequestHandler<LoginUserCommand, Result<TokenResponse, DomainException>>
{
	public async Task<Result<TokenResponse, DomainException>> Handle(
		LoginUserCommand userCommand,
		CancellationToken ct = default)
	{
		Core.Domains.User.User? user = await userReadRepository.GetByEmailAsync(email: userCommand.Email.Value, ct: ct);

		if (user is null || !await passwordHasher.Verify(password: userCommand.Password, hash: user.PasswordHash))
			return Result<TokenResponse, DomainException>.Failure(error: new InvalidCredentialsException());

		TokenResponse response = await sessionIssuer.IssueAsync(user: user, ct: ct);
		return Result<TokenResponse, DomainException>.Success(value: response);
	}
}
