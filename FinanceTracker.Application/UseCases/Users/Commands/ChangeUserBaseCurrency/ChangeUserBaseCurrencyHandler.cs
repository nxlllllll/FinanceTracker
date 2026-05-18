using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Application.UseCases.Users.Commands.ChangeUserBaseCurrency;

public sealed class ChangeUserBaseCurrencyHandler(
	IUserWriteRepository userWriteRepository
) : IAuthorizedHandler<ChangeUserBaseCurrencyCommand, User, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeUserBaseCurrencyCommand command,
		User user,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = user.ChangeBaseCurrency(newBaseCurrency: command.NewBaseCurrency);
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await userWriteRepository.ChangeBaseCurrencyAsync(
			userId: command.UserId,
			newBaseCurrencyCode: command.NewBaseCurrency,
			ct: ct
		);
		
		return Result<Guid, DomainException>.Success(value: user.Id);
	}
}