using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Core.ReadModels.User;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Api.Endpoints.Users.Contracts;

/// <summary>
/// HTTP projection of <see cref="UserReadModel"/>.
/// </summary>
public sealed record UserResponse(
	Guid Id,
	Email Email,
	Currency BaseCurrency,
	TimeZoneId TimeZone,
	DateTimeOffset CreatedAt
) : IResponseOf<UserReadModel, UserResponse>
{
	public static UserResponse FromReadModel(UserReadModel readModel) => new UserResponse(
		Id: readModel.Id,
		Email: readModel.Email,
		BaseCurrency: readModel.BaseCurrency,
		TimeZone: readModel.TimeZone,
		CreatedAt: readModel.CreatedAt
	);
}
