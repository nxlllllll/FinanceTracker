using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;

namespace FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;

public sealed class CreateAccountHandler(
	IAccountRepository accountRepository,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider
) : IRequestHandler<CreateAccountCommand, Result<Guid, AppException>>
{
	public async Task<Result<Guid, AppException>> Handle(
		CreateAccountCommand command,
		CancellationToken ct = default)
	{
		Result<Core.Domains.Account.Account, DomainException> accountResult = Core.Domains.Account.Account.Create(
			occurredAt: dateProvider.UtcNow,
			userId: command.UserId,
			name: command.Name,
			type: command.Type,
			currency: command.Currency,
			balance: command.InitialBalance
		);
		if (accountResult.IsFailure)
			return Result<Guid, AppException>.Failure(error: accountResult.Error!);

		Core.Domains.Account.Account account = accountResult.Value!;

		await unitOfWork.ExecuteInTransactionAsync(operation: async () => await accountRepository.SaveAsync(account: account, ct: ct), ct: ct);

		return Result<Guid, AppException>.Success(value: account.Id);
	}
}
