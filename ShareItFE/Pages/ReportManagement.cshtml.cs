using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization; // Thêm using này
using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.Contact;
using BusinessObject.DTOs.ReportDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using ShareItFE.Extensions;

namespace ShareItFE.Pages
{
    [Authorize(Roles = "admin")]
    public class ReportManagementModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly ILogger<ReportManagementModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public ReportManagementModel(AuthenticatedHttpClientHelper clientHelper, ILogger<ReportManagementModel> logger, IConfiguration configuration, IWebHostEnvironment environment)
        {
            _clientHelper = clientHelper;
            _logger = logger;
            _configuration = configuration;
            _environment = environment;
        }

        // Lớp helper để parse phản hồi OData
        public class ODataResponse<T>
        {
            [JsonPropertyName("@odata.context")]
            public string Context { get; set; }

            [JsonPropertyName("value")]
            public List<T> Value { get; set; }

            [JsonPropertyName("@odata.count")]
            public int Count { get; set; }
        }

        public string ApiRootUrl { get; set; }
        public List<ReportViewModel> Reports { get; set; } = new(); // Thay đổi từ dynamic sang ReportViewModel để dễ quản lý
        public List<AdminViewModel> Admins { get; set; } = new();

        public int AllReportsCount { get; set; }
        public int MyTasksCount { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Tab { get; set; } = "all";

        [BindProperty(SupportsGet = true)]
        public string? SearchQuery { get; set; }

        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }

        public string? AccessToken { get; private set; }

        [BindProperty]
        public ReportActionInput ReportAction { get; set; }

        [TempData]
        public string? NotificationMessage { get; set; }
        [TempData]
        public string? NotificationType { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            AccessToken = HttpContext.Request.Cookies["AccessToken"];
            ApiRootUrl = _configuration.GetApiRootUrl(_environment);
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                // === SỬA LỖI Ở ĐÂY: Thay đổi thứ tự, lấy thông tin phụ trước ===
                // Lấy danh sách admin trước tiên
                try
                {
                    var adminsResponse = await client.GetFromJsonAsync<ApiResponse<List<AdminViewModel>>>($"{ApiRootUrl}/api/report/admins");
                    Admins = adminsResponse?.Data ?? new List<AdminViewModel>();
                    _logger.LogInformation("Successfully loaded {AdminCount} admins", Admins.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load admins list");
                    Admins = new List<AdminViewModel>();
                }

                // Lấy số lượng All Reports
                try
                {
                    var allCountResponse = await client.GetAsync($"{ApiRootUrl}/odata/unassigned?$count=true&$top=0");
                    if (allCountResponse.IsSuccessStatusCode)
                    {
                        var allContent = await allCountResponse.Content.ReadAsStringAsync();
                        _logger.LogInformation("All reports count API response: {Content}", allContent);
                        var allOdata = JsonSerializer.Deserialize<ODataResponse<ReportViewModel>>(allContent, jsonOptions);
                        AllReportsCount = allOdata?.Count ?? 0;
                    }
                    else
                    {
                        _logger.LogWarning("All reports count API failed with status: {StatusCode}", allCountResponse.StatusCode);
                        AllReportsCount = 0;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load all reports count");
                    AllReportsCount = 0;
                }

                // Lấy số lượng My Tasks
                try
                {
                    var myTasksCountResponse = await client.GetAsync($"{ApiRootUrl}/odata/mytasks?$count=true&$top=0");
                    if (myTasksCountResponse.IsSuccessStatusCode)
                    {
                        var myTasksContent = await myTasksCountResponse.Content.ReadAsStringAsync();
                        _logger.LogInformation("My tasks count API response: {Content}", myTasksContent);
                        var myTasksOdata = JsonSerializer.Deserialize<ODataResponse<ReportViewModel>>(myTasksContent, jsonOptions);
                        MyTasksCount = myTasksOdata?.Count ?? 0;
                    }
                    else
                    {
                        _logger.LogWarning("My tasks count API failed with status: {StatusCode}", myTasksCountResponse.StatusCode);
                        MyTasksCount = 0;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load my tasks count");
                    MyTasksCount = 0;
                }
                // === KẾT THÚC THAY ĐỔI THỨ TỰ ===

                // Logic chính để lấy dữ liệu cho tab hiện tại (để ở cuối)
                try
                {
                    var endpoint = Tab == "mytasks" ? "odata/mytasks" : "odata/unassigned";
                    var requestUrl = $"{endpoint}?$skip={(CurrentPage - 1) * PageSize}&$top={PageSize}&$count=true";
                    if (!string.IsNullOrEmpty(SearchQuery))
                    {
                        requestUrl += $"&$filter={System.Web.HttpUtility.UrlEncode(SearchQuery)}";
                    }

                    var fullUrl = ApiRootUrl + "/" + requestUrl;
                    _logger.LogInformation("Making request to: {Url}", fullUrl);

                    var response = await client.GetAsync(fullUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation("Reports API response: {Content}", content);
                        
                        var odataResponse = JsonSerializer.Deserialize<ODataResponse<ReportViewModel>>(content, jsonOptions);
                        if (odataResponse != null)
                        {
                            Reports = odataResponse.Value ?? new List<ReportViewModel>();
                            TotalCount = odataResponse.Count;
                            TotalPages = (int)Math.Ceiling((double)TotalCount / PageSize);
                            _logger.LogInformation("Successfully loaded {ReportCount} reports", Reports.Count);
                        }
                        else
                        {
                            _logger.LogWarning("OData response deserialization returned null");
                            Reports = new List<ReportViewModel>();
                            TotalCount = 0;
                            TotalPages = 0;
                        }
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("API call failed with status code {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                        
                        // Check for specific authorization errors
                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            NotificationMessage = "You are not authorized to access reports. Please ensure you have admin privileges and are logged in.";
                            NotificationType = "error";
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            NotificationMessage = "Access forbidden. Admin role required to view reports.";
                            NotificationType = "error";
                        }
                        else
                        {
                            NotificationMessage = $"Failed to load reports. Server returned: {response.StatusCode}";
                            NotificationType = "error";
                        }
                        
                        Reports = new List<ReportViewModel>();
                        TotalCount = 0;
                        TotalPages = 0;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception occurred while loading main reports data");
                    NotificationMessage = "An error occurred while loading reports data.";
                    NotificationType = "error";
                    Reports = new List<ReportViewModel>();
                    TotalCount = 0;
                    TotalPages = 0;
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load report management data.");
                
                // Set default values to prevent UI crashes
                Reports = new List<ReportViewModel>();
                Admins = new List<AdminViewModel>();
                AllReportsCount = 0;
                MyTasksCount = 0;
                TotalCount = 0;
                TotalPages = 0;
                
                // Determine more specific error message based on exception type
                if (ex is UnauthorizedAccessException || ex.Message.Contains("Unauthorized") || ex.Message.Contains("401"))
                {
                    NotificationMessage = "Authentication failed. Please log in again as an admin user.";
                }
                else if (ex.Message.Contains("Forbidden") || ex.Message.Contains("403"))
                {
                    NotificationMessage = "Access denied. Admin privileges required to access report management.";
                }
                else if (ex is HttpRequestException)
                {
                    NotificationMessage = "Unable to connect to the server. Please check your internet connection and try again.";
                }
                else
                {
                    NotificationMessage = "Could not load data from the server. Please try again later.";
                }
                
                NotificationType = "error";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostTakeTaskAsync(Guid reportId)
        {
            var client = await _clientHelper.GetAuthenticatedClientAsync();
            var response = await client.PostAsync($"{ApiRootUrl}/api/report/{reportId}/take", null);

            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<string>>();
            NotificationMessage = apiResponse?.Message;
            NotificationType = response.IsSuccessStatusCode ? "success" : "error";

            return RedirectToPage(new { Tab, CurrentPage, SearchQuery });
        }

        public async Task<JsonResult> OnGetReportDetailsAsync(Guid id)
{
    var client = await _clientHelper.GetAuthenticatedClientAsync();
    var rootUrl = _configuration.GetApiRootUrl(_environment);

    var options = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    // Gọi đúng API và deserialize ApiResponse<ReportViewModel>
    var apiResponse = await client.GetFromJsonAsync<ApiResponse<ReportViewModel>>(
        $"{rootUrl}/api/report/{id}", options);

    if (apiResponse?.Data == null)
    {
        _logger.LogWarning("Không tìm thấy report có ID {ReportId}", id);
        return new JsonResult(null, options); // Trả null để JS xử lý fallback
    }

    return new JsonResult(apiResponse.Data, options);
}



        public async Task<IActionResult> OnPostUpdateReportAsync()
        {
            string action = Request.Form["action"];

            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                HttpResponseMessage response;
                var rootUrl = _configuration.GetApiRootUrl(_environment);

                switch (action)
                {
                    case "assign":
                        var assignRequest = new { NewAdminId = ReportAction.NewAdminId };
                        response = await client.PutAsJsonAsync($"{rootUrl}/api/report/{ReportAction.ReportId}/assign", assignRequest);
                        break;
                    case "respond":
                        var respondRequest = new { ResponseMessage = ReportAction.ResponseMessage, NewStatus = ReportAction.NewStatus };
                        response = await client.PostAsJsonAsync($"{rootUrl}/api/report/{ReportAction.ReportId}/respond", respondRequest);
                        break;
                    case "updateStatus":
                    default:
                        var statusRequest = new { NewStatus = ReportAction.NewStatus };
                        response = await client.PutAsJsonAsync($"{rootUrl}/api/report/{ReportAction.ReportId}/status", statusRequest);
                        break;
                }

                var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<string>>();
                NotificationMessage = apiResponse?.Message;
                NotificationType = response.IsSuccessStatusCode ? "success" : "error";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating report from modal.");
                NotificationMessage = "An unexpected error occurred.";
                NotificationType = "error";
            }

            return RedirectToPage(new { Tab, CurrentPage, SearchQuery });
        }

        // Thêm phương thức này vào file ReportManagement.cshtml.cs
        // Sửa lại phương thức này trong ReportManagement.cshtml.cs
        public async Task<JsonResult> OnPostUpdateReportJsonAsync()
        {
            try
            {
                // Đọc dữ liệu JSON từ request body
                using var reader = new StreamReader(HttpContext.Request.Body);
                var body = await reader.ReadToEndAsync();

                // ⚠️ Thêm JsonStringEnumConverter để parse Enum từ chuỗi JSON
                var data = JsonSerializer.Deserialize<ReportActionInput>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                });

                if (data == null || string.IsNullOrWhiteSpace(data.Action))
                {
                    _logger.LogError("UpdateReportJsonAsync: Null or invalid data received: {@data}", data);
                    return new JsonResult(new { success = false, message = "Invalid request data." });
                }

                var client = await _clientHelper.GetAuthenticatedClientAsync();
                HttpResponseMessage response;
                var rootUrl = _configuration.GetApiRootUrl(_environment);

                switch (data.Action.ToLower())
                {
                    case "assign":
                        var assignRequest = new { NewAdminId = data.NewAdminId };
                        response = await client.PutAsJsonAsync($"{rootUrl}/api/report/{data.ReportId}/assign", assignRequest);
                        break;

                    case "respond":
                        var respondRequest = new { ResponseMessage = data.ResponseMessage, NewStatus = data.NewStatus };
                        response = await client.PostAsJsonAsync($"{rootUrl}/api/report/{data.ReportId}/respond", respondRequest);
                        break;

                    case "updatestatus":
                    default:
                        var statusRequest = new { NewStatus = data.NewStatus };
                        response = await client.PutAsJsonAsync($"{rootUrl}/api/report/{data.ReportId}/status", statusRequest);
                        break;
                }

                if (response.IsSuccessStatusCode)
                {
                    ApiResponse<string> apiResponse = null;
                    if (response.Content.Headers.ContentLength > 0)
                    {
                        apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<string>>();
                    }
                    string successMessage = apiResponse?.Message ?? "Action completed successfully.";

                    // ⚠️ Đọc lại report đã cập nhật, có thêm converter để parse enum nếu cần
                    var updatedReportResponse = await client.GetFromJsonAsync<ApiResponse<ReportViewModel>>(
                        $"{rootUrl}/api/report/{data.ReportId}", new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                        });

                    return new JsonResult(new
                    {
                        success = true,
                        message = successMessage,
                        data = updatedReportResponse?.Data
                    });
                }
                else
                {
                    var errorResponse = await response.Content.ReadFromJsonAsync<ApiResponse<string>>();
                    return new JsonResult(new
                    {
                        success = false,
                        message = errorResponse?.Message ?? "An error occurred."
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating report via JSON endpoint.");
                return new JsonResult(new
                {
                    success = false,
                    message = "An unexpected server error occurred."
                });
            }
        }



    }
}