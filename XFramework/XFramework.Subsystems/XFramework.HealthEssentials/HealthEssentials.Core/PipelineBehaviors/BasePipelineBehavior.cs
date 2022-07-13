﻿using FluentValidation;
using Sentry;
using TypeSupport.Extensions;

namespace HealthEssentials.Core.PipelineBehaviors
{
    public class BasePipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;
        private TResponse _response;

        public BasePipelineBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
        {
            try
            {
                // Pre Validation
                await PreValidation(request);
                // Post Validation
            
                // Pre Handler
                _response = await next();
                // Post Handler
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
                
                return (TResponse) responseInstance;
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
                if (_response.ContainsProperty("Request"))
                {
                    _response.SetPropertyValue("Request", request); 
                }
            }

            if (_response.ContainsProperty("Message"))
            {
                if (_response.GetPropertyValue("Message") == null)
                {
                    _response.SetPropertyValue("Message", $"{HttpStatusCode.Accepted}");
                }
            }
        
            if (_response.ContainsProperty("HttpStatusCode"))
            {
                if (_response.GetPropertyValue("HttpStatusCode")?.ToString() == "0")
                {
                    _response.SetPropertyValue("HttpStatusCode", HttpStatusCode.Accepted);
                }
            }

            if (_response.ContainsProperty("IsSuccess"))
            {
                if (_response.ContainsProperty("HttpStatusCode"))
                {
                    if (_response.GetPropertyValue("HttpStatusCode")?.ToString() == $"{HttpStatusCode.Accepted}")
                    {
                        _response.SetPropertyValue("IsSuccess", true);
                    }
                }
            }
            
            await Task.FromResult(_response);
        }
    }
}
