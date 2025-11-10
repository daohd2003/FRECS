using BusinessObject.DTOs.DepositRefundDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace ShareItFE.Pages.Admin
{
    [Authorize(Roles = "admin")]
    public class CustomerRefundsModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CustomerRefundsModel> _logger;

        public CustomerRefundsModel(
            IHttpClientFactory httpClientFactory, 
            IConfiguration configuration,
            ILogger<CustomerRefundsModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public string ApiBaseUrl { get; set; } = string.Empty;

        public void OnGet()
        {
            ApiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7256";
            // Data will be loaded via JavaScript API calls
        }

        public async Task<IActionResult> OnGetRefundsAsync()
        {
            try
            {
                var token = Request.Cookies["AccessToken"];
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Unauthorized access to load refunds - no access token");
                    return new JsonResult(new { success = false, message = "Unauthorized" });
                }

                var client = _httpClientFactory.CreateClient("BackendApi");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var apiBaseUrl = _configuration["ApiSettings:BaseUrl"];
                var response = await client.GetAsync($"{apiBaseUrl}/api/depositrefunds/all");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = true, data = responseContent });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to load refund requests. Status: {StatusCode}, Response: {Response}", 
                        response.StatusCode, errorContent);
                    return new JsonResult(new { 
                        success = false, 
                        message = $"API call failed with status {response.StatusCode}",
                        error = errorContent 
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading deposit refunds data");
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnGetRefundByIdAsync(Guid id)
        {
            try
            {
                var token = Request.Cookies["AccessToken"];
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Unauthorized access - no access token");
                    return new JsonResult(new { success = false, message = "Unauthorized" });
                }

                var client = _httpClientFactory.CreateClient("BackendApi");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var apiBaseUrl = _configuration["ApiSettings:BaseUrl"];
                var response = await client.GetAsync($"{apiBaseUrl}/api/depositrefunds/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = true, data = responseContent });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to load refund {Id}. Status: {StatusCode}, Response: {Response}",
                        id, response.StatusCode, errorContent);
                    return new JsonResult(new
                    {
                        success = false,
                        message = $"Failed to load refund details",
                        error = errorContent
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading refund {Id}", id);
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostProcessRefundAsync([FromBody] ProcessRefundRequest request)
        {
            try
            {
                var token = Request.Cookies["AccessToken"];
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Unauthorized access attempt to process refund - no access token");
                    return new JsonResult(new { success = false, message = "Unauthorized" }) { StatusCode = 401 };
                }

                var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminIdClaim) || !Guid.TryParse(adminIdClaim, out var adminId))
                {
                    _logger.LogWarning("Invalid admin ID claim: {AdminIdClaim}", adminIdClaim);
                    return new JsonResult(new { success = false, message = "Invalid admin ID" }) { StatusCode = 400 };
                }

                var client = _httpClientFactory.CreateClient("BackendApi");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var apiBaseUrl = _configuration["ApiSettings:BaseUrl"];
                var response = await client.PostAsJsonAsync($"{apiBaseUrl}/api/depositrefunds/process", new
                {
                    refundId = request.RefundId,
                    isApproved = request.IsApproved,
                    bankAccountId = request.BankAccountId,
                    notes = request.Notes,
                    externalTransactionId = request.ExternalTransactionId
                });

                if (response.IsSuccessStatusCode)
                {
                    var action = request.IsApproved ? "approved" : "rejected";
                    _logger.LogInformation("Refund {RefundId} {Action} successfully by admin {AdminId}", 
                        request.RefundId, action, adminId);
                    return new JsonResult(new { success = true, message = $"Refund request {action} successfully" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to process refund {RefundId}. Status: {StatusCode}, Response: {Response}", 
                        request.RefundId, response.StatusCode, errorContent);
                    return new JsonResult(new { success = false, message = "Failed to process refund. Please try again." }) { StatusCode = (int)response.StatusCode };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing refund {RefundId}", request.RefundId);
                return new JsonResult(new { success = false, message = "An error occurred. Please contact support." }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnPostReopenRefundAsync(Guid refundId)
        {
            try
            {
                var token = Request.Cookies["AccessToken"];
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Unauthorized access attempt to reopen refund - no access token");
                    return new JsonResult(new { success = false, message = "Unauthorized" }) { StatusCode = 401 };
                }

                var client = _httpClientFactory.CreateClient("BackendApi");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var apiBaseUrl = _configuration["ApiSettings:BaseUrl"];
                var response = await client.PostAsync($"{apiBaseUrl}/api/depositrefunds/reopen/{refundId}", null);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Refund {RefundId} reopened successfully", refundId);
                    return new JsonResult(new { success = true, message = "Refund request reopened successfully" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to reopen refund {RefundId}. Status: {StatusCode}, Response: {Response}", 
                        refundId, response.StatusCode, errorContent);
                    return new JsonResult(new { success = false, message = "Failed to reopen refund. Please try again." }) { StatusCode = (int)response.StatusCode };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reopening refund {RefundId}", refundId);
                return new JsonResult(new { success = false, message = "An error occurred. Please contact support." }) { StatusCode = 500 };
            }
        }
    }

    public class ApiResponse<T>
    {
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
    }

    public class ProcessRefundRequest
    {
        public Guid RefundId { get; set; }
        public bool IsApproved { get; set; }
        public Guid? BankAccountId { get; set; }
        public string? Notes { get; set; }
        public string? ExternalTransactionId { get; set; }
    }
}

