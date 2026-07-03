using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Account.Queries.GetAccount;

public sealed class GetAccountHandler(
	IAccountReadRepository accountReadRepository
) : IRequestHandler<GetAccountQuery, Result<AccountReadModel, DomainException>>
{
	public async Task<Result<AccountReadModel, DomainException>> Handle(
		GetAccountQuery query,
		CancellationToken ct = default)
	{
		AccountReadModel? model = await accountReadRepository.GetByIdAsync(
			accountId: query.AccountId,
			userId: query.UserId,
			ct: ct
		);

		if (model is null)
			return Result<AccountReadModel, DomainException>.Failure(error: new NotFoundException(message: "Account not found.", id: query.AccountId));

		return Result<AccountReadModel, DomainException>.Success(value: model);
	}
}
