using System.Diagnostics;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentry;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Extensions;

namespace XFramework.Integration.Services;

/// <summary>
/// Simple command/query dispatcher for Vertical Slice Architecture
/// Replaces MediatR with direct handler resolution and includes validation/error handling pipeline
/// </summary>
public interface ICommandQueryDispatcher
{
    Task<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) where TResponse : IBaseResponse;
    Task<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) where TResponse : IBaseResponse;
}

public class CommandQueryDispatcher(
    IServiceProvider serviceProvider,
    ILogger<CommandQueryDispatcher> logger,
    IHostEnvironment env) : ICommandQueryDispatcher
{
    public async Task<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        where TResponse : IBaseResponse
    {
        return await ExecuteWithPipeline<TResponse>(command, command.GetType(), typeof(TResponse), cancellationToken);
    }

    public async Task<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
        where TResponse : IBaseResponse
    {
        return await ExecuteWithPipeline<TResponse>(query, query.GetType(), typeof(TResponse), cancellationToken);
    }

    private async Task<TResponse> ExecuteWithPipeline<TResponse>(object request, Type requestType, Type responseType, CancellationToken cancellationToken)
        where TResponse : IBaseResponse
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            using var scope = serviceProvider.CreateScope();
            
            // Validate request
            await ValidateRequest(request, requestType, scope);
            
            // Determine handler type
            Type handlerInterfaceType;
            if (request is ICommand<TResponse>)
            {
                handlerInterfaceType = typeof(ICommandHandler<,>).MakeGenericType(requestType, responseType);
            }
            else if (request is IQuery<TResponse>)
            {
                handlerInterfaceType = typeof(IQueryHandler<,>).MakeGenericType(requestType, responseType);
            }
            else
            {
                throw new InvalidOperationException($"Request type {requestType.Name} does not implement ICommand or IQuery");
            }
            
            // Resolve and invoke handler
            var handler = scope.ServiceProvider.GetRequiredService(handlerInterfaceType);
            var handleMethod = handlerInterfaceType.GetMethod("Handle");
            var task = (Task<TResponse>)handleMethod!.Invoke(handler, new[] { request, cancellationToken })!;
            var response = await task;
            
            // Post-process response
            stopwatch.Stop();
            
            if (string.IsNullOrEmpty(response.Message))
            {
                response.Message = nameof(HttpStatusCode.Accepted);
            }

            if (response.HttpStatusCode == 0)
            {
                response.HttpStatusCode = HttpStatusCode.Accepted;
            }
            
            logger.LogInformation("Invoked {HandlerName} in {ResponseTime}ms with response: {StatusCode}:{Message}",
                requestType.GetTypeFullName(),
                stopwatch.ElapsedMilliseconds,
                response.HttpStatusCode,
                response.Message);
            
            return response;
        }
        catch (ValidationException e)
        {
            stopwatch.Stop();
            return HandleValidationException<TResponse>(e);
        }
        catch (Exception e)
        {
            stopwatch.Stop();
            return HandleException<TResponse>(e);
        }
    }

    private async Task ValidateRequest(object request, Type requestType, IServiceScope scope)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(requestType);
        var validators = scope.ServiceProvider.GetServices(validatorType);
        
        if (!validators.Any()) return;

        var contextType = typeof(ValidationContext<>).MakeGenericType(requestType);
        var context = Activator.CreateInstance(contextType, request);

        var failures = validators
            .Select(v => (IEnumerable<FluentValidation.Results.ValidationFailure>)validatorType
                .GetMethod("Validate", new[] { contextType })!
                .Invoke(v, new[] { context })!
                .GetType()
                .GetProperty("Errors")!
                .GetValue(v))
            .SelectMany(x => x)
            .Where(x => x != null)
            .ToList();

        if (failures.Any())
        {
            throw new ValidationException(string.Join("; ", failures.Select(i => i.ErrorMessage)));
        }

        await Task.CompletedTask;
    }

    private TResponse HandleValidationException<TResponse>(ValidationException e) where TResponse : IBaseResponse
    {
        var responseInstance = Activator.CreateInstance<TResponse>();
        responseInstance.Message = e.Message;
        responseInstance.HttpStatusCode = HttpStatusCode.BadRequest;
        
        logger.LogError("Validation Error: {Message}", e.Message);

        return responseInstance;
    }

    private TResponse HandleException<TResponse>(Exception e) where TResponse : IBaseResponse
    {
        var responseInstance = Activator.CreateInstance<TResponse>();

        responseInstance.Message = env.IsProduction()
            ? "An error occurred while processing your request, please try again later"
            : $"Error: {e.Message}; {(e.InnerException is not null ? $"Inner Exception: {e.InnerException?.Message}" : string.Empty)}";
        
        responseInstance.HttpStatusCode = HttpStatusCode.InternalServerError;
        
        logger.LogError("Error: {Message}; Inner Exception: {InnerException}; Stack Trace: {StackTrace}",
            e.Message,
            e.InnerException?.Message,
            e.StackTrace);

        if (env.IsProduction())
        {
            SentrySdk.CaptureException(e);
        }
                
        return responseInstance;
    }
}

/// <summary>
/// Handler interfaces for commands
/// </summary>
public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<TResponse> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Handler interfaces for queries
/// </summary>
public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<TResponse> Handle(TQuery query, CancellationToken cancellationToken);
}