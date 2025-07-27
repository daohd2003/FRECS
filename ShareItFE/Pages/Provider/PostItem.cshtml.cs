using BusinessObject.DTOs.ProductDto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ShareItFE.Pages.Provider
{
    public class PostItemModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly JsonSerializerOptions _jsonOptions;

        public PostItemModel(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        [BindProperty] public int CurrentStep { get; set; } = 1;
        [BindProperty] public ProductDTO Product { get; set; } = new ProductDTO();
        [BindProperty] public string PrimaryImageUrl { get; set; }
        [BindProperty] public List<string> SecondaryImageUrls { get; set; } = new List<string>();

        public string PrimaryImagePublicId { get; set; }
        public List<string> SecondaryImagePublicIds { get; set; } = new List<string>();

        public List<string> Categories { get; } = new List<string> { "Evening Wear", "Wedding Dresses", "Cocktail Dresses", "Formal Suits", "Traditional Wear", "Casual Wear", "Accessories", "Kids Wear" };
        public List<string> Sizes { get; } = new List<string> { "XS", "S", "M", "L", "XL", "XXL", "2T", "3T", "4T", "5T", "6T" };
        public List<Step> Steps { get; } = new List<Step>
        {
            new Step { Number = 1, Title = "Basic Info", Description = "Item details and category" },
            new Step { Number = 2, Title = "Photos", Description = "Upload main and secondary images" },
            new Step { Number = 3, Title = "Pricing", Description = "Set rental rates" }
        };
        public class Step { public int Number { get; set; } public string Title { get; set; } public string Description { get; set; } }

        // --- QUẢN LÝ TRẠNG THÁI (TEMP DATA) ---
        private void RestoreStateFromTempData()
        {
            if (TempData.Peek("Product") is string productData) Product = JsonSerializer.Deserialize<ProductDTO>(productData, _jsonOptions);
            if (TempData.Peek("PrimaryImageUrl") is string primaryUrl) PrimaryImageUrl = primaryUrl;
            if (TempData.Peek("SecondaryImageUrls") is string secondaryUrlsData) SecondaryImageUrls = JsonSerializer.Deserialize<List<string>>(secondaryUrlsData, _jsonOptions) ?? new List<string>();
            if (TempData.Peek("PrimaryImagePublicId") is string primaryPublicId) PrimaryImagePublicId = primaryPublicId;
            if (TempData.Peek("SecondaryImagePublicIds") is string secondaryPublicIdsData) SecondaryImagePublicIds = JsonSerializer.Deserialize<List<string>>(secondaryPublicIdsData, _jsonOptions) ?? new List<string>();
            if (TempData.Peek("CurrentStep") is int currentStep) CurrentStep = currentStep;
        }

        private void SaveStateToTempData()
        {
            TempData["CurrentStep"] = CurrentStep;
            TempData["Product"] = JsonSerializer.Serialize(Product);
            TempData["PrimaryImageUrl"] = PrimaryImageUrl;
            TempData["SecondaryImageUrls"] = JsonSerializer.Serialize(SecondaryImageUrls);
            TempData["PrimaryImagePublicId"] = PrimaryImagePublicId;
            TempData["SecondaryImagePublicIds"] = JsonSerializer.Serialize(SecondaryImagePublicIds);
        }

        public IActionResult OnGet()
        {
            if (!User.Identity.IsAuthenticated) return Page();
            RestoreStateFromTempData();
            TempData.Keep();
            return Page();
        }

        // --- ĐIỀU HƯỚNG CÁC BƯỚC ---
        public IActionResult OnPostPrevious()
        {
            RestoreStateFromTempData();
            CurrentStep = Math.Max(1, CurrentStep - 1);
            SaveStateToTempData();
            return RedirectToPage();
        }

        public IActionResult OnPostNext()
        {
            RestoreStateFromTempData();

            if (CurrentStep == 1)
            {
                if (string.IsNullOrEmpty(Product.Name) || string.IsNullOrEmpty(Product.Description) || string.IsNullOrEmpty(Product.Category) || string.IsNullOrEmpty(Product.Size))
                {
                    ModelState.AddModelError("Product", "Please fill all required fields in this step.");
                    SaveStateToTempData();
                    return Page();
                }
            }
            if (CurrentStep == 2 && string.IsNullOrEmpty(PrimaryImageUrl))
            {
                ModelState.AddModelError("PrimaryImageUrl", "Please upload a primary image.");
                SaveStateToTempData();
                return Page();
            }

            CurrentStep = Math.Min(3, CurrentStep + 1);
            SaveStateToTempData();
            return RedirectToPage();
        }

        // --- XỬ LÝ ẢNH (AJAX) ---
        public async Task<IActionResult> OnPostUploadImages(IFormFileCollection imageFiles, [FromForm] bool isPrimary)
        {
            if (imageFiles == null || !imageFiles.Any()) return new JsonResult(new { success = false, message = "No files selected." });
            RestoreStateFromTempData();

            if (isPrimary && !string.IsNullOrEmpty(PrimaryImageUrl)) return new JsonResult(new { success = false, message = "Primary image already exists. Please remove it first." });
            if (!isPrimary && SecondaryImageUrls.Count + imageFiles.Count > 4) return new JsonResult(new { success = false, message = "Cannot exceed 4 secondary images." });

            var client = await GetAuthenticatedClientAsync();
            using var content = new MultipartFormDataContent();
            foreach (var file in imageFiles)
            {
                var streamContent = new StreamContent(file.OpenReadStream());
                content.Add(streamContent, "images", file.FileName);
            }

            var response = await client.PostAsync("api/providerUploadImages/upload-images", content);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return new JsonResult(new { success = false, message = $"API upload failed: {error}" });
            }

            var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<ImageUploadResult>>>(await response.Content.ReadAsStringAsync(), _jsonOptions);
            if (apiResponse?.Data == null) return new JsonResult(new { success = false, message = "API did not return image data." });

            if (isPrimary)
            {
                PrimaryImageUrl = apiResponse.Data[0].ImageUrl;
                PrimaryImagePublicId = apiResponse.Data[0].PublicId;
            }
            else
            {
                SecondaryImageUrls.AddRange(apiResponse.Data.Select(d => d.ImageUrl));
                SecondaryImagePublicIds.AddRange(apiResponse.Data.Select(d => d.PublicId));
            }

            SaveStateToTempData();
            return new JsonResult(new { success = true, primaryUrl = PrimaryImageUrl, secondaryUrls = SecondaryImageUrls });
        }

        /*  public IActionResult OnPostRemovePrimaryImage()
          {
              RestoreStateFromTempData();
              PrimaryImageUrl = null;
              PrimaryImagePublicId = null;
              SaveStateToTempData();
              return new JsonResult(new { success = true });
          }*/

        public async Task<IActionResult> OnPostRemovePrimaryImage()
        {
            RestoreStateFromTempData();

            // Lấy PublicId của ảnh cần xóa
            var publicIdToDelete = PrimaryImagePublicId;

            if (!string.IsNullOrEmpty(publicIdToDelete))
            {
                var client = await GetAuthenticatedClientAsync();
                await client.DeleteAsync($"api/ProviderUploadImages/delete-image?publicId={publicIdToDelete}");
            }

            PrimaryImageUrl = null;
            PrimaryImagePublicId = null;
            SaveStateToTempData();
            return new JsonResult(new { success = true });
        }
        /*  public IActionResult OnPostRemoveSecondaryImage(int index)
          {
              RestoreStateFromTempData();
              if (index >= 0 && index < SecondaryImageUrls.Count)
              {
                  SecondaryImageUrls.RemoveAt(index);
                  SecondaryImagePublicIds.RemoveAt(index);
              }
              SaveStateToTempData();
              return new JsonResult(new { success = true });
          }*/
        public async Task<IActionResult> OnPostRemoveSecondaryImage(int index)
        {
            RestoreStateFromTempData();

            if (index >= 0 && index < SecondaryImageUrls.Count)
            {
                var publicIdToDelete = SecondaryImagePublicIds[index];

                if (!string.IsNullOrEmpty(publicIdToDelete))
                {
                    var client = await GetAuthenticatedClientAsync();
                    // Gọi đến API DELETE
                    await client.DeleteAsync($"api/ProviderUploadImages/delete-image?publicId={publicIdToDelete}");
                }

                // Xóa thông tin khỏi trạng thái tạm
                SecondaryImageUrls.RemoveAt(index);
                SecondaryImagePublicIds.RemoveAt(index);
                SaveStateToTempData();
            }

            return new JsonResult(new { success = true });
        }

        // --- SUBMIT CUỐI CÙNG ---
        public async Task<IActionResult> OnPostSubmitAsync()
        {
            var submittedPrice = Product.PricePerDay;
            RestoreStateFromTempData();
            Product.PricePerDay = submittedPrice;
            ModelState.Clear();
            if (Product.PricePerDay <= 0) ModelState.AddModelError("Product.PricePerDay", "Please enter a valid price.");
            if (string.IsNullOrEmpty(PrimaryImageUrl)) ModelState.AddModelError("PrimaryImageUrl", "Primary image is required.");
            if (ModelState.ErrorCount > 0)
            {
                CurrentStep = string.IsNullOrEmpty(PrimaryImageUrl) ? 2 : 3;
                SaveStateToTempData();
                return Page();
            }

            // Tạo đối tượng ProductDTO để gửi đến API
            var productDtoToSend = new ProductRequestDTO
            {
                Name = Product.Name,
                Description = Product.Description,
                Category = Product.Category,
                Size = Product.Size,
                Color = Product.Color,
                PricePerDay = Product.PricePerDay,
                Images = new List<ProductImageDTO>()
            };

            productDtoToSend.Images.Add(new ProductImageDTO { ImageUrl = PrimaryImageUrl, IsPrimary = true });//PublicId = PrimaryImagePublicId,
            for (int i = 0; i < SecondaryImageUrls.Count; i++)
            {
                productDtoToSend.Images.Add(new ProductImageDTO { ImageUrl = SecondaryImageUrls[i], IsPrimary = false });//PublicId = SecondaryImagePublicIds[i],
            }

            var client = await GetAuthenticatedClientAsync();
            var jsonContent = new StringContent(JsonSerializer.Serialize(productDtoToSend), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("api/products", jsonContent);

            if (response.IsSuccessStatusCode)
            {
                TempData.Clear();
                TempData["SuccessMessage"] = "Your post has been successfully created and is awaiting moderation!";
                return RedirectToPage("/Products/Products");
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            TempData["ErrorMessage"] = $"Failed to post item: {errorContent}";
            SaveStateToTempData();
            return Page();
        }

        private async Task<HttpClient> GetAuthenticatedClientAsync()
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var token = _httpContextAccessor.HttpContext?.Request.Cookies["AccessToken"];
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            return client;
        }

        public class ApiResponse<T> { public T Data { get; set; } public string Message { get; set; } }
        public class ImageUploadResult { public string ImageUrl { get; set; } public string PublicId { get; set; } }
    }
}