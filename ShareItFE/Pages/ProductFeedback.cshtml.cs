using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.DTOs.FeedbackDto;
using BusinessObject.Enums;
using BusinessObject.DTOs.ApiResponses;
using System.Net.Http.Headers;
using System.Security.Claims;
using ShareItFE.Common.Utilities;
using Microsoft.Extensions.Options;
using BusinessObject.Models;
using System.Text.Json.Serialization;
using BusinessObject.DTOs.OrdersDto;

namespace ShareItFE.Pages // Replace with your actual project namespace
{
    public class ProductFeedbackModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly AuthenticatedHttpClientHelper _clientHelper;

        // Flag to control mock data usage
        private bool _useMockData = false; // Set to true for development/testing without API

        public ProductFeedbackModel(HttpClient httpClient, IConfiguration configuration, AuthenticatedHttpClientHelper clientHelper)
        {
           // _httpClient = httpClient;
           // _configuration = configuration;
            _clientHelper = clientHelper;
         //   _httpClient.BaseAddress = new Uri(_configuration["ApiSettings:BaseUrl"]);
        }

        [BindProperty]
        public OrderWithDetailsDto Order { get; set; }

        [BindProperty]
        public ProductDTO Product { get; set; }

        [BindProperty]
        public FeedbackRequestDto FeedbackInput { get; set; } = new FeedbackRequestDto();

        [BindProperty]
        public string RentalPeriodPlaceholder { get; set; } = "Not Available";

        public string ApiErrorMessage { get; set; }
        public string ApiSuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
             /* localhost:7256/api/orders/c0e4a7b0-8c2d-4e1f-8b7a-0a1b2c3d4e5f */

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToPage("/Auth");

            var client = await _clientHelper.GetAuthenticatedClientAsync();


            //----------- Get Order -----------------

            var orderResponse = await client.GetAsync($"api/orders/{id.Value}");
            
            if (!orderResponse.IsSuccessStatusCode) return RedirectToPage("/Auth");

            var orderApiResponse = JsonSerializer.Deserialize<ApiResponse<OrderWithDetailsDto>>(
               await orderResponse.Content.ReadAsStringAsync(), options);

            Order = orderApiResponse?.Data;
            //----------- End  Get Order -----------------


            //----------- Get Product -----------------

            var productResponse = await client.GetAsync($"api/products/{Order.Items.First().ProductId}");

            if (!productResponse.IsSuccessStatusCode) return RedirectToPage("/Auth");

            var productApiResponse = JsonSerializer.Deserialize<ProductDTO>(
               await productResponse.Content.ReadAsStringAsync(), options);

            Product = productApiResponse;
            //----------- End  Get Product -----------------

            FeedbackInput.TargetId = id.Value;
            FeedbackInput.OrderItemId = id.Value;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToPage("/Auth");

            var client = await _clientHelper.GetAuthenticatedClientAsync();


            if (FeedbackInput.Rating == 0)
            {
                ModelState.AddModelError("FeedbackInput.Rating", "Please provide an overall rating.");
                return Page();
            }

            if (FeedbackInput.Comment == null )
            {
                ModelState.AddModelError("FeedbackInput.Comment", "Please comment before submiting.");
                return Page();

            }

            var test = FeedbackInput;
            // Real API submission logic
            try
            {
                FeedbackInput.TargetType = FeedbackTargetType.Product;
                var jsonContent = JsonSerializer.Serialize(FeedbackInput, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                var response = await client.PostAsync("/api/feedbacks", content);

                if (response.IsSuccessStatusCode)
                {
                    ApiSuccessMessage = "Your feedback has been submitted successfully!";
                    ModelState.Clear();
                    FeedbackInput = new FeedbackRequestDto { TargetId = Product.Id, TargetType = FeedbackTargetType.Product };
                    // Re-fetch product data for a clean state
                    await OnGetAsync(Order.Id);
                    return Page();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    try
                    {
                        var apiErrorResponse = JsonSerializer.Deserialize<ApiResponse<object>>(errorContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        ApiErrorMessage = apiErrorResponse?.Message ?? $"Error: {response.StatusCode} - {response.ReasonPhrase}";
                    }
                    catch (JsonException)
                    {
                        ApiErrorMessage = $"Error: {response.StatusCode} - {response.ReasonPhrase}. Details: {errorContent}";
                    }
                    // Re-fetch product data for proper display after error
                    if (FeedbackInput.TargetId != Guid.Empty)
                    {
                        await OnGetAsync(FeedbackInput.TargetId);
                    }
                    return Page();
                }
            }
            catch (HttpRequestException ex)
            {
                ApiErrorMessage = $"Network error submitting feedback: {ex.Message}";
                // Re-fetch product data
                if (FeedbackInput.TargetId != Guid.Empty)
                {
                    await OnGetAsync(FeedbackInput.TargetId);
                }
                return Page();
            }
            catch (Exception ex)
            {
                ApiErrorMessage = $"An unexpected error occurred: {ex.Message}";
                // Re-fetch product data
                if (FeedbackInput.TargetId != Guid.Empty)
                {
                    await OnGetAsync(FeedbackInput.TargetId);
                }
                return Page();
            }

            return Page();
        }

       
    }
}