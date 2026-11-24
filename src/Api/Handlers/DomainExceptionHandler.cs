using Core;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Handlers;

public sealed class DomainExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Порушення бізнес-правила (Conflict)",
            Detail = domainException.Message,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10"
        };

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = domainException,
            ProblemDetails = problemDetails
        });
    }
}