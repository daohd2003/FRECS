using Services.Authentication;
using Repositories.UserRepositories;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ShareItAPI.Middlewares
{
    public class TokenValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _scopeFactory;

        public TokenValidationMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory)
        {
            _next = next;
            _scopeFactory = scopeFactory;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            using var scope = _scopeFactory.CreateScope();
            var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            try
            {
                // Skip validation for public endpoints
                if (context.Request.Path.StartsWithSegments("/api/auth/login") ||
                    context.Request.Path.StartsWithSegments("/api/auth/register") ||
                    context.Request.Path.StartsWithSegments("/api/auth/forgot-password") ||
                    context.Request.Path.StartsWithSegments("/api/auth/reset-password") ||
                    context.Request.Path.StartsWithSegments("/api/auth/confirm-email") ||
                    context.Request.Path.StartsWithSegments("/api/auth/verify-email") ||
                    context.Request.Path.StartsWithSegments("/api/auth/google-login") ||
                    context.Request.Path.StartsWithSegments("/api/auth/facebook-login") ||
                    context.Request.Path.StartsWithSegments("/swagger") ||
                    context.Request.Path.StartsWithSegments("/odata"))
                {
                    await _next(context);
                    return;
                }

                var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

                if (!string.IsNullOrEmpty(token))
                {
                    // Check if token is blacklisted (logged out)
                    if (!await jwtService.IsTokenValidAsync(token))
                    {
                        throw new UnauthorizedAccessException("Token has been logged out");
                    }

                    // Extract user ID from token and check if user is active
                    var tokenHandler = new JwtSecurityTokenHandler();
                    if (tokenHandler.CanReadToken(token))
                    {
                        var jwtToken = tokenHandler.ReadJwtToken(token);
                        var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

                        if (Guid.TryParse(userIdClaim, out var userId))
                        {
                            var user = await userRepository.GetByIdAsync(userId);
                            
                            // Check if user exists and is active
                            if (user == null)
                            {
                                throw new UnauthorizedAccessException("User not found");
                            }

                            if (user.IsActive == false)
                            {
                                throw new UnauthorizedAccessException("Your account has been blocked. Please contact support.");
                            }
                        }
                    }
                }

                await _next(context);
            }
            catch (UnauthorizedAccessException ex)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }
    }
}
