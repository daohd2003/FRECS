using BusinessObject.DTOs.Login;
using BusinessObject.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Repositories.Logout;
using Repositories.UserRepositories;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Web;
using Microsoft.Extensions.Configuration;
using Services.EmailServices;
namespace Services.Authentication
{
    public class JwtService : IJwtService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly IUserRepository _userRepository;
        private readonly ILoggedOutTokenRepository _loggedOutTokenRepository;
        private readonly ILogger<JwtService> _logger;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public JwtService(
            IOptions<JwtSettings> jwtSettings,
            IUserRepository userRepository,
            ILogger<JwtService> logger,
            ILoggedOutTokenRepository loggedOutTokenRepository,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _jwtSettings = jwtSettings.Value;
            _userRepository = userRepository;
            _logger = logger;
            _loggedOutTokenRepository = loggedOutTokenRepository;
            _emailService = emailService;
            _configuration = configuration;
        }

        public string GenerateRefreshToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        public string GenerateToken(User user)
        {
            var secretKey = _jwtSettings.SecretKey;
            var issuer = _jwtSettings.Issuer;
            var audience = _jwtSettings.Audience;
            var expiryMinutes = _jwtSettings.ExpiryMinutes;

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("email", user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var cred = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expiryTime = DateTime.UtcNow.AddMinutes(expiryMinutes);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiryTime,
                signingCredentials: cred
                );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public DateTime GetRefreshTokenExpiryTime()
        {
            return DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays);
        }

        public ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = false)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Token validation failed: Token is null or empty");
                return null;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);

            try
            {
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtSettings.Issuer,

                    ValidateAudience = true,
                    ValidAudience = _jwtSettings.Audience,

                    ValidateLifetime = false,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                // Đảm bảo token là JWT
                if (validatedToken is not JwtSecurityToken jwtToken)
                {
                    _logger.LogWarning("Token validation failed: Token is not a valid JWT");
                    return null;
                }

                // (Optional) Kiểm tra thuật toán ký có khớp không (nâng cao bảo mật)
                if (!jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    _logger.LogWarning("Token validation failed: Invalid signing algorithm");
                    return null;
                }

                return principal;
            }
            catch (SecurityTokenExpiredException ex)
            {
                _logger.LogWarning("Token validation failed: Token has expired");
                _logger.LogWarning($"Expired at: {ex.Expires} | Now: {DateTime.UtcNow}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Token validation failed: {ex.Message}");
                return null;
            }
        }

        public async Task<TokenResponseDto?> RefreshTokenAsync(string accessToken, string refreshToken)
        {
            var principal = ValidateToken(accessToken, validateLifetime: false);
            if (principal == null) return null;

            var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out Guid userId)) return null;

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || user.RefreshToken != refreshToken || user.RefreshTokenExpiryTime < DateTime.UtcNow)
            {
                return null;
            }

            var newAccessToken = GenerateToken(user);
            var newRefreshToken = GenerateRefreshToken();
            var newExpiry = GetRefreshTokenExpiryTime();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = newExpiry;
            await _userRepository.UpdateAsync(user);

            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(accessToken);
            var expDate = jwtToken.ValidTo;

            await _loggedOutTokenRepository.AddAsync(accessToken, expDate);

            return new TokenResponseDto
            {
                Token = newAccessToken,
                RefreshToken = newRefreshToken,
                RefreshTokenExpiryTime = newExpiry
            };
        }

        public async Task<TokenResponseDto> Authenticate(string email, string password)
        {
            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                if (!user.EmailConfirmed) throw new Exception("Email chưa được xác minh");

                var token = GenerateToken(user);
                var refreshToken = GenerateRefreshToken();
                var refreshExpiry = GetRefreshTokenExpiryTime();

                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = refreshExpiry;

                await _userRepository.UpdateAsync(user);

                return new TokenResponseDto
                {
                    Token = token,
                    RefreshToken = refreshToken,
                    RefreshTokenExpiryTime = refreshExpiry,
                    Role = user.Role.ToString()
                };
            }
            else
            {
                throw new UnauthorizedAccessException("Invalid email or password");
            }
        }

        public async Task LogoutAsync(string token)
        {
            var tokenHander = new JwtSecurityTokenHandler();
            var jwtToken = tokenHander.ReadJwtToken(token);
            var expDate = jwtToken.ValidTo;

            await _loggedOutTokenRepository.AddAsync(token, expDate);
        }

        public async Task<bool> IsTokenValidAsync(string token)
        {
            return !await _loggedOutTokenRepository.IsTokenLoggedOutAsync(token);
        }

        public async Task<TokenResponseDto?> RegisterAsync(RegisterRequest request)
        {
            var existingUser = await _userRepository.GetUserByEmailAsync(request.Email);

            if (existingUser != null)
            {
                return null;
            }

            var newUser = new User
            {
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                IsActive = true
            };

            // Tạo profile mặc định cho user
            newUser.Profile = new Profile
            {
                FullName = request.FullName ?? "",
                ProfilePictureUrl = "https://static-00.iconduck.com/assets.00/avatar-default-symbolic-icon-479x512-n8sg74wg.png"
            };

            var accessToken = GenerateToken(newUser);
            var refreshToken = GenerateRefreshToken();
            var refreshExpiry = GetRefreshTokenExpiryTime();

            newUser.RefreshToken = refreshToken;
            newUser.RefreshTokenExpiryTime = refreshExpiry;

            await _userRepository.AddAsync(newUser);


            await SendEmailVerificationAsync(newUser.Id);


            return new TokenResponseDto
            {
                Token = accessToken,
                RefreshToken = refreshToken,
                RefreshTokenExpiryTime = refreshExpiry,
                Role = newUser.Role.ToString()
            };
        }

        public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
                return false;

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _userRepository.UpdateAsync(user);
            return true;
        }

        public async Task<bool> ForgotPasswordAsync(string email)
        {
            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null) return false;

            var token = GenerateTokenString();
            user.PasswordResetToken = token;
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30);
            await _userRepository.UpdateAsync(user);

            var baseUrl = _configuration["Frontend:BaseUrl"];
            var resetLink = $"{baseUrl}/reset-password?email={HttpUtility.UrlEncode(email)}&token={HttpUtility.UrlEncode(token)}";

            await _emailService.SendVerificationEmailAsync(email, resetLink);
            return true;
        }

        public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword)
        {
            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null) return false;
            if (user.PasswordResetToken != token || user.PasswordResetTokenExpiry < DateTime.UtcNow) return false;

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;

            await _userRepository.UpdateAsync(user);
            return true;
        }

        public async Task<bool> SendEmailVerificationAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || user.EmailConfirmed) return false;

            var token = GenerateTokenString();
            user.EmailVerificationToken = token;
            user.EmailVerificationExpiry = DateTime.UtcNow.AddHours(1);
            await _userRepository.UpdateAsync(user);

            var baseUrl = _configuration["Frontend:BaseUrl"];
            var verifyLink = $"{baseUrl}/verify-email?email={HttpUtility.UrlEncode(user.Email)}&token={HttpUtility.UrlEncode(token)}";

            await _emailService.SendVerificationEmailAsync(user.Email, verifyLink);
            return true;
        }

        public async Task<bool> ConfirmEmailAsync(string email, string token)
        {
            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null || user.EmailConfirmed) return false;
            if (user.EmailVerificationToken != token || user.EmailVerificationExpiry < DateTime.UtcNow) return false;

            user.EmailConfirmed = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationExpiry = null;

            await _userRepository.UpdateAsync(user);
            return true;
        }

        private string GenerateTokenString()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        }
    }
}
