using BusinessObject.DTOs.ProductDto;
// CategoryDto is in ProductDto namespace
// using BusinessObject.DTOs.CategoryDto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        [BindProperty] public bool IsEditMode { get; set; } = false;

        public string PrimaryImagePublicId { get; set; }
        public List<string> SecondaryImagePublicIds { get; set; } = new List<string>();

        public List<CategoryDto> Categories { get; set; } = new List<CategoryDto>();
        public List<string> Sizes { get; } = new List<string> { "XS", "S", "M", "L", "XL", "XXL" };
        public List<Step> Steps { get; } = new List<Step>
        {
            new Step { Number = 1, Title = "Basic Info", Description = "Item details and category" },
            new Step { Number = 2, Title = "Photos", Description = "Upload main and secondary images" },
            new Step { Number = 3, Title = "Pricing", Description = "Set rental rates" }
        };
        public class Step { public int Number { get; set; } public string Title { get; set; } public string Description { get; set; } }

        public class ODataApiResponse
        {
            [JsonPropertyName("@odata.count")]
            public int Count { get; set; }

            [JsonPropertyName("value")]
            public List<ProductDTO> Value { get; set; }
        }

        // --- QUẢN LÝ TRẠNG THÁI (TEMP DATA) ---
        private void RestoreStateFromTempData()
        {
            if (TempData.Peek("Product") is string productData)
            {
                Product = JsonSerializer.Deserialize<ProductDTO>(productData, _jsonOptions);
            }
            if (TempData.Peek("PrimaryImageUrl") is string primaryUrl)
            {
                PrimaryImageUrl = primaryUrl;
            }
            if (TempData.Peek("SecondaryImageUrls") is string secondaryUrlsData)
            {
                SecondaryImageUrls = JsonSerializer.Deserialize<List<string>>(secondaryUrlsData, _jsonOptions) ?? new List<string>();
            }
            if (TempData.Peek("PrimaryImagePublicId") is string primaryPublicId) PrimaryImagePublicId = primaryPublicId;
            if (TempData.Peek("SecondaryImagePublicIds") is string secondaryPublicIdsData) SecondaryImagePublicIds = JsonSerializer.Deserialize<List<string>>(secondaryPublicIdsData, _jsonOptions) ?? new List<string>();
            if (TempData.Peek("CurrentStep") is int currentStep) CurrentStep = currentStep;
            if (TempData.Peek("IsEditMode") is bool isEditMode) IsEditMode = isEditMode;

            // Đảm bảo đồng bộ giữa SecondaryImageUrls và SecondaryImagePublicIds
            SyncSecondaryImageCollections();
        }

        private void SyncSecondaryImageCollections()
        {
            // Đảm bảo cả hai list đều được khởi tạo
            if (SecondaryImageUrls == null) SecondaryImageUrls = new List<string>();
            if (SecondaryImagePublicIds == null) SecondaryImagePublicIds = new List<string>();

            // Nếu có sự chênh lệch về kích thước, điều chỉnh cho đồng bộ
            var urlCount = SecondaryImageUrls.Count;
            var publicIdCount = SecondaryImagePublicIds.Count;

            if (urlCount > publicIdCount)
            {
                // Thêm publicId trống cho các URL không có publicId
                for (int i = publicIdCount; i < urlCount; i++)
                {
                    SecondaryImagePublicIds.Add(string.Empty);
                }
            }
            else if (publicIdCount > urlCount)
            {
                // Cắt bớt publicIds thừa
                SecondaryImagePublicIds = SecondaryImagePublicIds.Take(urlCount).ToList();
            }
        }

        private void SaveStateToTempData()
        {
            TempData["CurrentStep"] = CurrentStep;
            TempData["Product"] = JsonSerializer.Serialize(Product);
            TempData["PrimaryImageUrl"] = PrimaryImageUrl;
            TempData["SecondaryImageUrls"] = JsonSerializer.Serialize(SecondaryImageUrls);
            TempData["PrimaryImagePublicId"] = PrimaryImagePublicId;
            TempData["SecondaryImagePublicIds"] = JsonSerializer.Serialize(SecondaryImagePublicIds);
            TempData["IsEditMode"] = IsEditMode;
        }

        public async Task<IActionResult> OnGetAsync(string? edit)
        {
            if (!User.Identity.IsAuthenticated) return Page();
            await LoadCategoriesAsync();
            RestoreStateFromTempData();

            // Load product for editing if edit parameter is provided
            if (!string.IsNullOrEmpty(edit))
            {
                if (Guid.TryParse(edit, out var productId))
                {
                    IsEditMode = true;
                    await LoadProductForEditAsync(productId);
                }
                else
                {
                    TempData["ErrorMessage"] = $"Invalid product ID format: {edit}";
                }
            }

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
            return RedirectToPage("/Provider/PostItem");
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

                // Clear ModelState trước khi validation để tránh lỗi cũ (luôn clear)
                ModelState.Clear();

                // Validation chỉ cho các field bắt buộc trong step 1
                if (string.IsNullOrEmpty(Product.Name))
                {
                    ModelState.AddModelError("Product.Name", "Item title is required.");
                }
                if (string.IsNullOrEmpty(Product.Description))
                {
                    ModelState.AddModelError("Product.Description", "Description is required.");
                }
                if (Product.CategoryId == null || Product.CategoryId == Guid.Empty)
                {
                    ModelState.AddModelError("Product.CategoryId", "Please select a category.");
                }
                if (string.IsNullOrEmpty(Product.Size))
                {
                    ModelState.AddModelError("Product.Size", "Please select a size.");
                }
                if (string.IsNullOrEmpty(Product.Color))
                {
                    ModelState.AddModelError("Product.Color", "Color is required.");
                }

                if (ModelState.ErrorCount > 0)
                {
                    SaveStateToTempData();
                    return Page();
                }
            }
            if (CurrentStep == 2)
            {

                // Clear ModelState trước khi validation step 2 (luôn clear để tránh lỗi cũ)
                ModelState.Clear();

                if (string.IsNullOrEmpty(PrimaryImageUrl))
                {
                    ModelState.AddModelError("PrimaryImageUrl", "Please upload a primary image.");
                    SaveStateToTempData();
                    return Page();
                }
            }

            CurrentStep = Math.Min(3, CurrentStep + 1);
            SaveStateToTempData();
            return RedirectToPage("/Provider/PostItem");
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


            // Đảm bảo cả hai list đều được khởi tạo
            if (SecondaryImageUrls == null) SecondaryImageUrls = new List<string>();
            if (SecondaryImagePublicIds == null) SecondaryImagePublicIds = new List<string>();

            // Kiểm tra index hợp lệ cho cả hai mảng
            if (index < 0 || index >= SecondaryImageUrls.Count)
            {
                return new JsonResult(new { success = false, message = $"Invalid index {index} for {SecondaryImageUrls.Count} images" });
            }

            // Lấy PublicId an toàn
            string publicIdToDelete = null;
            if (index < SecondaryImagePublicIds.Count)
            {
                publicIdToDelete = SecondaryImagePublicIds[index];
            }

            // Xóa từ Cloudinary nếu có PublicId
            if (!string.IsNullOrEmpty(publicIdToDelete))
            {
                try
                {
                    var client = await GetAuthenticatedClientAsync();
                    var response = await client.DeleteAsync($"api/ProviderUploadImages/delete-image?publicId={publicIdToDelete}");
                }
                catch (Exception)
                {
                    // Không return lỗi ở đây, vẫn tiếp tục xóa khỏi state local
                }
            }

            // Xóa thông tin khỏi trạng thái tạm
            SecondaryImageUrls.RemoveAt(index);

            // Xóa PublicId tương ứng nếu tồn tại
            if (index < SecondaryImagePublicIds.Count)
            {
                SecondaryImagePublicIds.RemoveAt(index);
            }

            SaveStateToTempData();

            return new JsonResult(new
            {
                success = true,
                secondaryUrls = SecondaryImageUrls,
                message = $"Image removed successfully. Remaining: {SecondaryImageUrls.Count}"
            });
        }

        // --- SUBMIT CUỐI CÙNG ---
        public async Task<IActionResult> OnPostSubmitAsync()
        {
            // Lưu form data trước (step 3 data)
            var formStep3Data = new
            {
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

            // Restore state từ TempData (step 1 & 2 data)
            RestoreStateFromTempData();

            // Merge form data (step 3) với TempData (step 1 & 2)
            // Form data ưu tiên hơn TempData
            if (formStep3Data.PricePerDay > 0)
                Product.PricePerDay = formStep3Data.PricePerDay;

            if (formStep3Data.PurchasePrice > 0)
                Product.PurchasePrice = formStep3Data.PurchasePrice;

            if (formStep3Data.PurchaseQuantity > 0)
                Product.PurchaseQuantity = formStep3Data.PurchaseQuantity;

            if (formStep3Data.RentalQuantity > 0)
                Product.RentalQuantity = formStep3Data.RentalQuantity;

            if (!string.IsNullOrEmpty(formStep3Data.Gender))
                Product.Gender = formStep3Data.Gender;

            if (!string.IsNullOrEmpty(formStep3Data.RentalStatus))
                Product.RentalStatus = formStep3Data.RentalStatus;

            if (!string.IsNullOrEmpty(formStep3Data.PurchaseStatus))
                Product.PurchaseStatus = formStep3Data.PurchaseStatus;


            // Set các field bắt buộc cho API
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                Product.ProviderId = Guid.Parse(userId);
            }

            // Set category name từ CategoryId
            if (Product.CategoryId.HasValue && Categories?.Any() == true)
            {
                var selectedCategory = Categories.FirstOrDefault(c => c.Id == Product.CategoryId.Value);
                Product.Category = selectedCategory?.Name ?? "";
            }

            // Set primary image URL
            Product.PrimaryImagesUrl = PrimaryImageUrl;

            // Set provider name
            Product.ProviderName = User.Identity?.Name ?? "Provider";

            // Set availability status (giữ nguyên cho edit, pending cho create)
            if (!IsEditMode)
            {
                Product.AvailabilityStatus = "pending";
            }

            // Validation tổng thể cho tất cả steps
            var validationErrors = new List<string>();

            // Step 1 validation
            if (string.IsNullOrEmpty(Product.Name))
                validationErrors.Add("Item title is required.");
            if (string.IsNullOrEmpty(Product.Description))
                validationErrors.Add("Description is required.");
            if (Product.CategoryId == null || Product.CategoryId == Guid.Empty)
                validationErrors.Add("Please select a category.");
            if (string.IsNullOrEmpty(Product.Size))
                validationErrors.Add("Please select a size.");
            if (string.IsNullOrEmpty(Product.Color))
                validationErrors.Add("Color is required.");

            // Step 2 validation
            if (string.IsNullOrEmpty(PrimaryImageUrl))
                validationErrors.Add("Primary image is required.");

            // Step 3 validation
            if (string.IsNullOrEmpty(Product.Gender))
                validationErrors.Add("Please select gender.");
            if (string.IsNullOrEmpty(Product.RentalStatus))
                validationErrors.Add("Please select rental option.");
            if (string.IsNullOrEmpty(Product.PurchaseStatus))
                validationErrors.Add("Please select purchase option.");

            // Validation conditional dựa trên rental/purchase status
            if (Product.RentalStatus == "Available")
            {
                if (Product.PricePerDay <= 0)
                    validationErrors.Add("Please enter a valid rental price.");
                if (Product.RentalQuantity <= 0)
                    validationErrors.Add("Please enter rental quantity.");
            }

            if (Product.PurchaseStatus == "Available")
            {
                if (Product.PurchasePrice <= 0)
                    validationErrors.Add("Please enter a valid purchase price.");
                if (Product.PurchaseQuantity <= 0)
                    validationErrors.Add("Please enter purchase quantity.");
            }

            // Nếu có lỗi validation, hiển thị tất cả
            if (validationErrors.Any())
            {
                foreach (var error in validationErrors)
                {
                    ModelState.AddModelError("", error);
                }

                // Quay về step có lỗi đầu tiên
                if (string.IsNullOrEmpty(Product.Name) || string.IsNullOrEmpty(Product.Description) ||
                    Product.CategoryId == Guid.Empty || string.IsNullOrEmpty(Product.Size) || string.IsNullOrEmpty(Product.Color))
                    CurrentStep = 1;
                else if (string.IsNullOrEmpty(PrimaryImageUrl))
                    CurrentStep = 2;
                else
                    CurrentStep = 3;

                SaveStateToTempData();
                return Page();
            }


            // Tạo đối tượng ProductDTO để gửi đến API
            var productDtoToSend = new ProductRequestDTO
            {
                ProviderId = Product.ProviderId,
                Name = Product.Name,
                Description = Product.Description,
                CategoryId = Product.CategoryId,
                Size = Product.Size,
                Color = Product.Color,
                PricePerDay = Product.RentalStatus == "Available" ? Product.PricePerDay : 0,
                PurchasePrice = Product.PurchaseStatus == "Available" ? Product.PurchasePrice : 0,
                PurchaseQuantity = Product.PurchaseStatus == "Available" ? Product.PurchaseQuantity : 0,
                RentalQuantity = Product.RentalStatus == "Available" ? Product.RentalQuantity : 0,
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

            HttpResponseMessage response;

            if (IsEditMode && Product.Id != Guid.Empty)
            {
                // Update existing product
                response = await client.PutAsync($"api/products/{Product.Id}", jsonContent);
            }
            else
            {
                // Create new product
                response = await client.PostAsync("api/products", jsonContent);
            }

            if (response.IsSuccessStatusCode)
            {
                TempData.Clear();
                if (IsEditMode && Product.Id != Guid.Empty)
                {
                    TempData["SuccessMessage"] = "Your product has been successfully updated!";
                }
                else
                {
                    TempData["SuccessMessage"] = "Your post has been successfully created!";
                }
                return RedirectToPage("/Provider/Products");
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            TempData["ErrorMessage"] = $"Failed to {(IsEditMode ? "update" : "create")} item: {errorContent}";
            SaveStateToTempData();
            return Page();
        }

        private async Task LoadProductForEditAsync(Guid productId)
        {
            try
            {
                var client = await GetAuthenticatedClientAsync();

                // Try OData endpoint first (same as Products listing) with Images included
                var odataEndpoint = $"odata/products?$filter=Id eq {productId}&$expand=Images";
                var response = await client.GetAsync(odataEndpoint);

                if (!response.IsSuccessStatusCode)
                {
                    // Fallback to regular API endpoint
                    var apiEndpoint = $"api/products/{productId}";
                    response = await client.GetAsync(apiEndpoint);
                }

                if (response.IsSuccessStatusCode)
                {
                    var productData = await response.Content.ReadAsStringAsync();

                    // Try different deserialization approaches
                    ProductDTO existingProduct = null;

                    // First try OData response format (same as Products listing)
                    try
                    {
                        var odataResponse = JsonSerializer.Deserialize<ODataApiResponse>(productData, _jsonOptions);
                        if (odataResponse?.Value?.Any() == true)
                        {
                            existingProduct = odataResponse.Value.First();
                        }
                    }
                    catch (Exception)
                    {
                        // Try ApiResponse format
                        try
                        {
                            var apiResponse = JsonSerializer.Deserialize<ApiResponse<ProductDTO>>(productData, _jsonOptions);
                            if (apiResponse?.Data != null)
                            {
                                existingProduct = apiResponse.Data;
                            }
                        }
                        catch (Exception)
                        {
                            // Finally try direct ProductDTO
                            try
                            {
                                existingProduct = JsonSerializer.Deserialize<ProductDTO>(productData, _jsonOptions);
                            }
                            catch (Exception)
                            {
                                TempData["ErrorMessage"] = "Failed to load product data for editing.";
                                return;
                            }
                        }
                    }

                    if (existingProduct != null)
                    {

                        // Load product data into form
                        Product.Id = existingProduct.Id;
                        Product.Name = existingProduct.Name;
                        Product.Description = existingProduct.Description;
                        Product.CategoryId = existingProduct.CategoryId;
                        Product.Size = existingProduct.Size;
                        Product.Color = existingProduct.Color;
                        Product.Gender = existingProduct.Gender;
                        
                        // Only load prices and quantities if status is Available
                        Product.PricePerDay = existingProduct.RentalStatus == "Available" ? existingProduct.PricePerDay : 0;
                        Product.PurchasePrice = existingProduct.PurchaseStatus == "Available" ? existingProduct.PurchasePrice : 0;
                        Product.RentalQuantity = existingProduct.RentalStatus == "Available" ? existingProduct.RentalQuantity : 0;
                        Product.PurchaseQuantity = existingProduct.PurchaseStatus == "Available" ? existingProduct.PurchaseQuantity : 0;
                        Product.RentalStatus = existingProduct.RentalStatus;
                        Product.PurchaseStatus = existingProduct.PurchaseStatus;

                        // Load images
                        if (existingProduct.Images?.Any() == true)
                        {
                            var primaryImage = existingProduct.Images.FirstOrDefault(i => i.IsPrimary);
                            if (primaryImage != null)
                            {
                                PrimaryImageUrl = primaryImage.ImageUrl;
                            }

                            SecondaryImageUrls = existingProduct.Images
                                .Where(i => !i.IsPrimary)
                                .Select(i => i.ImageUrl)
                                .ToList();
                        }
                        else
                        {
                            // Fallback: try to get from PrimaryImagesUrl if available
                            if (!string.IsNullOrEmpty(existingProduct.PrimaryImagesUrl))
                            {
                                PrimaryImageUrl = existingProduct.PrimaryImagesUrl;
                            }
                        }

                        // Set to step 1 for editing
                        CurrentStep = 1;
                        IsEditMode = true;

                        // Save state
                        SaveStateToTempData();
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Failed to deserialize product data from API response";
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    TempData["ErrorMessage"] = $"API Error {response.StatusCode}: {errorContent}";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading product for editing: {ex.Message}";
            }
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