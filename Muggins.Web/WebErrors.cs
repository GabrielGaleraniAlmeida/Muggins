using System.Net;
using Muggins.Engine.Api;

namespace Muggins.Web;

public sealed class WebRequestException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public static class WebErrors
{
    public static IResult ToResult(Exception exception)
    {
        var (status, message) = exception switch
        {
            WebRequestException requestException => (requestException.StatusCode, requestException.Message),
            MugginsApiException apiException => ((int)(apiException.StatusCode ?? HttpStatusCode.BadGateway),
                apiException.ApiError?.Codigo ?? "A API recusou a operação."),
            HttpRequestException => (StatusCodes.Status502BadGateway,
                "Não foi possível alcançar a API de Dominó. Tente novamente em instantes."),
            ArgumentException argumentException => (StatusCodes.Status400BadRequest, argumentException.Message),
            InvalidOperationException invalidOperationException => (StatusCodes.Status409Conflict, invalidOperationException.Message),
            _ => (StatusCodes.Status500InternalServerError, "Ocorreu um erro interno ao processar a operação.")
        };

        return Results.Json(new WebError(message), statusCode: status);
    }
}
