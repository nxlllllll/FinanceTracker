using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.Services.Password;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Commands.LoginUser;

public sealed class LoginUserHandler(
	IUserReadRepository userReadRepository,
	IPasswordHasher passwordHasher,
	ISessionIssuer sessionIssuer
) : IRequestHandler<LoginUserCommand, Result<SessionToken, DomainException>>
{
	public async Task<Result<SessionToken, DomainException>> Handle(
		LoginUserCommand userCommand,
		CancellationToken ct = default)
	{
		Core.Domains.User.User? user = await userReadRepository.GetByEmailAsync(email: userCommand.Email.Value, ct: ct);

		if (user is null || !await passwordHasher.Verify(password: userCommand.Password, hash: user.PasswordHash))
			return Result<SessionToken, DomainException>.Failure(error: new InvalidCredentialsException());

		SessionToken response = await sessionIssuer.IssueAsync(user: user, ct: ct);
		return Result<SessionToken, DomainException>.Success(value: response);
	}
}
