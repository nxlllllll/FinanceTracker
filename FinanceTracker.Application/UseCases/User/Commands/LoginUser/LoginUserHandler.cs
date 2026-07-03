using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.Services.Password;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Commands.LoginUser;

public sealed class LoginUserHandler(
	IUserAuthRepository userAuthRepository,
	IPasswordHasher passwordHasher,
	ISessionIssuer sessionIssuer
) : IRequestHandler<LoginUserCommand, Result<SessionToken, DomainException>>
{
	public async Task<Result<SessionToken, DomainException>> Handle(
		LoginUserCommand userCommand,
		CancellationToken ct = default)
	{
		Core.Domains.User.User? user = await userAuthRepository.GetByEmailAsync(email: userCommand.Email.Value, ct: ct);

		bool passwordMatches = await passwordHasher.Verify(password: userCommand.Password, storedHash: user?.PasswordHash);

		if (user is null || !passwordMatches)
			return Result<SessionToken, DomainException>.Failure(error: new InvalidCredentialsException());

		SessionToken response = await sessionIssuer.IssueAsync(user: user, ct: ct);
		return Result<SessionToken, DomainException>.Success(value: response);
	}
}
