using System.Diagnostics;
using FluentValidation;
using Microsoft.Extensions.Logging;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Extensions;
using XFramework.Integration.PipelineBehaviours;

namespace XFramework.Blazor.Core.PipelineBehaviors;

public class BlazorPipelineBehavior<TRequest, TResponse>
    (
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<BasePipelineBehavior<TRequest, TResponse>> log,
        IHostEnvironment env,
        SweetAlertService sweetAlertService
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
        catch (ValidationException e)
        {
            return HandleValidationException(e);
        }
        catch (Exception e)
        {
            return HandleException(e);
        }
    }

    private TResponse HandleException(Exception e)
    {
        var responseInstance = Activator.CreateInstance<TResponse>();

        responseInstance.Message = env.IsProduction() 
            ? "An error occurred while processing your request, please try again later" 
            : $"Error: {e.Message}; {(e.InnerException is not null ? $"Inner Exception: {e.InnerException?.Message}" : string.Empty)}";
        
        responseInstance.HttpStatusCode = HttpStatusCode.InternalServerError;
        
        log.LogError("Error: {Message}; Inner Exception: {InnerException}; Stack Trace: {StackTrace}", 
            e.Message, 
            e.InnerException?.Message, 
            e.StackTrace
        );

        if (env.IsProduction())
        {
            SentrySdk.CaptureException(e);
        }
                
        return responseInstance;
    }
    
    private TResponse HandleValidationException(ValidationException e)
    {
        var responseInstance = Activator.CreateInstance<TResponse>();

        responseInstance.Message = e.Message;
        responseInstance.HttpStatusCode = HttpStatusCode.BadRequest;
        
        log.LogError("Validation Error: {Message}", e.Message);
        sweetAlertService.FireAsync("Error", e.Message, SweetAlertIcon.Error);

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
            throw new ValidationException(string.Join(", ", failures.Select(i => i.ErrorMessage)));
        }

        await Task.FromResult(new Unit());
        _stopwatch.Start();
    }

    private async Task HandleResponse(TRequest request, TResponse response, RequestHandlerDelegate<TResponse> next)
    {
        _stopwatch.Stop();
        log.LogInformation("Invoked {HandlerName} in {ResponseTime}ms with response: {StatusCode}:{Message}", 
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