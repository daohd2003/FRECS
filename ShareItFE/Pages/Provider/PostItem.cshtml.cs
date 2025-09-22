using BusinessObject.DTOs.ProductDto;
using BusinessObject.Enums;
// CategoryDto is in ProductDto namespace
// using BusinessObject.DTOs.CategoryDto;
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

        public List<CategoryDto> Categories { get; set; } = new List<CategoryDto>();
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
            if (TempData.Peek("Product") is string productData)
            {
                Product = JsonSerializer.Deserialize<ProductDTO>(productData, _jsonOptions);
            }
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

        public async Task<IActionResult> OnGetAsync()
        {
            if (!User.Identity.IsAuthenticated) return Page();
            await LoadCategoriesAsync();
            RestoreStateFromTempData();
            TempData.Keep();
            return Page();
        }

        // --- ĐIỀU HƯỚNG CÁC BƯỚC ---
        public async Task<IActionResult> OnPostPreviousAsync()
        {
            await LoadCategoriesAsync();
            RestoreStateFromTempData();
            CurrentStep = Math.Max(1, CurrentStep - 1);
            SaveStateToTempData();
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostNextAsync()
        {
            await LoadCategoriesAsync();

            // Lưu giá trị từ form trước khi restore
            var submittedName = Product.Name;
            var submittedDescription = Product.Description;
            var submittedSize = Product.Size;
            var submittedColor = Product.Color;
            var submittedCategoryId = Product.CategoryId;
            var submittedGender = Product.Gender;
            var submittedRentalStatus = Product.RentalStatus;
            var submittedPurchaseStatus = Product.PurchaseStatus;

            RestoreStateFromTempData();

            // Restore giá trị từ form
            if (!string.IsNullOrEmpty(submittedName)) Product.Name = submittedName;
            if (!string.IsNullOrEmpty(submittedDescription)) Product.Description = submittedDescription;
            if (!string.IsNullOrEmpty(submittedSize)) Product.Size = submittedSize;
            if (!string.IsNullOrEmpty(submittedColor)) Product.Color = submittedColor;
            if (submittedCategoryId != null && submittedCategoryId != Guid.Empty) Product.CategoryId = submittedCategoryId;
            if (!string.IsNullOrEmpty(submittedGender)) Product.Gender = submittedGender;
            if (!string.IsNullOrEmpty(submittedRentalStatus)) Product.RentalStatus = submittedRentalStatus;
            if (!string.IsNullOrEmpty(submittedPurchaseStatus)) Product.PurchaseStatus = submittedPurchaseStatus;

            if (CurrentStep == 1)
            {
                if (string.IsNullOrEmpty(Product.Name) || string.IsNullOrEmpty(Product.Description) || Product.CategoryId == Guid.Empty || string.IsNullOrEmpty(Product.Size))
                {
                    ModelState.AddModelError("Product", "Please fill all required fields in this step.");
                    SaveStateToTempData();
                    return Page();
                }
                if (Product.CategoryId == null || Product.CategoryId == Guid.Empty)
                {
                    ModelState.AddModelError("Product.CategoryId", "Please select a category.");
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
            // Lưu tất cả data từ form trước khi restore TempData
            var formData = new
            {
                Name = Product.Name,
                Description = Product.Description,
                CategoryId = Product.CategoryId,
                Size = Product.Size,
                Color = Product.Color,
                PricePerDay = Product.PricePerDay,
                PurchasePrice = Product.PurchasePrice,
                PurchaseQuantity = Product.PurchaseQuantity,
                RentalQuantity = Product.RentalQuantity,
                Gender = Product.Gender,
                RentalStatus = Product.RentalStatus,
                PurchaseStatus = Product.PurchaseStatus
            };

            // Load categories first để đảm bảo có data để map
            await LoadCategoriesAsync();

            RestoreStateFromTempData();

            // Restore tất cả giá trị từ form (ưu tiên form data hơn TempData)
            Product.Name = formData.Name;
            Product.Description = formData.Description;
            Product.CategoryId = formData.CategoryId;
            Product.Size = formData.Size;
            Product.Color = formData.Color;
            Product.PricePerDay = formData.PricePerDay;
            Product.PurchasePrice = formData.PurchasePrice;
            Product.PurchaseQuantity = formData.PurchaseQuantity;
            Product.RentalQuantity = formData.RentalQuantity;
            Product.Gender = formData.Gender;
            Product.RentalStatus = formData.RentalStatus;
            Product.PurchaseStatus = formData.PurchaseStatus;

            ModelState.Clear();
            
            // Validation cho 3 fields mới ở Step 3
            if (string.IsNullOrEmpty(Product.Gender))
                ModelState.AddModelError("Product.Gender", "Please select gender.");
                
            if (string.IsNullOrEmpty(Product.RentalStatus))
                ModelState.AddModelError("Product.RentalStatus", "Please select rental option.");
                
            if (string.IsNullOrEmpty(Product.PurchaseStatus))
                ModelState.AddModelError("Product.PurchaseStatus", "Please select purchase option.");
            
            // Validation conditional dựa trên rental/purchase status
            if (Product.RentalStatus == "Available")
            {
                if (Product.PricePerDay <= 0) 
                    ModelState.AddModelError("Product.PricePerDay", "Please enter a valid rental price.");
                if (Product.RentalQuantity <= 0)
                    ModelState.AddModelError("Product.RentalQuantity", "Please enter rental quantity.");
            }
            
            if (Product.PurchaseStatus == "Available")
            {
                if (Product.PurchasePrice <= 0)
                    ModelState.AddModelError("Product.PurchasePrice", "Please enter a valid purchase price.");
                if (Product.PurchaseQuantity <= 0)
                    ModelState.AddModelError("Product.PurchaseQuantity", "Please enter purchase quantity.");
            }
            
            // Validation cơ bản
            if (string.IsNullOrEmpty(PrimaryImageUrl)) ModelState.AddModelError("PrimaryImageUrl", "Primary image is required.");
            if (Product.CategoryId == null || Product.CategoryId == Guid.Empty)
                ModelState.AddModelError("Product.CategoryId", "Please select a category.");

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
                CategoryId = Product.CategoryId,
                Size = Product.Size,
                Color = Product.Color,
                PricePerDay = Product.PricePerDay,
                PurchasePrice = Product.PurchasePrice != 0 ? Product.PurchasePrice : 0,
                PurchaseQuantity = Product.PurchaseQuantity > 0 ? Product.PurchaseQuantity : 1,
                RentalQuantity = Product.RentalQuantity > 0 ? Product.RentalQuantity : 1,
                Gender = Product.Gender,
                RentalStatus = Product.RentalStatus,
                PurchaseStatus = Product.PurchaseStatus,
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

        private async Task LoadCategoriesAsync()
        {
            try
            {
                var client = await GetAuthenticatedClientAsync();
                var response = await client.GetAsync("api/categories");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var categories = JsonSerializer.Deserialize<List<CategoryDto>>(content, _jsonOptions);
                    Categories = categories ?? new List<CategoryDto>();
                }
                else
                {
                    // Fallback to default categories
                    Categories = GetDefaultCategories();
                }
            }
            catch (Exception)
            {
                // Log error if needed, fallback to default categories
                Categories = GetDefaultCategories();
            }
        }

        private List<CategoryDto> GetDefaultCategories()
        {
            return new List<CategoryDto>
            {
                new CategoryDto { Id = Guid.NewGuid(), Name = "Evening Wear", Description = "Evening wear", IsActive = true },
                new CategoryDto { Id = Guid.NewGuid(), Name = "Wedding Dresses", Description = "Wedding dresses", IsActive = true },
                new CategoryDto { Id = Guid.NewGuid(), Name = "Formal Suits", Description = "Formal suits", IsActive = true },
                new CategoryDto { Id = Guid.NewGuid(), Name = "Casual Wear", Description = "Casual wear", IsActive = true }
            };
        }

        private async Task<Guid?> GetCategoryIdByNameAsync(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName))
            {
                // Use first available category if no category selected
                return Categories.FirstOrDefault()?.Id;
            }

            try
            {
                var client = await GetAuthenticatedClientAsync();
                var response = await client.GetAsync($"api/categories/by-name/{Uri.EscapeDataString(categoryName)}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<CategoryDto>>(content, _jsonOptions);
                    return apiResponse?.Data?.Id;
                }
                else
                {
                    // Fallback: tìm trong danh sách Categories đã load
                    var category = Categories.FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
                    return category?.Id;
                }
            }
            catch (Exception)
            {
                // Fallback: tìm trong danh sách Categories đã load
                var category = Categories.FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
                return category?.Id;
            }
        }

        public class ApiResponse<T> { public T Data { get; set; } public string Message { get; set; } }
        public class ImageUploadResult { public string ImageUrl { get; set; } public string PublicId { get; set; } }
    }
}