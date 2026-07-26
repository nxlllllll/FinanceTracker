using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Api.Security;

/// <summary>
/// Parses a set of "resource:action" strings into validated Permission VOs, collecting all
/// format errors at once — so a role with three typos gets one 400 listing all three, not three round trips.
/// </summary>
public static class PermissionSetParser
{
	public static Result<IReadOnlySet<Permission>, ValidationException> Parse(IReadOnlySet<string> raw)
	{
		Dictionary<string, string[]> errors = [];
		HashSet<Permission> parsed = [];

		foreach (string value in raw)
		{
			Result<Permission, DomainException> result = Permission.Create(value: value);
			if (result.IsFailure)
				errors[value] = [result.Error!.Message];
			else
				parsed.Add(item: result.Value!);
		}

		if (errors.Count > 0)
			return Result<IReadOnlySet<Permission>, ValidationException>.Failure(error: new ValidationException(errors: errors));

		return Result<IReadOnlySet<Permission>, ValidationException>.Success(value: parsed);
	}
}
