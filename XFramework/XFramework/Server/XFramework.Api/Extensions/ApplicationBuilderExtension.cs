using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;

namespace XFramework.Api.Extensions
{
    public static class ApplicationBuilderExtension
    {
        public static void UseFluentValidationExceptionHandler(this IApplicationBuilder application) {
            application.UseExceptionHandler( x => {
                x.Run(async context => {
                    var errorFeature = context.Features.Get<IExceptionHandlerFeature>();
                    var exception = errorFeature.Error;
                    string errorText = "";
                    IEnumerable<(string, string)> errors = null;

                    if (!(exception is ValidationException validationException))
                    {
                        List<(string, string)> _error = new List<(string, string)>() {
                           ("Exception",exception.Message),("InnerException", exception.InnerException != null ? exception.InnerException.Message : "")
                        };

                        errors = _error;

                    }
                    else {
                        errors = validationException.Errors.Select(err => ( err.PropertyName, err.ErrorMessage ));
                    }
                    
                    errorText = JsonSerializer.Serialize(errors);
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    context.Response.ContentType = "application/json";

                    await context.Response.WriteAsync(errorText, Encoding.UTF8).ConfigureAwait(true);

                });
            });
        }
    }
}
