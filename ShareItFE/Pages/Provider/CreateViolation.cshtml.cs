using BusinessObject.DTOs.OrdersDto;
using BusinessObject.DTOs.RentalViolationDto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using ShareItFE.Extensions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShareItFE.Pages.Provider
{
    public class CreateViolationModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public CreateViolationModel(
            AuthenticatedHttpClientHelper clientHelper,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _clientHelper = clientHelper;
            _configuration = configuration;
            _environment = environment;
        }

        public OrderDetailsDto Order { get; set; }
        public List<Guid> ReportedOrderItemIds { get; set; } = new List<Guid>();
        public string ApiBaseUrl => _configuration.GetApiBaseUrl(_environment);

        public async Task<IActionResult> OnGetAsync(Guid orderId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Auth");
            }

            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var apiUrl = $"api/orders/{orderId}/provider-details";
                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<BusinessObject.DTOs.ApiResponses.ApiResponse<OrderDetailsDto>>(
                        responseContent,
                        new JsonSerializerOptions 
                        { 
                            PropertyNameCaseInsensitive = true,
                            Converters = { new JsonStringEnumConverter() }
                        }
                    );
                    
                    if (apiResponse?.Data != null)
                    {
                        Order = apiResponse.Data;
                        
                        // Get existing violations for this order to filter out already reported items
                        try
                        {
                            var violationsResponse = await client.GetAsync($"api/rental-violations/order/{orderId}");
                            if (violationsResponse.IsSuccessStatusCode)
                            {
                                var violationsContent = await violationsResponse.Content.ReadAsStringAsync();
                                var violationsApiResponse = JsonSerializer.Deserialize<BusinessObject.DTOs.ApiResponses.ApiResponse<IEnumerable<BusinessObject.DTOs.RentalViolationDto.RentalViolationDto>>>(
                                    violationsContent,
                                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } }
                                );
                                
                                if (violationsApiResponse?.Data != null)
                                {
                                    ReportedOrderItemIds = violationsApiResponse.Data.Select(v => v.OrderItemId).ToList();
                                }
                            }
                        }
                        catch
                        {
                            // Ignore errors when fetching violations
                        }
                        
                        return Page();
                    }
                    else
                    {
                        TempData["ErrorMessage"] = $"API returned null data. Message: {apiResponse?.Message}";
                        return RedirectToPage("/Provider/OrderManagement");
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    TempData["ErrorMessage"] = $"Failed to load order. Status: {response.StatusCode}. Details: {errorContent}";
                    return RedirectToPage("/Provider/OrderManagement");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return RedirectToPage("/Provider/OrderManagement");
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                var form = await Request.ReadFormAsync();
                var orderId = Guid.Parse(form["orderId"]);

                // Build multipart form data
                var formData = new MultipartFormDataContent();
                formData.Add(new StringContent(orderId.ToString()), "OrderId");

                int violationIndex = 0;
                var orderItemIds = form.Keys.Where(k => k.StartsWith("violations[") && k.EndsWith("].OrderItemId")).ToList();

                foreach (var key in orderItemIds)
                {
                    var itemId = form[key];
                    var prefix = key.Replace(".OrderItemId", "");

                    // Kiểm tra xem có violation type không (chỉ xử lý item đã được chọn)
                    var violationType = form[$"{prefix}.ViolationType"].ToString();
                    if (string.IsNullOrEmpty(violationType))
                    {
                        continue; // Bỏ qua item không được chọn
                    }

                    // Add violation data
                    formData.Add(new StringContent(itemId), $"Violations[{violationIndex}].OrderItemId");
                    formData.Add(new StringContent(violationType), $"Violations[{violationIndex}].ViolationType");
                    
                    var description = form[$"{prefix}.Description"].ToString();
                    if (string.IsNullOrEmpty(description))
                    {
                        continue; // Bỏ qua nếu không có description
                    }
                    formData.Add(new StringContent(description), $"Violations[{violationIndex}].Description");

                    // Damage percentage (optional)
                    if (!string.IsNullOrEmpty(form[$"{prefix}.DamagePercentage"]))
                    {
                        formData.Add(new StringContent(form[$"{prefix}.DamagePercentage"]), $"Violations[{violationIndex}].DamagePercentage");
                    }

                    // Penalty percentage
                    var penaltyPct = form[$"{prefix}.PenaltyPercentage"].ToString();
                    if (!string.IsNullOrEmpty(penaltyPct))
                    {
                        formData.Add(new StringContent(penaltyPct), $"Violations[{violationIndex}].PenaltyPercentage");
                    }

                    // Penalty amount
                    var penaltyAmt = form[$"{prefix}.PenaltyAmount"].ToString();
                    if (!string.IsNullOrEmpty(penaltyAmt))
                    {
                        formData.Add(new StringContent(penaltyAmt), $"Violations[{violationIndex}].PenaltyAmount");
                    }

                    // Add files
                    var files = form.Files.Where(f => f.Name.StartsWith($"{prefix}.EvidenceFiles")).ToList();
                    
                    if (files.Count == 0)
                    {
                        continue; // Skip if no files
                    }

                    foreach (var file in files)
                    {
                        var fileContent = new StreamContent(file.OpenReadStream());
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                        formData.Add(fileContent, $"Violations[{violationIndex}].EvidenceFiles", file.FileName);
                    }

                    violationIndex++;
                }

                // Check if there are any violations
                if (violationIndex == 0)
                {
                    TempData["ErrorMessage"] = "Please select at least 1 item with violation";
                    
                    // Reload order data
                    var client = await _clientHelper.GetAuthenticatedClientAsync();
                    var orderResponse = await client.GetAsync($"api/orders/{orderId}/provider-details");
                    if (orderResponse.IsSuccessStatusCode)
                    {
                        var apiResponse = await orderResponse.Content.ReadFromJsonAsync<BusinessObject.DTOs.ApiResponses.ApiResponse<OrderDetailsDto>>();
                        Order = apiResponse?.Data;
                    }
                    
                    return Page();
                }

                var apiClient = await _clientHelper.GetAuthenticatedClientAsync();
                var response = await apiClient.PostAsync("api/rental-violations", formData);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Violation report created successfully";
                    return RedirectToPage("/Provider/OrderDetail", new { id = orderId });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    TempData["ErrorMessage"] = $"Failed to create violation report. Details: {errorContent}";
                    
                    // Reload order data
                    var orderResponse = await apiClient.GetAsync($"api/orders/{orderId}/provider-details");
                    if (orderResponse.IsSuccessStatusCode)
                    {
                        var apiResponse = await orderResponse.Content.ReadFromJsonAsync<BusinessObject.DTOs.ApiResponses.ApiResponse<OrderDetailsDto>>();
                        Order = apiResponse?.Data;
                    }
                    
                    return Page();
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                
                // Reload order data nếu có thể
                try
                {
                    var form = await Request.ReadFormAsync();
                    var orderId = Guid.Parse(form["orderId"]);
                    
                    var client = await _clientHelper.GetAuthenticatedClientAsync();
                    var orderResponse = await client.GetAsync($"api/orders/{orderId}/provider-details");
                    if (orderResponse.IsSuccessStatusCode)
                    {
                        var apiResponse = await orderResponse.Content.ReadFromJsonAsync<BusinessObject.DTOs.ApiResponses.ApiResponse<OrderDetailsDto>>();
                        Order = apiResponse?.Data;
                    }
                }
                catch { }
                
                return Page();
            }
        }
    }
}