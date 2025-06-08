using BusinessObject.DTOs.ApiResponses;
using System.Net;
using System.Text.Json;

namespace ShareItAPI.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public GlobalExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            if (!context.Response.HasStarted)
            {
                HttpStatusCode statusCode = HttpStatusCode.InternalServerError;
                string message = "An unexpected error occurred.";

                if (exception is InvalidOperationException)
                {
                    statusCode = HttpStatusCode.BadRequest;
                    message = exception.Message;
                }

                var includeDetails = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

                var response = new ApiResponse<string>(
                    message,
                    includeDetails ? exception.ToString() : null
                );

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)statusCode;

                var jsonResponse = JsonSerializer.Serialize(response);

                return context.Response.WriteAsync(jsonResponse);
            }
            return Task.CompletedTask;
        }
    }
}
