using BusinessObject.DTOs.OrdersDto;
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
        public List<BusinessObject.DTOs.RentalViolationDto.RentalViolationDto> ExistingViolations { get; set; } = new List<BusinessObject.DTOs.RentalViolationDto.RentalViolationDto>();
        [BindProperty] public bool IsEditMode { get; set; } = false;
        public string ApiBaseUrl => _configuration.GetApiBaseUrl(_environment);

        public async Task<IActionResult> OnGetAsync(Guid orderId, string? edit)
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
                                    ExistingViolations = violationsApiResponse.Data.ToList();
                                    ReportedOrderItemIds = ExistingViolations.Select(v => v.OrderItemId).ToList();
                                }
                            }
                        }
                        catch
                        {
                            // Ignore errors when fetching violations
                        }

                        // Check for edit mode (following PostItem pattern)
                        if (!string.IsNullOrEmpty(edit) && edit.ToLower() == "true")
                        {
                            IsEditMode = true;
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
            if (IsEditMode)
            {
                return await HandleMixedModeAsync();
            }
            else
            {
                return await HandleCreateViolationsAsync();
            }
        }

        private async Task<IActionResult> HandleMixedModeAsync()
        {
            try
            {
                var form = await Request.ReadFormAsync();
                var orderId = Guid.Parse(form["orderId"]);

                // Reload existing violations để có data mới nhất
                var tempClient = await _clientHelper.GetAuthenticatedClientAsync();
                await ReloadOrderDataAsync(orderId, tempClient);

                // Get existing violation item IDs
                var existingViolationItemIds = ExistingViolations?.Select(v => v.OrderItemId).ToList() ?? new List<Guid>();
                
                // Get all selected items from form
                var selectedItemIds = new List<Guid>();
                foreach (var key in form.Keys)
                {
                    if (key.StartsWith("violations[") && key.EndsWith("].OrderItemId"))
                    {
                        if (Guid.TryParse(form[key], out var itemId))
                        {
                            selectedItemIds.Add(itemId);
                        }
                    }
                }

                // Phân loại items
                var editingExistingItems = selectedItemIds.Intersect(existingViolationItemIds).ToList();
                var newItemIds = selectedItemIds.Except(existingViolationItemIds).ToList();

                var apiClient = await _clientHelper.GetAuthenticatedClientAsync();
                bool updateSuccess = true;
                bool createSuccess = true;

                // TH1 + TH3: Update existing violations nếu có
                if (editingExistingItems.Any())
                {
                    updateSuccess = await UpdateExistingViolationsAsync(form, apiClient);
                    if (!updateSuccess)
                    {
                        TempData["ErrorMessage"] = "Failed to update existing violations";
                        await ReloadOrderDataAsync(orderId, apiClient);
                        return Page();
                    }
                }

                // TH2 + TH3: Create new violations nếu có
                if (newItemIds.Any())
                {
                    createSuccess = await CreateNewViolationsAsync(form, orderId, existingViolationItemIds, apiClient);
                    if (!createSuccess)
                    {
                        // Error message should already be set in CreateNewViolationsAsync
                        // If not set, use default message
                        if (TempData["ErrorMessage"] == null)
                        {
                            TempData["ErrorMessage"] = "Failed to create new violations. Please check your input and try again.";
                        }
                        await ReloadOrderDataAsync(orderId, apiClient);
                        return Page();
                    }
                }

                // Success messages based on what actually happened
                string successMessage = "";
                if (editingExistingItems.Any() && newItemIds.Any())
                {
                    successMessage = "Violation report updated and new violations added successfully"; // TH3
                }
                else if (editingExistingItems.Any())
                {
                    successMessage = "Violation report updated successfully"; // TH1  
                }
                else if (newItemIds.Any())
                {
                    successMessage = "New violation reports added successfully"; // TH2
                }
                else
                {
                    successMessage = "No changes detected";
                }

                TempData["SuccessMessage"] = successMessage;
                return RedirectToPage("/Provider/OrderDetail", new { id = orderId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                return Page();
            }
        }

        private async Task<bool> CreateNewViolationsAsync(IFormCollection form, Guid orderId, List<Guid> existingViolationItemIds, HttpClient apiClient)
        {
            try
            {
                // Build multipart form data cho new violations
                var formData = new MultipartFormDataContent();
                formData.Add(new StringContent(orderId.ToString()), "OrderId");

                int violationIndex = 0;
                var orderItemIds = form.Keys.Where(k => k.StartsWith("violations[") && k.EndsWith("].OrderItemId")).ToList();

                foreach (var key in orderItemIds)
                {
                    var itemId = form[key];
                    var prefix = key.Replace(".OrderItemId", "");

                    // Skip if this item already has existing violation (chỉ process new items)
                    if (Guid.TryParse(itemId, out var parsedItemId) && existingViolationItemIds.Contains(parsedItemId))
                    {
                        continue; // Skip existing violations
                    }

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

                // Check if we have any new violations to create
                if (violationIndex == 0)
                {
                    return true; // No new violations to create, but that's OK
                }

                var response = await apiClient.PostAsync("api/rental-violations", formData);
                
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    // Read error message from API response
                    var errorContent = await response.Content.ReadAsStringAsync();
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<BusinessObject.DTOs.ApiResponses.ApiResponse<string>>(
                            errorContent,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );
                        
                        // Store error message for display
                        if (!string.IsNullOrEmpty(errorResponse?.Message))
                        {
                            TempData["ErrorMessage"] = errorResponse.Message;
                        }
                        else
                        {
                            TempData["ErrorMessage"] = $"Failed to create violation report. Status: {response.StatusCode}";
                        }
                    }
                    catch
                    {
                        // If parsing fails, use raw content or default message
                        TempData["ErrorMessage"] = !string.IsNullOrEmpty(errorContent) 
                            ? $"Failed to create violation report: {errorContent}"
                            : $"Failed to create violation report. Status: {response.StatusCode}";
                    }
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Store exception message for debugging
                TempData["ErrorMessage"] = $"An error occurred while creating violation report: {ex.Message}";
                return false;
            }
        }

        private async Task<IActionResult> HandleCreateViolationsAsync()
        {
            try
            {
                var form = await Request.ReadFormAsync();
                var orderId = Guid.Parse(form["orderId"]);

                // Get existing violation item IDs to avoid duplicates
                var existingViolationItemIds = ExistingViolations?.Select(v => v.OrderItemId).ToList() ?? new List<Guid>();
                
                var apiClient = await _clientHelper.GetAuthenticatedClientAsync();
                var createSuccess = await CreateNewViolationsAsync(form, orderId, existingViolationItemIds, apiClient);

                if (createSuccess)
                {
                    TempData["SuccessMessage"] = "Violation report created successfully";
                    return RedirectToPage("/Provider/OrderDetail", new { id = orderId });
                }
                else
                {
                    // Error message should already be set in CreateNewViolationsAsync
                    // If not set, use default message
                    if (TempData["ErrorMessage"] == null)
                    {
                        TempData["ErrorMessage"] = "Failed to create violation report. Please check your input and try again.";
                    }
                    await ReloadOrderDataAsync(orderId, apiClient);
                    return Page();
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                
                // Reload order data if possible
                try
                {
                    var form = await Request.ReadFormAsync();
                    var orderId = Guid.Parse(form["orderId"]);
                    var client = await _clientHelper.GetAuthenticatedClientAsync();
                    await ReloadOrderDataAsync(orderId, client);
                }
                catch { }

                return Page();
            }
        }

        private async Task<IActionResult> HandleUpdateViolationsAsync()
        {
            try
            {
                var form = await Request.ReadFormAsync();
                var orderId = Guid.Parse(form["orderId"]);

                // Reload existing violations
                var tempClient = await _clientHelper.GetAuthenticatedClientAsync();
                await ReloadOrderDataAsync(orderId, tempClient);

                if (ExistingViolations == null || !ExistingViolations.Any())
                {
                    TempData["ErrorMessage"] = "No existing violations found to update";
                    return RedirectToPage("/Provider/OrderDetail", new { id = orderId });
                }

                var apiClient = await _clientHelper.GetAuthenticatedClientAsync();
                var updateSuccess = await UpdateExistingViolationsAsync(form, apiClient);

                if (updateSuccess)
                {
                    TempData["SuccessMessage"] = "Violation report updated successfully";
                    return RedirectToPage("/Provider/OrderDetail", new { id = orderId });
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update violation report";
                    await ReloadOrderDataAsync(orderId, apiClient);
                    return Page();
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                return Page();
            }
        }

        private async Task<bool> UpdateExistingViolationsAsync(IFormCollection form, HttpClient apiClient)
        {
            try
            {
                // Update each existing violation
                foreach (var existingViolation in ExistingViolations)
                {
                    var prefix = $"violations[{existingViolation.OrderItemId}]";

                    // Get updated values from form
                    var violationTypeStr = form[$"{prefix}.ViolationType"].ToString();
                    var description = form[$"{prefix}.Description"].ToString();
                    var damagePercentageStr = form[$"{prefix}.DamagePercentage"].ToString();
                    var penaltyPercentageStr = form[$"{prefix}.PenaltyPercentage"].ToString();
                    var penaltyAmountStr = form[$"{prefix}.PenaltyAmount"].ToString();

                    // Convert ViolationType string to enum number
                    int? violationTypeEnum = null;
                    if (!string.IsNullOrEmpty(violationTypeStr))
                    {
                        switch (violationTypeStr)
                        {
                            case "DAMAGED": violationTypeEnum = 0; break;
                            case "LATE_RETURN": violationTypeEnum = 1; break;
                            case "NOT_RETURNED": violationTypeEnum = 2; break;
                            default: 
                                if (int.TryParse(violationTypeStr, out var vType)) 
                                    violationTypeEnum = vType; 
                                break;
                        }
                    }

                    // Build update DTO with all fields
                    var updateDto = new
                    {
                        ViolationType = violationTypeEnum,
                        Description = !string.IsNullOrEmpty(description) ? description : null,
                        DamagePercentage = decimal.TryParse(damagePercentageStr, out var damagePct) ? damagePct : (decimal?)null,
                        PenaltyPercentage = decimal.TryParse(penaltyPercentageStr, out var penaltyPct) ? penaltyPct : (decimal?)null,
                        PenaltyAmount = decimal.TryParse(penaltyAmountStr, out var penaltyAmt) ? penaltyAmt : (decimal?)null
                    };

                    // Call EDIT API (new endpoint for edit mode)
                    var updateResponse = await apiClient.PutAsJsonAsync($"api/rental-violations/{existingViolation.ViolationId}/edit", updateDto);
                    
                    if (!updateResponse.IsSuccessStatusCode)
                    {
                        return false; // Fail if any update fails
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task ReloadOrderDataAsync(Guid orderId, HttpClient client)
        {
            try
            {
                // Reload order data
                var orderResponse = await client.GetAsync($"api/orders/{orderId}/provider-details");
                if (orderResponse.IsSuccessStatusCode)
                {
                    var apiResponse = await orderResponse.Content.ReadFromJsonAsync<BusinessObject.DTOs.ApiResponses.ApiResponse<OrderDetailsDto>>();
                    Order = apiResponse?.Data;
                }

                // Reload existing violations
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
                        ExistingViolations = violationsApiResponse.Data.ToList();
                        ReportedOrderItemIds = ExistingViolations.Select(v => v.OrderItemId).ToList();
                    }
                }
            }
            catch
            {
                // Ignore errors when reloading data
            }
        }
    }
}