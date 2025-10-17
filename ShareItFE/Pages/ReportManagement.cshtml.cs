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
                var adminsResponse = await client.GetFromJsonAsync<ApiResponse<List<AdminViewModel>>>($"{ApiRootUrl}/api/report/admins");
                Admins = adminsResponse?.Data ?? new List<AdminViewModel>();

                // Lấy số lượng All Reports
                var allCountResponse = await client.GetAsync($"{ApiRootUrl}/odata/unassigned?$count=true&$top=0");
                if (allCountResponse.IsSuccessStatusCode)
                {
                    var allContent = await allCountResponse.Content.ReadAsStringAsync();
                    var allOdata = JsonSerializer.Deserialize<ODataResponse<ReportViewModel>>(allContent, jsonOptions);
                    AllReportsCount = allOdata?.Count ?? 0;
                }

                // Lấy số lượng My Tasks
                var myTasksCountResponse = await client.GetAsync($"{ApiRootUrl}/odata/mytasks?$count=true&$top=0");
                if (myTasksCountResponse.IsSuccessStatusCode)
                {
                    var myTasksContent = await myTasksCountResponse.Content.ReadAsStringAsync();
                    var myTasksOdata = JsonSerializer.Deserialize<ODataResponse<ReportViewModel>>(myTasksContent, jsonOptions);
                    MyTasksCount = myTasksOdata?.Count ?? 0;
                }
                // === KẾT THÚC THAY ĐỔI THỨ TỰ ===

                // Logic chính để lấy dữ liệu cho tab hiện tại (để ở cuối)
                var endpoint = Tab == "mytasks" ? "odata/mytasks" : "odata/unassigned";
                var requestUrl = $"{endpoint}?$skip={(CurrentPage - 1) * PageSize}&$top={PageSize}&$count=true";
                if (!string.IsNullOrEmpty(SearchQuery))
                {
                    requestUrl += $"&$filter={System.Web.HttpUtility.UrlEncode(SearchQuery)}";
                }

                var response = await client.GetAsync(ApiRootUrl + "/" + requestUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    //var a = JsonSerializer.Deserialize<ReportViewModel>(content, jsonOptions);
                    //var odataResponse = JsonSerializer.Deserialize<ODataResponse<ReportViewModel>>(content, jsonOptions);
                    var json = await response.Content.ReadAsStringAsync();
                    var odataResponse = JsonSerializer.Deserialize<ODataResponse<ReportViewModel>>(json, jsonOptions);
                    if (odataResponse != null)
                    {
                        Reports = odataResponse.Value;
                        TotalCount = odataResponse.Count;
                        TotalPages = (int)Math.Ceiling((double)TotalCount / PageSize);
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"API call failed with status code {response.StatusCode}: {errorContent}");
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load report management data.");
                NotificationMessage = "Could not load data from the server. Please try again later.";
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