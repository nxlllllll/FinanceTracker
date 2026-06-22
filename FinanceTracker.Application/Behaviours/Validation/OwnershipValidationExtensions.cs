using FluentValidation;

namespace FinanceTracker.Application.Behaviours.Validation;

public static class OwnershipValidationExtensions
{
	/// <summary>
	/// Fails validation unless the referenced entity exists and belongs to the same user as the command.
	/// </summary>
	/// <param name="existsForUserAsync">
	/// Resolves to <c>true</c> only if an entity with this id exists <em>and</em> is owned by this user —
	/// e.g. <c>(id, userId, ct) =&gt; repository.ExistsAsync(id, userId, ct)</c>.
	/// </param>
	/// <param name="userIdSelector">Reads the owning user id off the command instance.</param>
	/// <param name="entityName">Used in the default error message, e.g. <c>"category"</c>.</param>
	public static IRuleBuilderOptions<T, Guid> MustBelongToUser<T>(
		this IRuleBuilder<T, Guid> ruleBuilder,
		Func<Guid, Guid, CancellationToken, Task<bool>> existsForUserAsync,
		Func<T, Guid> userIdSelector,
		string entityName)
	{
		return ruleBuilder.MustAsync(predicate: async (instance, id, ct) => await existsForUserAsync(id, userIdSelector(instance), ct))
			.WithMessage(errorMessage: $"The {entityName} was not found.");
	}

	/// <summary>
	/// Same as <see cref="MustBelongToUser{T}"/>, but for optional references (e.g. a category's
	/// optional parent) — a <c>null</c> value is always valid; only a non-null value is checked.
	/// </summary>
	public static IRuleBuilderOptions<T, Guid?> MustBelongToUserWhenSpecified<T>(
		this IRuleBuilder<T, Guid?> ruleBuilder,
		Func<Guid, Guid, CancellationToken, Task<bool>> existsForUserAsync,
		Func<T, Guid> userIdSelector,
		string entityName)
	{
		return ruleBuilder.MustAsync(predicate: async (instance, id, ct) => id is null || await existsForUserAsync(id.Value, userIdSelector(instance), ct))
			.WithMessage(errorMessage: $"The {entityName} was not found.");
	}
}