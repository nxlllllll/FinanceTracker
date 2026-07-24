using FinanceTracker.Api.Contracts.Abstractions;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Api.Contracts.Account.Response;

/// <summary>HTTP projection of <see cref="AccountReadModel"/></summary>
public sealed record AccountResponse(
	Guid Id,
	Name Name,
	AccountType Type,
	Money Balance,
	bool IsArchived,
	int Version,
	DateTimeOffset CreatedAt
) : IResponseOf<AccountReadModel, AccountResponse>
{
	public static AccountResponse FromReadModel(AccountReadModel readModel) => new AccountResponse(
		Id: readModel.Id,
		Name: readModel.Name,
		Type: readModel.Type,
		Balance: readModel.Balance,
		IsArchived: readModel.IsArchived,
		Version: readModel.Version,
		CreatedAt: readModel.CreatedAt
	);
}
