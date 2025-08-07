namespace InterviewProject.Common
{
    public static class ExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseCustomExceptionMiddleware(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ExceptionMiddleware>();
        }
    }
}
