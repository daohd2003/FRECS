using BusinessObject.DTOs.FeedbackDto;
using BusinessObject.DTOs.PagingDto;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ShareItFE.Pages
{
    public class ProductVerificationModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;

        public List<ProductDTO> ProductItems { get; set; } = new();

        // New properties for pagination and filtering
        public string SearchTerm { get; set; }
        public string StatusFilter { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 5; // Default page size

        public int TotalCount { get; set; }

        [TempData]
        public string messageResponse { get; set; }

        [TempData]
        public bool isError { get; set; }


        public ProductVerificationModel(
            HttpClient httpClient, // Can be removed if not directly used by the model
            IConfiguration configuration, // Can be removed if not directly used by the model
            AuthenticatedHttpClientHelper clientHelper)
        {
            _clientHelper = clientHelper;
        }

        public async Task<IActionResult> OnGetAsync(string searchTerm = "", string status = "all", int page = 1, int pageSize = 5)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToPage("/Auth");

            // Assign received parameters to model properties
            SearchTerm = searchTerm;
            StatusFilter = status;
            CurrentPage = page < 1 ? 1 : page; // Ensure page is at least 1
            PageSize = pageSize < 1 ? 5 : pageSize; // Ensure pageSize is at least 5

            var client = await _clientHelper.GetAuthenticatedClientAsync();

            try
            {
                // Construct the URL with query parameters for filtering and pagination
                var baseUrl = "https://localhost:7256/api/products/filter"; // Use your filter endpoint
                var url = $"{baseUrl}?searchTerm={Uri.EscapeDataString(SearchTerm ?? "")}&status={Uri.EscapeDataString(StatusFilter ?? "all")}&page={CurrentPage}&pageSize={PageSize}";

                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    // Assuming your API returns a PagedResult<ProductDTO>
                    var pagedResult = JsonSerializer.Deserialize<PagedResult<ProductDTO>>(jsonString, options);

                    if (pagedResult != null)
                    {
                        ProductItems = pagedResult.Items ?? new List<ProductDTO>();
                        TotalCount = pagedResult.TotalCount;
                        TotalPages = (int)Math.Ceiling(pagedResult.TotalCount / (double)PageSize);
                        CurrentPage = pagedResult.CurrentPage; // Update with actual current page from API if different
                    }
                    else
                    {
                        ProductItems = new List<ProductDTO>();
                        TotalPages = 0;
                    }
                }
                else
                {
                    Console.WriteLine($"Error fetching data: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    ProductItems = new List<ProductDTO>();
                    TotalPages = 0;
                    // Optionally set an error message in TempData
                    messageResponse = $"Failed to load products: {response.ReasonPhrase}";
                    isError = true;
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Network error: {ex.Message}");
                ProductItems = new List<ProductDTO>();
                TotalPages = 0;
                messageResponse = "Network error: Could not connect to the product service.";
                isError = true;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON deserialization error: {ex.Message}");
                ProductItems = new List<ProductDTO>();
                TotalPages = 0;
                messageResponse = "Data format error: Could not process product list.";
                isError = true;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostUpdateItemStatus(string id, string status)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToPage("/Auth");

            if (!Guid.TryParse(id, out Guid productId))
            {
                messageResponse = "Invalid Product ID.";
                isError = true;
                
                return RedirectToPage("/ProductVerification", new { searchTerm = SearchTerm, status = StatusFilter, page = CurrentPage, pageSize = PageSize });
            }

            var productStatusDto = new ProductStatusUpdateDto
            {
                ProductId = productId,
                NewAvailabilityStatus = status
            };

            var jsonContent = JsonSerializer.Serialize(
                productStatusDto,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            );

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                Console.WriteLine("Authorization: " + client.DefaultRequestHeaders.Authorization?.ToString());

                var response = await client.PutAsync($"https://localhost:7256/api/products/update-status/{id}", content);

                if (response.IsSuccessStatusCode)
                {
                    messageResponse = "Action has been submitted successfully!";
                    isError = false;
                    ModelState.Clear();
                    Console.WriteLine($"Updated Product {id} to Status: {status}");
                    
                    return RedirectToPage("/ProductVerification", new { searchTerm = SearchTerm, status = StatusFilter, page = CurrentPage, pageSize = PageSize });
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Update failed: {response.StatusCode} - {errorContent}");

                messageResponse = $"Update failed: {response.StatusCode}";
                isError = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                messageResponse = "An unexpected error occurred.";
                isError = true;
            }

            
            return RedirectToPage("/ProductVerification", new { searchTerm = SearchTerm, status = StatusFilter, page = CurrentPage, pageSize = PageSize });
        }
    }
}