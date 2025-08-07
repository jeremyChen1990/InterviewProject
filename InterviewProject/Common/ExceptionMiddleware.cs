using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace InterviewProject.Common
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            int statusCode;
            object response;

            switch (exception)
            {
                case ArgumentOutOfRangeException argumentOut:
                    statusCode = StatusCodes.Status400BadRequest;
                    response = new { status = statusCode, message = argumentOut.Message};
                    break;

                case ArgumentException argument:
                    statusCode = StatusCodes.Status400BadRequest;
                    response = new { status = statusCode, message = argument.Message };
                    break;

                case NotFoundException notFound:
                    statusCode = StatusCodes.Status404NotFound;
                    response = new { status = statusCode, message = notFound.Message};
                    break;

                default:
                    statusCode = StatusCodes.Status500InternalServerError;
                    response = _env.IsDevelopment()
                        ? new { status = statusCode, message = exception.Message, stackTrace = exception.StackTrace }
                        : new { status = statusCode, message = "internal server error." };
                    break;
            }

            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(response);
        }
    }

}
