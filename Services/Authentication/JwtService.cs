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

        public string GenerateToken(User user, bool rememberMe = false)
        {
            var secretKey = _jwtSettings.SecretKey;
            var issuer = _jwtSettings.Issuer;
            var audience = _jwtSettings.Audience;
            var expiryMinutes = rememberMe ? _jwtSettings.RememberMeExpiryMinutes : _jwtSettings.ExpiryMinutes;

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

        public ClaimsPrincipal? ValidateToken(string? token, bool validateLifetime = false)
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

        public async Task<TokenResponseDto?> RefreshTokenAsync(string? accessToken, string refreshToken)
        {
            Guid userId;

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                // Cho phép access token hết hạn nhưng vẫn phải parse được claim
                var principal = ValidateToken(accessToken, validateLifetime: false);
                if (principal == null) return null;

                var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdClaim, out userId)) return null;
            }
            else
            {
                // Nếu accessToken bị null thì tìm user theo refresh token
                var userByToken = await _userRepository.GetByRefreshTokenAsync(refreshToken);
                if (userByToken == null || userByToken.RefreshTokenExpiryTime < DateTime.UtcNow)
                    return null;

                userId = userByToken.Id;
            }

            // Tìm user từ ID
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || user.RefreshToken != refreshToken || user.RefreshTokenExpiryTime < DateTime.UtcNow)
            {
                return null;
            }

            // Check if user is active/blocked
            if (user.IsActive == false)
            {
                _logger.LogWarning("Refresh token denied for blocked user: {UserId}", userId);
                return null;
            }

            // Tạo mới token và refresh token
            var newAccessToken = GenerateToken(user);
            var newRefreshToken = GenerateRefreshToken();
            var newExpiry = GetRefreshTokenExpiryTime();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = newExpiry;
            user.LastLogin = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);

            // Nếu accessToken cũ còn tồn tại → thêm vào danh sách blacklist
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(accessToken);
                var expDate = jwtToken.ValidTo;

                await _loggedOutTokenRepository.AddAsync(accessToken, expDate);
            }

            return new TokenResponseDto
            {
                Token = newAccessToken,
                RefreshToken = newRefreshToken,
                RefreshTokenExpiryTime = newExpiry
            };
        }

        public async Task<TokenResponseDto> Authenticate(string email, string password, bool rememberMe = false)
        {
            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                if (!user.EmailConfirmed) throw new InvalidOperationException("Email not verified");
                
                // Check if user is active/blocked
                if (user.IsActive == false)
                {
                    throw new UnauthorizedAccessException("Your account has been blocked. Please contact support.");
                }

                var token = GenerateToken(user, rememberMe);
                var refreshToken = GenerateRefreshToken();
                var refreshExpiry = GetRefreshTokenExpiryTime();

                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = refreshExpiry;
                user.LastLogin = DateTime.UtcNow;

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
                // Nếu email đã được xác thực, không cho phép đăng ký lại
                if (existingUser.EmailConfirmed)
                {
                    return null;
                }

                // Nếu email chưa được xác thực và token verification đã hết hạn
                // hoặc không có token verification, cho phép đăng ký lại
                if (!existingUser.EmailConfirmed && 
                    (existingUser.EmailVerificationExpiry == null || 
                     existingUser.EmailVerificationExpiry < DateTime.UtcNow))
                {
                    // Xóa user cũ chưa được verify để tạo mới
                    await _userRepository.DeleteAsync(existingUser.Id);
                }
                else
                {
                    // Email chưa verify nhưng token vẫn còn hạn
                    throw new InvalidOperationException("Email already registered but not verified. Please check your email or wait for the verification link to expire before registering again.");
                }
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
                ProfilePictureUrl = "https://inkythuatso.com/uploads/thumbnails/800/2023/03/3-anh-dai-dien-trang-inkythuatso-03-15-25-56.jpg"
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

            var baseUrl = GetFrontendBaseUrl();
            var resetLink = $"{baseUrl}/reset-password?email={HttpUtility.UrlEncode(email)}&token={HttpUtility.UrlEncode(token)}";

            _logger.LogInformation("Sending password reset to {Email} with link: {ResetLink}", email, resetLink);
            await _emailService.SendPasswordResetEmailAsync(email, resetLink);
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

            var baseUrl = GetFrontendBaseUrl();
            var verifyLink = $"{baseUrl}/verify-email?email={HttpUtility.UrlEncode(user.Email)}&token={HttpUtility.UrlEncode(token)}";

            _logger.LogInformation("Sending email verification to {Email} with link: {VerifyLink}", user.Email, verifyLink);
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

        private string GetFrontendBaseUrl()
        {
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
            var baseUrl = _configuration[$"FrontendSettings:{environment}:BaseUrl"] ?? "https://localhost:7045";
            
            _logger.LogDebug("Environment: {Environment}, Frontend BaseUrl: {BaseUrl}", environment, baseUrl);
            return baseUrl;
        }
    }
}
