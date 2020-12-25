using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.BO;
using IdentityServer.Domain.Enums;
using MediatR;

namespace IdentityServer.Core.PipelineBehaviors
{
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;
        private readonly IDataLayer _dataLayer;
        private readonly IMapper _mapper;
        private TResponse _response;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators, IDataLayer dataLayer, IMapper mapper)
        {
            _validators = validators;
            _dataLayer = dataLayer;
            _mapper = mapper;
        }

        public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
        {
            // Pre Validation
            await PreValidation(request);
            // Post Validation
            
            // Create data layer transaction
            var transaction = await _dataLayer.Database.BeginTransactionAsync(cancellationToken);
            
            try
            {
                // Pre Handler
                _response = await next();
                // Post Handler

                // Commit data layer transaction
                await transaction.CommitAsync(cancellationToken);
                await PostHandler(request,_response);                

                return _response;
            }
            catch (Exception e)
            {
                if (_response.GetType() != typeof(CmdResponseBO<TRequest>)) throw new ArgumentException($"Error: {e.Message}; Inner Exception: {e.InnerException?.Message}");

                _response.GetType().GetProperty("Message")?.SetValue(_response, $"Error: {e.Message}; Inner Exception: {e.InnerException?.Message}", null);
                return _response;
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
                throw new ValidationException(failures);
            }

            await Task.FromResult(new Unit());
        }

        private async Task PostHandler(TRequest request, TResponse response)
        {
            if (_response.GetType() != typeof(CmdResponseBO<TRequest>)) return;

            _response.GetType().GetProperty("Message")?.SetValue(_response, $"Success", null);
            _response.GetType().GetProperty("HttpStatusCode")?.SetValue(_response, ApiStatus.Success, null);
            _response.GetType().GetProperty("Request")?.SetValue(_response, request, null);

            await Task.FromResult(_response);
        }
    }
}
