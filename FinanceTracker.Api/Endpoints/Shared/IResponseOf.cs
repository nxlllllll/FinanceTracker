namespace FinanceTracker.Api.Endpoints.Shared;

/// <summary>
/// Declares that <typeparamref name="TResponse"/> is the HTTP
/// projection of <typeparamref name="TReadModel"/>.
/// </summary>
public interface IResponseOf<in TReadModel, out TResponse> where TResponse : IResponseOf<TReadModel, TResponse>
{
	static abstract TResponse FromReadModel(TReadModel readModel);
}
