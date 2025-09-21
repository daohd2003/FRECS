using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.Login;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.Authentication;
using Services.UserServices;
using Services.Utilities;

namespace ShareItAPI.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IJwtService _jwtService;
        private readonly IUserService _userService;
        private readonly GoogleAuthService _googleAuthService;
        private readonly FacebookAuthService _facebookAuthService;

        public AuthController(IJwtService jwtService, IUserService userService, GoogleAuthService googleAuthService, FacebookAuthService facebookAuthService)
        {
            _jwtService = jwtService;
            _userService = userService;
            _googleAuthService = googleAuthService;
            _facebookAuthService = facebookAuthService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                var tokenResponse = await _jwtService.Authenticate(request.Email, request.Password, request.RememberMe);
                return Ok(new ApiResponse<TokenResponseDto>("Login successful", tokenResponse));
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new ApiResponse<string>("Invalid email or password", null));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Email not verified"))
            {
                return BadRequest(new ApiResponse<string>("Login failed. Please verify your email to continue.", null));
            }
        }

        [HttpPost("facebook-login")]
        public async Task<IActionResult> FacebookLogin([FromBody] FacebookLoginRequestDto request)
        {
            try
            {
                var payload = await _facebookAuthService.VerifyFacebookTokenAsync(request.AccessToken);
                if (payload == null)
                {
                    return Unauthorized(new ApiResponse<string>("Invalid Facebook token", null));
                }

                var user = await _userService.GetOrCreateUserAsync(payload);
                if (user == null)
                {
                    return BadRequest(new ApiResponse<string>("Could not create or fetch user from Facebook payload", null));
                }

                var tokens = _jwtService.GenerateToken(user);
                var refreshTokens = _jwtService.GenerateRefreshToken();
                var expiryTime = _jwtService.GetRefreshTokenExpiryTime();

                user.RefreshTokenExpiryTime = expiryTime;
                user.RefreshToken = refreshTokens;
                await _userService.UpdateAsync(user);

                var response = new TokenResponseDto
                {
                    Token = tokens,
                    RefreshToken = refreshTokens,
                    RefreshTokenExpiryTime = expiryTime,
                    Role = user.Role.ToString()
                };

                return Ok(new ApiResponse<TokenResponseDto>("Facebook login successful", response));
            }
            catch (Exception ex)
            {
                return Unauthorized(new ApiResponse<string>($"Facebook authentication error: {ex.Message}", null));
            }
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequestDto request)
        {
            try
            {
                var payload = await _googleAuthService.VerifyGoogleTokenAsync(request.IdToken);
                if (payload == null)
                {
                    return Unauthorized(new ApiResponse<string>("Invalid Google token", null));
                }

                var user = await _userService.GetOrCreateUserAsync(payload);

                if (user == null)
                {
                    return BadRequest(new ApiResponse<string>("Email is already registered using traditional login", null));
                }

                var tokens = _jwtService.GenerateToken(user);
                var refreshTokens = _jwtService.GenerateRefreshToken();
                var expiryTime = _jwtService.GetRefreshTokenExpiryTime();

                user.RefreshTokenExpiryTime = expiryTime;
                user.RefreshToken = refreshTokens;

                await _userService.UpdateAsync(user);

                var response = new TokenResponseDto
                {
                    Token = tokens,
                    RefreshToken = refreshTokens,
                    RefreshTokenExpiryTime = expiryTime,
                    Role = user.Role.ToString()
                };

                return Ok(new ApiResponse<TokenResponseDto>("Google login successful", response));
            }
            catch (Exception ex)
            {
                return Unauthorized(new ApiResponse<string>($"Google authentication error: {ex.Message}", null));
            }
        }

        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
        {
            var oldAccessToken = TokenHelper.ExtractAccessToken(HttpContext);

            if (string.IsNullOrEmpty(request.RefreshToken))
                return BadRequest(new ApiResponse<string>("Refresh token is missing from the request body.", null));

            var result = await _jwtService.RefreshTokenAsync(oldAccessToken, request.RefreshToken);

            if (result == null)
                return Unauthorized(new ApiResponse<string>("Refresh token is invalid or has expired. Please log in again.", null));

            return Ok(new ApiResponse<TokenResponseDto>("Token refreshed successfully", result));
        }

        [HttpPost("log-out")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(new ApiResponse<string>("Token not found", null));
            }

            await _jwtService.LogoutAsync(token);

            return Ok(new ApiResponse<string>("Logout successful", null));
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var tokenResponse = await _jwtService.RegisterAsync(request);
            if (tokenResponse == null)
                return BadRequest(new ApiResponse<string>("Email is already registered", null));

            return Ok(new ApiResponse<TokenResponseDto>("Registration successful! Please check your email to verify your account.", tokenResponse));
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponse<object>("Validation failed", ModelState));
            }

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user", null));
            }

            var result = await _jwtService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
            if (!result)
            {
                return BadRequest(new ApiResponse<string>("Current password is incorrect or user not found", null));
            }

            return Ok(new ApiResponse<string>("Password changed successfully", null));
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var result = await _jwtService.ForgotPasswordAsync(request.Email);
            if (!result)
                return BadRequest(new ApiResponse<string>("Email not found", null));

            return Ok(new ApiResponse<string>("Password reset email sent", null));
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var result = await _jwtService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword);
            if (!result)
                return BadRequest(new ApiResponse<string>("Invalid token or token expired", null));

            return Ok(new ApiResponse<string>("Password reset successful", null));
        }

        [HttpPost("send-email-verification")]
        [Authorize]
        public async Task<IActionResult> SendEmailVerification()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized(new ApiResponse<string>("Invalid user", null));

            var result = await _jwtService.SendEmailVerificationAsync(userId);
            if (!result)
                return BadRequest(new ApiResponse<string>("Email already verified or user not found", null));

            return Ok(new ApiResponse<string>("Verification email sent", null));
        }

        [HttpPost("confirm-email")]
        // This endpoint is used by the frontend form to manually confirm email using email and token in the request body.
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequestDto request)
        {
            var result = await _jwtService.ConfirmEmailAsync(request.Email, request.Token);
            if (!result)
                return BadRequest(new ApiResponse<string>("Invalid token or email already confirmed", null));

            return Ok(new ApiResponse<string>("Email confirmed successfully", null));
        }

        [HttpGet("verify-email")]
        // This endpoint is triggered by clicking the verification link sent to the user's email.
        // It verifies the email directly via query string parameters (email & token).
        // After successful verification, it automatically logs in the user by returning JWT tokens.
        public async Task<IActionResult> VerifyEmail([FromQuery] string email, [FromQuery] string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                return BadRequest(new ApiResponse<string>("Invalid email or token", null));
            }

            var user = await _userService.GetUserByEmailAsync(email);
            if (user == null)
            {
                return NotFound(new ApiResponse<string>("User not found", null));
            }

            var isValid = await _jwtService.ConfirmEmailAsync(user.Email, token);
            if (!isValid)
            {
                return BadRequest(new ApiResponse<string>("Invalid or expired verification token", null));
            }

            // Refresh user object from database to get updated EmailConfirmed status
            user = await _userService.GetUserByEmailAsync(email);
            if (user == null)
            {
                return NotFound(new ApiResponse<string>("User not found after email verification", null));
            }

            // After successful email verification, automatically generate tokens for login
            var accessToken = _jwtService.GenerateToken(user);
            var refreshToken = _jwtService.GenerateRefreshToken();
            var refreshTokenExpiry = _jwtService.GetRefreshTokenExpiryTime();

            // Update user with new refresh token
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = refreshTokenExpiry;
            await _userService.UpdateAsync(user);

            var tokenResponse = new TokenResponseDto
            {
                Token = accessToken,
                RefreshToken = refreshToken,
                RefreshTokenExpiryTime = refreshTokenExpiry,
                Role = user.Role.ToString()
            };

            return Ok(new ApiResponse<TokenResponseDto>("Email verified successfully. You are now logged in.", tokenResponse));
        }
    }
}