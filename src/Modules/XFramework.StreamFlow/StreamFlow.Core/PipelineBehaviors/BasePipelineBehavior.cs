﻿using FluentValidation;
using MediatR;
using StreamFlow.Core.Interfaces;
using System.Net;
using Sentry;
using XFramework.Domain.Shared.BusinessObjects;

namespace StreamFlow.Core.PipelineBehaviors;

public class BasePipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private TResponse _response;

    public BasePipelineBehavior(IEnumerable<IValidator<TRequest>> validators)//, IDataLayer dataLayer)
    {
        _validators = validators;
        //_dataLayer = dataLayer;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            // Pre Validation
            await PreValidation(request);
            // Post Validation

            // Create data layer transaction
            //var transaction = await _dataLayer.Database.BeginTransactionAsync(cancellationToken);

            // Pre Handler
            _response = await next();
            // Post Handler

            // Commit data layer transaction
            //await transaction.CommitAsync(cancellationToken);
            await PostHandler(request);

            return _response;
        }
        catch (Exception e)
        {
            SentrySdk.CaptureMessage(e.ToString());
            var responseInstance = Activator.CreateInstance(next.GetType().GenericTypeArguments[0]);

            responseInstance?.GetType().GetProperty("Message")?
                .SetValue(responseInstance, $"Error: {e.Message};", null);

            if (e.InnerException != null)
            {
                responseInstance?.GetType().GetProperty("Message")?
                    .SetValue(responseInstance, $"{responseInstance?.GetType().GetProperty("Message")?.GetValue(responseInstance)} Inner Exception: {e.InnerException?.Message}", null);
            }

            responseInstance?.GetType().GetProperty("HttpStatusCode")?
                .SetValue(responseInstance, HttpStatusCode.InternalServerError, null);

            return (TResponse)responseInstance;
        }
    }

    private async Task PreValidation(TRequest request)
    {
        var context = new ValidationContext<TRequest>(request);
        var failures = _validators
            .Select(x => x.Validate(context))
            .SelectMany(x => x.Errors)
            .Where(x => x != null)
            .ToList();

        if (failures.Any())
        {
            throw new ArgumentException(string.Join("; ", failures.Select(i => i.ErrorMessage)));
        }

        await Task.FromResult(new Unit());
    }

    private async Task PostHandler(TRequest request)
    {
        if (_response.GetType() == typeof(CmdResponse<TRequest>))
        {
            _response.GetType().GetProperty("Request")?.SetValue(_response, request, null);
        }

        if (_response.GetType().GetProperty("Message")?.GetValue(_response) == null)
        {
            _response.GetType().GetProperty("Message")?.SetValue(_response, HttpStatusCode.Accepted.ToString(), null);
        }

        if (_response.GetType().GetProperty("HttpStatusCode")?.GetValue(_response)?.ToString() == "0")
        {
            _response.GetType().GetProperty("HttpStatusCode")?.SetValue(_response, HttpStatusCode.Accepted, null);
        }
            
        await Task.FromResult(_response);
    }
}