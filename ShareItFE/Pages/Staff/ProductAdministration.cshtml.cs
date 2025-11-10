using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.DTOs.ApiResponses;
using System.Text.Json;
using System.Security.Claims;
using ShareItFE.Extensions;

namespace ShareItFE.Pages.Staff
{
    [Authorize(Roles = "admin,staff")]
    public class ProductAdministrationModel : PageModel
    {
        private readonly ILogger<ProductAdministrationModel> _logger;
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly JsonSerializerOptions _jsonOptions;

        public ProductAdministrationModel(
            ILogger<ProductAdministrationModel> logger,
            AuthenticatedHttpClientHelper clientHelper,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _logger = logger;
            _clientHelper = clientHelper;
            _configuration = configuration;
            _environment = environment;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public int TotalProducts { get; set; }
        public int Available { get; set; }
        public int Pending { get; set; }
        public int Rejected { get; set; }
        public string CurrentUserRole { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string ApiBaseUrl { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            try
            {
                // Get current user info
                CurrentUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
                AccessToken = HttpContext.Request.Cookies["AccessToken"] ?? string.Empty;

                // Get API URL dynamically based on environment
                ApiBaseUrl = _configuration.GetApiBaseUrl(_environment);

                // Load statistics from API
                await LoadStatisticsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading product administration page");
            }
        }

        private async Task LoadStatisticsAsync()
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var response = await client.GetAsync("api/products/admin/statistics");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<ProductStatisticsDto>>(content, _jsonOptions);

                    if (apiResponse?.Data != null)
                    {
                        TotalProducts = apiResponse.Data.TotalProducts;
                        Available = apiResponse.Data.Available;
                        Pending = apiResponse.Data.Pending;
                        Rejected = apiResponse.Data.Rejected;
                    }
                }
                else
                {
                    _logger.LogError("Failed to load statistics. Status: {Status}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading statistics from API");
            }
        }

        public async Task<IActionResult> OnPostApproveProductAsync()
        {
            try
            {
                var form = await Request.ReadFormAsync();
                var productId = form["productId"].ToString();

                if (string.IsNullOrEmpty(productId) || !Guid.TryParse(productId, out var productGuid))
                {
                    return new JsonResult(new { success = false, message = "Invalid product ID" });
                }

                var client = await _clientHelper.GetAuthenticatedClientAsync();

                var updateDto = new
                {
                    productId = productGuid,
                    newAvailabilityStatus = "Approved"
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(updateDto),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PutAsync($"api/products/admin/approve-reject/{productGuid}", jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    return new JsonResult(new { success = true, message = "Product approved successfully" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = false, message = $"Failed to approve product: {errorContent}" });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnPostRejectProductAsync()
        {
            try
            {
                var form = await Request.ReadFormAsync();
                var productId = form["productId"].ToString();

                if (string.IsNullOrEmpty(productId) || !Guid.TryParse(productId, out var productGuid))
                {
                    return new JsonResult(new { success = false, message = "Invalid product ID" });
                }

                var client = await _clientHelper.GetAuthenticatedClientAsync();

                var updateDto = new
                {
                    productId = productGuid,
                    newAvailabilityStatus = "Rejected"
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(updateDto),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PutAsync($"api/products/admin/approve-reject/{productGuid}", jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    return new JsonResult(new { success = true, message = "Product rejected successfully" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = false, message = $"Failed to reject product: {errorContent}" });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnPostDeleteProductAsync()
        {
            try
            {
                var form = await Request.ReadFormAsync();
                var productId = form["productId"].ToString();
                var hardDelete = form["hardDelete"].ToString() == "true";

                if (string.IsNullOrEmpty(productId) || !Guid.TryParse(productId, out var productGuid))
                {
                    return new JsonResult(new { success = false, message = "Invalid product ID" });
                }

                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var response = await client.DeleteAsync($"api/products/admin/delete/{productGuid}?hardDelete={hardDelete}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(content, _jsonOptions);

                    return new JsonResult(new
                    {
                        success = true,
                        message = apiResponse?.Message ?? "Product deleted successfully"
                    });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = false, message = $"Failed to delete product: {errorContent}" });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Error: {ex.Message}" });
            }
        }
    }

    // DTO for statistics
    public class ProductStatisticsDto
    {
        public int TotalProducts { get; set; }
        public int Available { get; set; }
        public int Pending { get; set; }
        public int Rejected { get; set; }
        public int Archived { get; set; }
        public int Deleted { get; set; }
        public int TotalRented { get; set; }
        public int TotalSold { get; set; }
    }
}

