namespace FinanceTracker.Core.Exceptions;

/// <summary>
/// Declares code for an <see cref="AppException"/> subclass (e.g. "account.insufficient_funds")
/// </summary>
[AttributeUsage(validOn: AttributeTargets.Class, Inherited = false)]
public sealed class ErrorCodeAttribute(string code) : Attribute
{
	public string Code { get; } = code;
}
