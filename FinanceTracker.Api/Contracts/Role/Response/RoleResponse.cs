using FinanceTracker.Api.Contracts.Abstractions;
using FinanceTracker.Core.Repositories.Role;

namespace FinanceTracker.Api.Contracts.Role.Response;

public sealed record RoleResponse(
	Guid Id,
	string? SystemKey,
	string DisplayName,
	IReadOnlySet<string> Permissions
) : IResponseOf<RoleDto, RoleResponse>
{
	public static RoleResponse FromReadModel(RoleDto readModel) => new RoleResponse(
		Id: readModel.Id,
		SystemKey: readModel.SystemKey?.ToString().ToLowerInvariant(),
		DisplayName: readModel.DisplayName.Value,
		Permissions: readModel.Permissions.Select(selector: p => p.ToString()).ToHashSet()
	);
}
