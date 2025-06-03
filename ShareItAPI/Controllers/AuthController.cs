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

        public AuthController(IJwtService jwtService, IUserService userService, GoogleAuthService googleAuthService)
        {
            _jwtService = jwtService;
            _userService = userService;
            _googleAuthService = googleAuthService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                var tokenResponse = await _jwtService.Authenticate(request.Email, request.Password);
                return Ok(new ApiResponse<TokenResponseDto>("Login successful", tokenResponse));
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new ApiResponse<string>("Invalid email or password", null));
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
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
        {
            var token = TokenHelper.ExtractAccessToken(HttpContext);

            if (string.IsNullOrEmpty(token))
                return Unauthorized(new ApiResponse<string>("Access token is missing or invalid", null));

            var result = await _jwtService.RefreshTokenAsync(token, request.RefreshToken);

            if (result == null)
                return Unauthorized(new ApiResponse<string>("Refresh token is invalid or has expired", null));

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

            return Ok(new ApiResponse<TokenResponseDto>("Registration successful", tokenResponse));
        }
    }
}