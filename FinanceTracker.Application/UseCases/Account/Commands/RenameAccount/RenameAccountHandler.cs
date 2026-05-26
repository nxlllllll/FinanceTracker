using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Application.UseCases.Account.Commands.RenameAccount;

public sealed class RenameAccountHandler(
	IAccountRepository accountRepository,
	IDateProvider dateProvider
) : IAuthorizedHandler<RenameAccountCommand, Core.Domains.Account.Account, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		RenameAccountCommand command,
		Core.Domains.Account.Account account,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = account.Rename(occurredAt: dateProvider.UtcNow, newName: command.NewName);
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		if (account.Events.Count > 0)
			await accountRepository.SaveAsync(account: account, ct: ct);
		
		return Result<Guid, DomainException>.Success(value: account.Id);
	}
}
