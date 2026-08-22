using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels.Account;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Account.Queries.GetAccount;

public sealed class GetAccountHandler(
	IAccountReadRepository accountReadRepository
) : IRequestHandler<GetAccountQuery, Result<AccountReadModel, AppException>>
{
	public async Task<Result<AccountReadModel, AppException>> Handle(
		GetAccountQuery query,
		CancellationToken ct = default)
	{
		AccountReadModel? model = await accountReadRepository.GetByIdAsync(
			accountId: query.AccountId,
			userId: query.UserId,
			ct: ct
		);

		if (model is null)
			return Result<AccountReadModel, AppException>.Failure(error: new NotFoundException(message: "Account not found.", id: query.AccountId));

		return Result<AccountReadModel, AppException>.Success(value: model);
	}
}
