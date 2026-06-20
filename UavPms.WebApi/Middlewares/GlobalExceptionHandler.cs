using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using UavPms.WebApi.Controllers;

namespace UavPms.WebApi.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;   
    }
    
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        ApiResponse apiResponse;

        if (exception is ValidationException validationException)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            
            var errors = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select( e => e.ErrorMessage).ToArray()
                );

            apiResponse = new ApiResponse(
                Success: false,
                Message: "One or more validation errors occurred.",
                Data: null,
                Errors: errors
            );
        }
        else
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            apiResponse = new ApiResponse(
                Success: false,
                Message: exception.Message,
                Data: null,
                Errors: null
            );
        }

        await httpContext.Response.WriteAsJsonAsync(apiResponse, cancellationToken);
        
        return true;
    }
}
