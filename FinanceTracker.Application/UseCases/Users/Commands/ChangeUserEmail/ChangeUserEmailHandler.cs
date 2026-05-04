using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Application.UseCases.Users.Commands.ChangeUserEmail;

public sealed class ChangeUserEmailHandler(
	IUserReadRepository userReadRepository,
	IUserWriteRepository userWriteRepository
) : IAuthorizedHandler<ChangeUserEmailCommand, User, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeUserEmailCommand command,
		User user,
		CancellationToken ct = default)
	{
		User? existing = await userReadRepository.GetByEmailAsync(email: command.NewEmail, ct: ct);
		if (existing is not null)
			return Result<Guid, DomainException>.Failure(error: new EmailException(message: "The user with this email address already exists.", email: command.NewEmail)!);

		Result<Email, DomainException> newEmailResult = Email.Create(value: command.NewEmail);
		if (newEmailResult.IsFailure)
			return Result<Guid, DomainException>.Failure(error: newEmailResult.Error!);
		
		Result<Unit, DomainException> result = user.ChangeEmail(newEmail: newEmailResult.Value);
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);
		
		await userWriteRepository.ChangeEmailAsync(
			userId: command.UserId,
			newEmail: command.NewEmail,
			ct: ct
		);
		
		return Result<Guid, DomainException>.Success(value: user.Id);
	}
}