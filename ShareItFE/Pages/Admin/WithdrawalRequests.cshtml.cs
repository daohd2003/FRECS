using BusinessObject.DTOs.WithdrawalDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace ShareItFE.Pages.Admin
{
    [Authorize(Roles = "admin")]
    public class WithdrawalRequestsModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WithdrawalRequestsModel> _logger;

        public WithdrawalRequestsModel(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<WithdrawalRequestsModel> logger)
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

        /// <summary>
        /// Get all withdrawal requests (pending and processed)
        /// </summary>
        public async Task<IActionResult> OnGetWithdrawalsAsync()
        {
            try
            {
                var token = Request.Cookies["AccessToken"];
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Unauthorized access to load withdrawals - no access token");
                    return new JsonResult(new { success = false, message = "Unauthorized" });
                }

                var client = _httpClientFactory.CreateClient("BackendApi");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var apiBaseUrl = _configuration["ApiSettings:BaseUrl"];
                
                // Get all withdrawal requests (pending, completed, rejected)
                var response = await client.GetAsync($"{apiBaseUrl}/api/withdrawals/all");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = true, data = responseContent });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to load withdrawal requests. Status: {StatusCode}, Response: {Response}",
                        response.StatusCode, errorContent);
                    return new JsonResult(new
                    {
                        success = false,
                        message = $"API call failed with status {response.StatusCode}",
                        error = errorContent
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading withdrawal requests data");
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific withdrawal request by ID
        /// </summary>
        public async Task<IActionResult> OnGetWithdrawalByIdAsync(Guid id)
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
                var response = await client.GetAsync($"{apiBaseUrl}/api/withdrawals/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = true, data = responseContent });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to load withdrawal {Id}. Status: {StatusCode}, Response: {Response}",
                        id, response.StatusCode, errorContent);
                    return new JsonResult(new
                    {
                        success = false,
                        message = $"Failed to load withdrawal request",
                        error = errorContent
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading withdrawal {Id}", id);
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Process (approve or reject) a withdrawal request
        /// </summary>
        public async Task<IActionResult> OnPostProcessWithdrawalAsync([FromBody] ProcessWithdrawalRequest request)
        {
            try
            {
                var token = Request.Cookies["AccessToken"];
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Unauthorized access attempt to process withdrawal - no access token");
                    return new JsonResult(new { success = false, message = "Unauthorized" }) { StatusCode = 401 };
                }

                var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminIdClaim) || !Guid.TryParse(adminIdClaim, out var adminId))
                {
                    _logger.LogWarning("Invalid admin ID claim: {AdminIdClaim}", adminIdClaim);
                    return new JsonResult(new { success = false, message = "Invalid admin ID" }) { StatusCode = 400 };
                }

                // Validate input
                if (request.Status != "Completed" && request.Status != "Rejected")
                {
                    return new JsonResult(new { success = false, message = "Invalid status. Must be 'Completed' or 'Rejected'" }) { StatusCode = 400 };
                }

                if (request.Status == "Rejected" && string.IsNullOrWhiteSpace(request.RejectionReason))
                {
                    return new JsonResult(new { success = false, message = "Rejection reason is required when rejecting a request" }) { StatusCode = 400 };
                }

                var client = _httpClientFactory.CreateClient("BackendApi");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var apiBaseUrl = _configuration["ApiSettings:BaseUrl"];
                var response = await client.PostAsJsonAsync($"{apiBaseUrl}/api/withdrawals/process", new
                {
                    withdrawalRequestId = request.WithdrawalRequestId,
                    status = request.Status,
                    rejectionReason = request.RejectionReason,
                    externalTransactionId = request.ExternalTransactionId,
                    adminNotes = request.AdminNotes
                });

                if (response.IsSuccessStatusCode)
                {
                    var action = request.Status == "Completed" ? "approved" : "rejected";
                    _logger.LogInformation("Withdrawal {WithdrawalId} {Action} successfully by admin {AdminId}",
                        request.WithdrawalRequestId, action, adminId);
                    return new JsonResult(new { success = true, message = $"Withdrawal request {action} successfully" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to process withdrawal {WithdrawalId}. Status: {StatusCode}, Response: {Response}",
                        request.WithdrawalRequestId, response.StatusCode, errorContent);
                    return new JsonResult(new { success = false, message = "Failed to process withdrawal. Please try again." }) { StatusCode = (int)response.StatusCode };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing withdrawal {WithdrawalId}", request.WithdrawalRequestId);
                return new JsonResult(new { success = false, message = "An error occurred. Please contact support." }) { StatusCode = 500 };
            }
        }
    }

    public class ProcessWithdrawalRequest
    {
        public Guid WithdrawalRequestId { get; set; }
        public string Status { get; set; } = string.Empty; // "Completed" or "Rejected"
        public string? RejectionReason { get; set; }
        public string? ExternalTransactionId { get; set; }
        public string? AdminNotes { get; set; }
    }
}

