using BusinessObject.DTOs.ProfileDtos;
using BusinessObject.Mappings;
using BusinessObject.Models;
using Microsoft.AspNetCore.Mvc;
using Services.CartServices;
using Services.ProfileServices;
using Services.UserServices;
using System.Security.Claims;
using System.Text.Json;

namespace ShareItFE.ViewComponents.Header
{
    public class HeaderViewComponent : ViewComponent
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HeaderViewComponent(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
        }


        public async Task<IViewComponentResult> InvokeAsync()
        {
            var model = new HeaderViewModel { IsUserLoggedIn = false };
            var currentUser = _httpContextAccessor.HttpContext?.User;

            if (currentUser != null && currentUser.Identity.IsAuthenticated)
            {
                model.IsUserLoggedIn = true;
                model.UserRole = currentUser.FindFirst(ClaimTypes.Role)?.Value;

                // Tạo HttpClient để gọi Backend API
                var client = _httpClientFactory.CreateClient("BackendApi");

                // Gửi kèm cookie xác thực trong mỗi request
                var authToken = _httpContextAccessor.HttpContext.Request.Cookies["AccessToken"];
                if (authToken != null)
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
                }

                try
                {
                    var profileResponse = await client.GetAsync("api/profile/header-info");
                    if (profileResponse.IsSuccessStatusCode)
                    {
                        var profileContent = await profileResponse.Content.ReadFromJsonAsync<UserHeaderInfoDto>();
                        model.UserName = profileContent?.FullName;
                        model.UserAvatarUrl = profileContent?.ProfilePictureUrl;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching profile info: {ex.Message}");
                }


                if (string.Equals(model.UserRole, "customer", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var cartResponse = await client.GetAsync("api/carts/count");
                        if (cartResponse.IsSuccessStatusCode)
                        {
                            var cartContent = await cartResponse.Content.ReadFromJsonAsync<JsonElement>();
                            if (cartContent.TryGetProperty("count", out var countElement))
                            {
                                model.CartItemCount = countElement.GetInt32();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error fetching cart count: {ex.Message}");
                    }
                }
            }

            return View(model);
        }
    }
}
