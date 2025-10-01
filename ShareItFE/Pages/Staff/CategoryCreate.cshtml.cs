using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using ShareItFE.Common.Utilities;
using System.Linq;
using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.ProductDto;

namespace ShareItFE.Pages.Staff
{
    [Authorize(Roles = "admin,staff")]
    public class CategoryCreateModel : PageModel
    {
        private readonly ILogger<CategoryCreateModel> _logger;
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly JsonSerializerOptions _jsonOptions;

        public CategoryCreateModel(ILogger<CategoryCreateModel> logger, AuthenticatedHttpClientHelper clientHelper)
        {
            _logger = logger;
            _clientHelper = clientHelper;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        [BindProperty]
        public CategoryCreateDto CategoryInput { get; set; } = new();

        [BindProperty]
        public IFormFile? ImageFile { get; set; }

        // Image upload state
        public string? UploadedImageUrl { get; set; }
        public string? UploadedImagePublicId { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
            // Initialize form
        }

        public IActionResult OnGetTestSimple()
        {
            try
            {
                _logger.LogInformation("Testing simple endpoint...");
                
                // Check authentication first
                if (!User.Identity?.IsAuthenticated == true)
                {
                    _logger.LogWarning("User not authenticated");
                    return new JsonResult(new { success = false, error = "User not authenticated" });
                }
                
                var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                _logger.LogInformation("User role: {Role}", userRole);
                
                if (string.IsNullOrEmpty(userRole) || (!userRole.Contains("admin") && !userRole.Contains("staff")))
                {
                    _logger.LogWarning("User does not have required role. Role: {Role}", userRole);
                    return new JsonResult(new { success = false, error = "User does not have required role" });
                }
                
                return new JsonResult(new { 
                    success = true, 
                    message = "Simple test successful",
                    userRole = userRole,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in simple test");
                return new JsonResult(new { 
                    success = false, 
                    error = ex.Message, 
                    stackTrace = ex.StackTrace
                });
            }
        }

        public async Task<IActionResult> OnGetTestUploadApi()
        {
            try
            {
                _logger.LogInformation("Testing upload API...");
                
                // Check authentication first
                if (!User.Identity?.IsAuthenticated == true)
                {
                    _logger.LogWarning("User not authenticated");
                    return new JsonResult(new { success = false, error = "User not authenticated" });
                }
                
                var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                _logger.LogInformation("User role: {Role}", userRole);
                
                if (string.IsNullOrEmpty(userRole) || (!userRole.Contains("admin") && !userRole.Contains("staff")))
                {
                    _logger.LogWarning("User does not have required role. Role: {Role}", userRole);
                    return new JsonResult(new { success = false, error = "User does not have required role" });
                }
                
                _logger.LogInformation("Creating authenticated client...");
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                _logger.LogInformation("Got authenticated client");
                
                // Debug: Check if client has base URL and auth header
                _logger.LogInformation("Client base URL: {BaseUrl}", client.BaseAddress);
                _logger.LogInformation("Client auth header: {AuthHeader}", client.DefaultRequestHeaders.Authorization?.ToString());
                
                // Try different URL formats (base URL already includes /api)
                var testUrls = new[] { 
                    "CategoryUpload/test",
                    "/CategoryUpload/test"
                };
                
                HttpResponseMessage response = null;
                string usedUrl = "";
                
                foreach (var url in testUrls)
                {
                    _logger.LogInformation("Trying URL: {Url}", url);
                    response = await client.GetAsync(url);
                    _logger.LogInformation("Response status for {Url}: {Status}", url, response.StatusCode);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        usedUrl = url;
                        break;
                    }
                }
                
                if (response == null)
                {
                    return new JsonResult(new { success = false, error = "No response received" });
                }
                _logger.LogInformation("API call completed. Status: {Status}", response.StatusCode);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("API response: {Response}", content);
                    
                    // Return the API response directly
                    return new JsonResult(new { 
                        success = true, 
                        message = "API call successful",
                        data = content,
                        statusCode = response.StatusCode
                    });
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("API error: {Error}", error);
                    return new JsonResult(new { 
                        success = false, 
                        error = error,
                        statusCode = response.StatusCode
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing upload API");
                return new JsonResult(new { 
                    success = false, 
                    error = ex.Message, 
                    stackTrace = ex.StackTrace,
                    innerException = ex.InnerException?.Message
                });
            }
        }

        public async Task<IActionResult> OnPostUploadImage()
        {
            if (ImageFile == null || ImageFile.Length == 0)
            {
                return new JsonResult(new { success = false, message = "No file selected." });
            }

            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                
                using var content = new MultipartFormDataContent();
                var streamContent = new StreamContent(ImageFile.OpenReadStream());
                content.Add(streamContent, "image", ImageFile.FileName);

                var response = await client.PostAsync("api/categories/upload-image", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = false, message = $"Upload failed: {error}" });
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<ImageUploadResult>>(responseContent, _jsonOptions);
                
                if (apiResponse?.Data == null)
                {
                    _logger.LogError("API response data is null. Response: {Response}", responseContent);
                    return new JsonResult(new { success = false, message = "API did not return image data." });
                }

                // Store uploaded image info
                UploadedImageUrl = apiResponse.Data.ImageUrl;
                UploadedImagePublicId = apiResponse.Data.PublicId;
                CategoryInput.ImageUrl = UploadedImageUrl;

                return new JsonResult(new { 
                    success = true, 
                    imageUrl = UploadedImageUrl,
                    publicId = UploadedImagePublicId
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Upload error: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                // Check if user is authenticated
                if (!User.Identity?.IsAuthenticated == true)
                {
                    ErrorMessage = "You must be logged in to create categories.";
                    return Page();
                }

                // Check if user has required role
                var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                if (string.IsNullOrEmpty(userRole) || (!userRole.Contains("admin") && !userRole.Contains("staff")))
                {
                    ErrorMessage = "You don't have permission to create categories.";
                    return Page();
                }

                var client = await _clientHelper.GetAuthenticatedClientAsync();

                // Create multipart/form-data content
                using var formContent = new MultipartFormDataContent();
                
                // Add form fields
                formContent.Add(new StringContent(CategoryInput.Name), "name");
                
                if (!string.IsNullOrEmpty(CategoryInput.Description))
                {
                    formContent.Add(new StringContent(CategoryInput.Description), "description");
                }
                
                formContent.Add(new StringContent(CategoryInput.IsActive.ToString().ToLower()), "isActive");
                
                // Add image file if provided
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    var imageContent = new StreamContent(ImageFile.OpenReadStream());
                    imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(ImageFile.ContentType);
                    formContent.Add(imageContent, "ImageFile", ImageFile.FileName);
                }

                // Send request to backend
                var response = await client.PostAsync("api/categories", formContent);

                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "Category created successfully!";
                    
                    // Reset form
                    CategoryInput = new CategoryCreateDto();
                    ImageFile = null;
                    
                    return RedirectToPage();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        ErrorMessage = "Access denied. Please make sure you are logged in with admin or staff role.";
                    }
                    else
                    {
                        ErrorMessage = $"Failed to create category: {errorContent}";
                    }
                    return Page();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"An error occurred: {ex.Message}";
                return Page();
            }
        }
    }

    public class CategoryCreateDto
    {
        [Required(ErrorMessage = "Category name is required")]
        [StringLength(150, ErrorMessage = "Name cannot exceed 150 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(255, ErrorMessage = "Description cannot exceed 255 characters")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Image URL")]
        public string? ImageUrl { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;
    }
}
