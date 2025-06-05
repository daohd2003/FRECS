using BusinessObject.DTOs.Login;
using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Services.Authentication
{
    public interface IJwtService
    {
        string GenerateToken(User user);
        string GenerateRefreshToken();
        ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = false);
        DateTime GetRefreshTokenExpiryTime();
        Task<TokenResponseDto> Authenticate(string email, string password);
        Task<TokenResponseDto?> RefreshTokenAsync(string accessToken, string refreshToken);
        Task LogoutAsync(string token);
        Task<bool> IsTokenValidAsync(string token);
        Task<TokenResponseDto?> RegisterAsync(RegisterRequest request);
        Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
        Task<bool> ForgotPasswordAsync(string email);
        Task<bool> ResetPasswordAsync(string email, string token, string newPassword);
        Task<bool> SendEmailVerificationAsync(Guid userId);
        Task<bool> ConfirmEmailAsync(string email, string token);

    }
}
