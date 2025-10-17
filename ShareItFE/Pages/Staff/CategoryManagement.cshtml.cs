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
    public class CategoryManagementModel : PageModel
    {
        private readonly ILogger<CategoryManagementModel> _logger;
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly JsonSerializerOptions _jsonOptions;

        public CategoryManagementModel(
            ILogger<CategoryManagementModel> logger, 
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

        public List<CategoryDto> Categories { get; set; } = new();
        public int TotalCategories { get; set; }
        public int ActiveCategories { get; set; }
        public int InactiveCategories { get; set; }
        public int TotalProducts { get; set; }
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
                
                // Get API URL dynamically based on environment (Development/Production)
                ApiBaseUrl = _configuration.GetApiBaseUrl(_environment);

                // Load categories from API
                await LoadCategoriesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading category management page");
                Categories = new List<CategoryDto>();
            }
        }

        private async Task LoadCategoriesAsync()
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var response = await client.GetAsync("api/categories");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    
                    // The API returns IEnumerable<CategoryDto> directly, not wrapped in ApiResponse
                    var categories = JsonSerializer.Deserialize<List<CategoryDto>>(content, _jsonOptions);
                    
                    if (categories != null)
                    {
                        Categories = categories;
                        CalculateStatistics();
                    }
                    else
                    {
                        Categories = new List<CategoryDto>();
                    }
                }
                else
                {
                    _logger.LogError("Failed to load categories. Status: {Status}", response.StatusCode);
                    Categories = new List<CategoryDto>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading categories from API");
                Categories = new List<CategoryDto>();
            }
        }

        private void CalculateStatistics()
        {
            TotalCategories = Categories.Count;
            ActiveCategories = Categories.Count(c => c.IsActive);
            InactiveCategories = Categories.Count(c => !c.IsActive);
            TotalProducts = Categories.Sum(c => c.Products?.Count ?? 0);
        }

        public async Task<IActionResult> OnPostToggleStatusAsync()
        {
            try
            {
                var form = await Request.ReadFormAsync();
                var categoryId = form["categoryId"].ToString();
                var isActiveStr = form["isActive"].ToString();

                if (string.IsNullOrEmpty(categoryId) || !Guid.TryParse(categoryId, out var categoryGuid))
                {
                    return new JsonResult(new { success = false, message = "Invalid category ID" });
                }

                if (!bool.TryParse(isActiveStr, out var isActive))
                {
                    return new JsonResult(new { success = false, message = "Invalid status value" });
                }

                var client = await _clientHelper.GetAuthenticatedClientAsync();

                // Create DTO for update
                var updateDto = new
                {
                    isActive = isActive
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(updateDto),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PatchAsync($"api/categories/{categoryGuid}/status?isActive={isActive}", null);

                if (response.IsSuccessStatusCode)
                {
                    return new JsonResult(new { success = true, message = "Category status updated successfully" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = false, message = $"Failed to update status: {errorContent}" });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnPostCreateCategoryAsync()
        {
            try
            {
                var form = await Request.ReadFormAsync();
                var categoryName = form["categoryName"].ToString();
                var categoryDescription = form["categoryDescription"].ToString();
                var isActive = form["isActive"].ToString() == "true";
                var imageFile = form.Files["imageFile"]; // Changed from "categoryImage" to "imageFile"

                if (string.IsNullOrEmpty(categoryName))
                {
                    return new JsonResult(new { success = false, message = "Category name is required" });
                }

                var client = await _clientHelper.GetAuthenticatedClientAsync();

                // Create multipart/form-data content
                using var formContent = new MultipartFormDataContent();
                
                // Add form fields
                formContent.Add(new StringContent(categoryName), "name");
                
                if (!string.IsNullOrEmpty(categoryDescription))
                {
                    formContent.Add(new StringContent(categoryDescription), "description");
                }
                
                formContent.Add(new StringContent(isActive.ToString().ToLower()), "isActive");
                
                // Add image file if provided
                if (imageFile != null && imageFile.Length > 0)
                {
                    var imageContent = new StreamContent(imageFile.OpenReadStream());
                    imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(imageFile.ContentType);
                    formContent.Add(imageContent, "ImageFile", imageFile.FileName);
                }

                // Send request to backend API
                var response = await client.PostAsync("api/categories", formContent);

                if (response.IsSuccessStatusCode)
                {
                    return new JsonResult(new { success = true, message = "Category created successfully" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = false, message = $"Failed to create category: {errorContent}" });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnPostUpdateCategoryAsync()
        {
            try
            {
                var form = await Request.ReadFormAsync();
                var categoryId = form["categoryId"].ToString();
                var categoryName = form["categoryName"].ToString();
                var categoryDescription = form["categoryDescription"].ToString();
                var isActive = form["isActive"].ToString() == "true";
                var imageFile = form.Files["imageFile"]; // Changed to imageFile

                if (string.IsNullOrEmpty(categoryId) || string.IsNullOrEmpty(categoryName))
                {
                    return new JsonResult(new { success = false, message = "Category ID and name are required" });
                }

                var client = await _clientHelper.GetAuthenticatedClientAsync();

                // Create multipart/form-data for update (with image if provided)
                using var formContent = new MultipartFormDataContent();
                
                formContent.Add(new StringContent(categoryName), "name");
                
                if (!string.IsNullOrEmpty(categoryDescription))
                {
                    formContent.Add(new StringContent(categoryDescription), "description");
                }
                
                formContent.Add(new StringContent(isActive.ToString().ToLower()), "isActive");
                
                // Add image file if provided
                if (imageFile != null && imageFile.Length > 0)
                {
                    var imageContent = new StreamContent(imageFile.OpenReadStream());
                    imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(imageFile.ContentType);
                    formContent.Add(imageContent, "ImageFile", imageFile.FileName);
                }

                // Use PUT with multipart/form-data
                var response = await client.PutAsync($"api/categories/{categoryId}", formContent);

                if (response.IsSuccessStatusCode)
                {
                    return new JsonResult(new { success = true, message = "Category updated successfully" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = false, message = $"Failed to update category: {errorContent}" });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnPostDeleteCategoryAsync(string categoryId)
        {
            try
            {
                if (string.IsNullOrEmpty(categoryId))
                {
                    return new JsonResult(new { success = false, message = "Category ID is required" });
                }

                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var response = await client.DeleteAsync($"api/categories/{categoryId}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(content, _jsonOptions);
                    
                    return new JsonResult(new { 
                        success = true, 
                        message = apiResponse?.Message ?? "Category deleted successfully" 
                    });
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    // Category has products - cannot delete
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(content, _jsonOptions);
                    
                    return new JsonResult(new { 
                        success = false, 
                        message = apiResponse?.Message ?? "Cannot delete category with products" 
                    });
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new JsonResult(new { success = false, message = "Category not found" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = false, message = $"Failed to delete category: {response.StatusCode}" });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnGetCategoryAsync(string categoryId)
        {
            try
            {
                if (string.IsNullOrEmpty(categoryId))
                {
                    return new JsonResult(new { success = false, message = "Category ID is required" });
                }

                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var response = await client.GetAsync($"api/categories/{categoryId}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    
                    // API GetById returns CategoryDto directly (not wrapped)
                    var category = JsonSerializer.Deserialize<CategoryDto>(content, _jsonOptions);
                    
                    if (category != null)
                    {
                        return new JsonResult(new { success = true, data = category });
                    }
                    else
                    {
                        return new JsonResult(new { success = false, message = "Failed to parse category data" });
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new JsonResult(new { success = false, message = "Category not found" });
                }

                return new JsonResult(new { success = false, message = $"API error: {response.StatusCode}" });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        private async Task<(bool Success, string ImageUrl, string Error)> UploadImageAsync(HttpClient client, IFormFile imageFile)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                var streamContent = new StreamContent(imageFile.OpenReadStream());
                content.Add(streamContent, "image", imageFile.FileName);

                var response = await client.PostAsync("categories/upload-image", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<ImageUploadResult>>(responseContent, _jsonOptions);
                    
                    if (apiResponse?.Data != null)
                    {
                        return (true, apiResponse.Data.ImageUrl, string.Empty);
                    }
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                return (false, string.Empty, $"Upload failed: {errorContent}");
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"Upload error: {ex.Message}");
            }
        }
    }
}
