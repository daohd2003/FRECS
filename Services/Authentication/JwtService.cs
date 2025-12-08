using BusinessObject.DTOs.Login;
using BusinessObject.Models;
using BusinessObject.Utilities;
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

        /// <summary>
        /// Tạo refresh token ngẫu nhiên để duy trì phiên đăng nhập
        /// Refresh token được sử dụng để lấy access token mới khi access token hết hạn
        /// </summary>
        /// <returns>Chuỗi Base64 64 bytes ngẫu nhiên</returns>
        public string GenerateRefreshToken()
        {
            // Tạo 64 bytes ngẫu nhiên và chuyển thành chuỗi Base64
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        /// <summary>
        /// Tạo JWT access token chứa thông tin xác thực người dùng
        /// Token này được gửi kèm trong mỗi request để xác thực và phân quyền
        /// </summary>
        /// <param name="user">Thông tin người dùng cần tạo token</param>
        /// <param name="rememberMe">Nếu true, token có thời gian sống lâu hơn (7 ngày thay vì 60 phút)</param>
        /// <returns>Chuỗi JWT token</returns>
        public string GenerateToken(User user, bool rememberMe = false)
        {
            // Lấy cấu hình JWT từ appsettings.json
            var secretKey = _jwtSettings.SecretKey;
            var issuer = _jwtSettings.Issuer;
            var audience = _jwtSettings.Audience;
            // Thời gian hết hạn: 7 ngày nếu "Remember Me", 60 phút nếu không
            var expiryMinutes = rememberMe ? _jwtSettings.RememberMeExpiryMinutes : _jwtSettings.ExpiryMinutes;

            // Tạo claims (thông tin người dùng) để nhúng vào token
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), // User ID
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // Token ID duy nhất
                new Claim("email", user.Email), // Email
                new Claim(ClaimTypes.Role, user.Role.ToString()) // Role (customer, provider, admin, staff)
            };

            // Tạo khóa bí mật để ký token
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var cred = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Thời điểm token hết hạn
            var expiryTime = DateTime.UtcNow.AddMinutes(expiryMinutes);

            // Tạo JWT token với các thông tin trên
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiryTime,
                signingCredentials: cred
                );
            
            // Chuyển token thành chuỗi để trả về
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
            user.LastLogin = DateTimeHelper.GetVietnamTime();
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

        /// <summary>
        /// Xác thực người dùng bằng email và password
        /// Kiểm tra thông tin đăng nhập, trạng thái tài khoản và tạo token nếu hợp lệ
        /// </summary>
        /// <param name="email">Email đăng nhập</param>
        /// <param name="password">Mật khẩu (plain text)</param>
        /// <param name="rememberMe">Có duy trì đăng nhập lâu dài không</param>
        /// <returns>TokenResponseDto chứa access token, refresh token và thông tin role</returns>
        /// <exception cref="InvalidOperationException">Email chưa được xác thực</exception>
        /// <exception cref="UnauthorizedAccessException">Tài khoản bị khóa hoặc thông tin đăng nhập sai</exception>
        public async Task<TokenResponseDto> Authenticate(string email, string password, bool rememberMe = false)
        {
            // Tìm user theo email
            var user = await _userRepository.GetUserByEmailAsync(email);
            
            // Kiểm tra user tồn tại và password đúng (so sánh hash)
            if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                // Kiểm tra email đã được xác thực chưa
                if (!user.EmailConfirmed) throw new InvalidOperationException("Email not verified");
                
                // Kiểm tra tài khoản có bị khóa không
                if (user.IsActive == false)
                {
                    throw new UnauthorizedAccessException("Your account has been blocked. Please contact support.");
                }

                // Tạo access token và refresh token
                var token = GenerateToken(user, rememberMe);
                var refreshToken = GenerateRefreshToken();
                var refreshExpiry = GetRefreshTokenExpiryTime();

                // Lưu refresh token vào database để sử dụng sau này
                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = refreshExpiry;
                user.LastLogin = DateTimeHelper.GetVietnamTime(); // Cập nhật thời gian đăng nhập cuối

                await _userRepository.UpdateAsync(user);

                // Trả về thông tin token cho client
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
                // Email không tồn tại hoặc password sai
                throw new UnauthorizedAccessException("Invalid email or password");
            }
        }

        /// <summary>
        /// Đăng xuất người dùng bằng cách thêm token vào blacklist
        /// Token sẽ không thể sử dụng lại cho đến khi hết hạn
        /// </summary>
        /// <param name="token">Access token cần vô hiệu hóa</param>
        public async Task LogoutAsync(string token)
        {
            // Parse token để lấy thời gian hết hạn
            var tokenHander = new JwtSecurityTokenHandler();
            var jwtToken = tokenHander.ReadJwtToken(token);
            var expDate = jwtToken.ValidTo;

            // Thêm token vào bảng LoggedOutTokens (blacklist)
            // Token này sẽ bị từ chối khi validate trong middleware
            await _loggedOutTokenRepository.AddAsync(token, expDate);
        }

        public async Task<bool> IsTokenValidAsync(string token)
        {
            return !await _loggedOutTokenRepository.IsTokenLoggedOutAsync(token);
        }

        /// <summary>
        /// Đăng ký tài khoản mới cho người dùng
        /// Xử lý logic: kiểm tra email trùng, tạo user mới, hash password, tạo profile mặc định, gửi email xác thực
        /// </summary>
        /// <param name="request">Thông tin đăng ký (email, password, fullname)</param>
        /// <returns>TokenResponseDto nếu thành công, null nếu email đã tồn tại và đã xác thực</returns>
        /// <exception cref="InvalidOperationException">Email đã đăng ký nhưng chưa xác thực và token còn hạn</exception>
        public async Task<TokenResponseDto?> RegisterAsync(RegisterRequest request)
        {
            // Kiểm tra email đã tồn tại chưa
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

            // Tạo user mới với password đã hash
            var newUser = new User
            {
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password), // Hash password bằng BCrypt
                IsActive = true // Tài khoản mặc định là active
            };

            // Tạo profile mặc định cho user (quan hệ 1-1)
            newUser.Profile = new Profile
            {
                FullName = request.FullName ?? "",
                ProfilePictureUrl = "https://res.cloudinary.com/dtzg1vs7r/image/upload/v1765160862/t%E1%BA%A3i_xu%E1%BB%91ng_zhflev.jpg" // Avatar mặc định
            };

            // Tạo token để user có thể đăng nhập ngay (nhưng chưa xác thực email)
            var accessToken = GenerateToken(newUser);
            var refreshToken = GenerateRefreshToken();
            var refreshExpiry = GetRefreshTokenExpiryTime();

            // Lưu refresh token vào user
            newUser.RefreshToken = refreshToken;
            newUser.RefreshTokenExpiryTime = refreshExpiry;

            // Lưu user vào database
            await _userRepository.AddAsync(newUser);

            // Gửi email xác thực đến user
            await SendEmailVerificationAsync(newUser.Id);

            // Trả về token để user có thể sử dụng hệ thống (một số chức năng yêu cầu xác thực email)
            return new TokenResponseDto
            {
                Token = accessToken,
                RefreshToken = refreshToken,
                RefreshTokenExpiryTime = refreshExpiry,
                Role = newUser.Role.ToString()
            };
        }

        /// <summary>
        /// Đổi mật khẩu cho người dùng đã đăng nhập
        /// Yêu cầu xác thực mật khẩu hiện tại trước khi đổi
        /// </summary>
        /// <param name="userId">ID người dùng</param>
        /// <param name="currentPassword">Mật khẩu hiện tại (để xác thực)</param>
        /// <param name="newPassword">Mật khẩu mới</param>
        /// <returns>true nếu đổi thành công, false nếu user không tồn tại hoặc mật khẩu hiện tại sai</returns>
        public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
        {
            // Tìm user theo ID
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            // Xác thực mật khẩu hiện tại
            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
                return false;

            // Hash mật khẩu mới và cập nhật
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _userRepository.UpdateAsync(user);
            return true;
        }

        /// <summary>
        /// Xử lý yêu cầu quên mật khẩu
        /// Tạo token reset password và gửi link đặt lại mật khẩu qua email
        /// </summary>
        /// <param name="email">Email của người dùng quên mật khẩu</param>
        /// <returns>true nếu gửi email thành công, false nếu email không tồn tại</returns>
        public async Task<bool> ForgotPasswordAsync(string email)
        {
            // Tìm user theo email
            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null) return false;

            // Tạo token reset password (có thời hạn 30 phút)
            var token = GenerateTokenString();
            user.PasswordResetToken = token;
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30);
            await _userRepository.UpdateAsync(user);

            // Tạo link reset password với token
            var baseUrl = GetFrontendBaseUrl();
            var resetLink = $"{baseUrl}/reset-password?email={HttpUtility.UrlEncode(email)}&token={HttpUtility.UrlEncode(token)}";

            // Gửi email chứa link reset password
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

        /// <summary>
        /// Gửi email xác thực tài khoản cho người dùng mới đăng ký
        /// Email chứa link xác thực có thời hạn 1 giờ
        /// </summary>
        /// <param name="userId">ID người dùng cần xác thực email</param>
        /// <returns>true nếu gửi thành công, false nếu user không tồn tại hoặc đã xác thực</returns>
        public async Task<bool> SendEmailVerificationAsync(Guid userId)
        {
            // Tìm user và kiểm tra đã xác thực chưa
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || user.EmailConfirmed) return false;

            // Tạo token xác thực email (có thời hạn 1 giờ)
            var token = GenerateTokenString();
            user.EmailVerificationToken = token;
            user.EmailVerificationExpiry = DateTime.UtcNow.AddHours(1);
            await _userRepository.UpdateAsync(user);

            // Tạo link xác thực email
            var baseUrl = GetFrontendBaseUrl();
            var verifyLink = $"{baseUrl}/verify-email?email={HttpUtility.UrlEncode(user.Email)}&token={HttpUtility.UrlEncode(token)}";

            // Gửi email xác thực
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
