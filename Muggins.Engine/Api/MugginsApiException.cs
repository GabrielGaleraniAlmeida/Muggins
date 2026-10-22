using System.Net;

namespace Muggins.Engine.Api;

public sealed class MugginsApiException : HttpRequestException
{
    public RespostaErroDto? ApiError { get; }
    public string? RawResponse { get; }

    public MugginsApiException(
        HttpStatusCode statusCode,
        RespostaErroDto? apiError,
        string? rawResponse)
        : base(
            apiError?.Codigo is { Length: > 0 } code
                ? $"Muggins API returned {(int)statusCode} ({statusCode}): {code}."
                : $"Muggins API returned {(int)statusCode} ({statusCode}).",
            inner: null,
            statusCode)
    {
        ApiError = apiError;
        RawResponse = rawResponse;
    }
}
