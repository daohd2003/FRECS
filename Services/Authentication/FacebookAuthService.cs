using BusinessObject.DTOs.Login;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Services.Authentication
{
    public class FacebookAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public FacebookAuthService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<FacebookPayload?> VerifyFacebookTokenAsync(string accessToken)
        {
            var appId = _configuration["FacebookClientSettings:AppId"];
            var appSecret = _configuration["FacebookClientSettings:AppSecret"];
            
            // 1. Verify token with Facebook
            var appAccessToken = $"{appId}|{appSecret}";
            var debugUrl = $"https://graph.facebook.com/debug_token?input_token={accessToken}&access_token={appAccessToken}";
            
            var debugResponse = await _httpClient.GetAsync(debugUrl);
            if (!debugResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var debugJson = await debugResponse.Content.ReadAsStringAsync();
            using var debugDoc = JsonDocument.Parse(debugJson);
            var debugData = debugDoc.RootElement.GetProperty("data");
            
            // Check if token is valid
            if (!debugData.GetProperty("is_valid").GetBoolean())
            {
                return null;
            }

            // 2. Get user profile from Facebook Graph API
            var profileUrl = $"https://graph.facebook.com/v17.0/me?fields=id,name,email,picture.height(200)&access_token={accessToken}";
            var profileResponse = await _httpClient.GetAsync(profileUrl);
            
            if (!profileResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var profileJson = await profileResponse.Content.ReadAsStringAsync();
            using var profileDoc = JsonDocument.Parse(profileJson);
            var root = profileDoc.RootElement;

            var payload = new FacebookPayload
            {
                Id = root.GetProperty("id").GetString() ?? string.Empty,
                Name = root.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                Email = root.TryGetProperty("email", out var email) ? email.GetString() ?? string.Empty : string.Empty,
                PictureUrl = root.TryGetProperty("picture", out var pic) &&
                             pic.TryGetProperty("data", out var data) &&
                             data.TryGetProperty("url", out var urlProp)
                             ? urlProp.GetString() ?? string.Empty
                             : string.Empty
            };

            return payload;
        }
    }
}


