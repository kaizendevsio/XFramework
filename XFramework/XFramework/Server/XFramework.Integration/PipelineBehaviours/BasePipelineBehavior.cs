using System.Diagnostics;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Serilog;
using XFramework.Domain.Generic.Contracts.Base;
using XFramework.Integration.Extensions;

namespace XFramework.Integration.PipelineBehaviours;

public class BasePipelineBehavior<TRequest, TResponse>
    (
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<BasePipelineBehavior<TRequest, TResponse>> log
    )
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IBaseResponse
{
    private TResponse _response;
    private Stopwatch _stopwatch = new();
    
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            // Pre Validation
            await ValidateRequest(request);
            // Post Validation
            
            // Pre Handler
            _response = await next();
            // Post Handler
            await HandleResponse(request, _response, next);                

            return _response;
        }
        catch (Exception e)
        {
            return HandleError(e);
        }
    }

    private static TResponse HandleError(Exception e)
    {
        var responseInstance = Activator.CreateInstance<TResponse>();
                
        responseInstance.Message = $"Error: {e.Message}; {(e.InnerException is not null ? $"Inner Exception: {e.InnerException?.Message}" : string.Empty)}";
        responseInstance.HttpStatusCode = HttpStatusCode.InternalServerError;
                
        return responseInstance;
    }

    private async Task ValidateRequest(TRequest request)
    {
        var context = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(x => x.Validate(context))
            .SelectMany(x => x.Errors)
            .Where(x => x != null)
            .ToList();

        if (failures.Any())
        {
            throw new ArgumentException(string.Join("; ", failures.Select(i => i.ErrorMessage)));
        }

        await Task.FromResult(new Unit());
        _stopwatch.Start();
    }

    private async Task HandleResponse(TRequest request, TResponse response, RequestHandlerDelegate<TResponse> next)
    {
        _stopwatch.Stop();
        log.LogInformation("Invoked {HandlerName} in {ElapsedTime}ms with response: {StatusCode}:{Message}", 
            typeof(TRequest).GetTypeFullName(), 
            _stopwatch.ElapsedMilliseconds,
            response.HttpStatusCode,
            response.Message
            );


        if (string.IsNullOrEmpty(response.Message))
        {
            response.Message = nameof(HttpStatusCode.Accepted);
        }

        if (response.HttpStatusCode is 0)
        {
            response.HttpStatusCode = HttpStatusCode.Accepted;
        }
            
        await Task.FromResult(_response);
    }
}