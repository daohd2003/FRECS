using AutoMapper;
using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.PagingDto;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;
using Services.ProductServices;
using Services.ContentModeration;
using Services.ConversationServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/products")]
    [ApiController]
        [Authorize(Roles = "admin,provider,staff")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _service;
        private readonly IMapper _mapper;
        private readonly IContentModerationService _moderationService;
        private readonly IConversationService _conversationService;

        public ProductController(
            IProductService service, 
            IMapper mapper,
            IContentModerationService moderationService,
            IConversationService conversationService)
        {
            _service = service;
            _mapper = mapper;
            _moderationService = moderationService;
            _conversationService = conversationService;
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(Guid id)
        {
            var product = await _service.GetByIdAsync(id);
            if (product == null) return NotFound(new ApiResponse<string>("Product not found", null));
            return Ok(new ApiResponse<ProductDTO>("Product retrieved successfully", product));
        }

        [HttpGet()]
        [AllowAnonymous]
        public  IActionResult GetAll()
        {
            IQueryable<ProductDTO> products = _service.GetAll();
            if (products == null) return NotFound();
            return Ok(products);
        }

        [HttpGet("filter")] 
        public async Task<ActionResult<PagedResult<ProductDTO>>> GetProductsAsync(
            [FromQuery] string? searchTerm,
            [FromQuery] string status,
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 5) 
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 5; 

            if (searchTerm == "\"\"")
            {
                searchTerm = string.Empty;
            }

            IQueryable<ProductDTO> products = _service.GetAll(); 

            var query = products.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerSearchTerm = searchTerm.ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(lowerSearchTerm) ||
                    (p.Description != null && p.Description.ToLower().Contains(lowerSearchTerm))
                );
            }

            if (!string.IsNullOrWhiteSpace(status) && status.ToLower() != "all")
            {
                var lowerStatus = status.ToLower();
                query = query.Where(p =>
                    p.AvailabilityStatus.ToLower().Equals(lowerStatus)
                );
            }

            var totalCount = query.Count();

            var items = query
                .OrderByDescending(p => p.CreatedAt) 
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList(); 

            var pagedResult = new PagedResult<ProductDTO>
            {
                Items = items,
                TotalCount = totalCount,
                CurrentPage = page,
                PageSize = pageSize
            };

            return Ok(pagedResult);
        }



        /* [HttpPost]
         public async Task<IActionResult> Create([FromBody] ProductDTO dto)
         {
             var created = await _service.AddAsync(dto);
             return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
         }*/
        /// <summary>
        /// Create a new product with AUTOMATIC AI content moderation
        /// Product will be checked BEFORE being made available to customers
        /// </summary>
        [HttpPost]
        [Authorize] // Đảm bảo người dùng đã đăng nhập
        public async Task<IActionResult> Create([FromBody] ProductRequestDTO dto)
        {
            try
            {
                // Lấy thông tin Provider từ token
                dto.ProviderId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                // SYNCHRONOUS AI moderation check happens inside AddAsync
                // Provider will wait 2-3 seconds for AI check to complete
                var createdProduct = await _service.AddAsync(dto);

                // Return different messages based on moderation result
                if (createdProduct.AvailabilityStatus.ToLower() == "pending")
                {
                    // Product was flagged by AI
                    return StatusCode(201, new ApiResponse<ProductDTO>(
                        "Product created but flagged for review. " +
                        "Your product contains content that may violate our guidelines and has been set to PENDING. " +
                        "Please check your email for details and make necessary corrections. " +
                        "The product will NOT be visible to customers until approved by staff.",
                        createdProduct
                    ));
                }
                else
                {
                    // Product passed AI check
                    return CreatedAtAction(nameof(GetById), new { id = createdProduct.Id }, 
                        new ApiResponse<ProductDTO>(
                            "Product created successfully and is now AVAILABLE to customers. " +
                            "Your product passed our automated content moderation check.",
                            createdProduct
                        ));
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("community guidelines"))
            {
                // Trả về lỗi rõ ràng cho trường hợp vi phạm content
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>(
                    "Failed to create product: " + ex.Message, 
                    null
                ));
            }
        }

        [HttpPut("{id}")]
        [Authorize] // Đảm bảo người dùng đã đăng nhập
        public async Task<IActionResult> Update(Guid id, [FromBody] ProductRequestDTO dto)
        {
            try
            {
                // Đảm bảo ProviderId từ token
                dto.ProviderId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                
                // Convert ProductRequestDTO to ProductDTO để update
                var productDto = new ProductDTO
                {
                    Id = id,
                    ProviderId = dto.ProviderId,
                    Name = dto.Name,
                    Description = dto.Description,
                    CategoryId = dto.CategoryId,
                    Category = dto.Category,
                    Size = dto.Size,
                    Color = dto.Color,
                    PricePerDay = dto.PricePerDay,
                    PurchasePrice = dto.PurchasePrice ?? 0,
                    PurchaseQuantity = dto.PurchaseQuantity ?? 0,
                    RentalQuantity = dto.RentalQuantity ?? 0,
                    SecurityDeposit = dto.SecurityDeposit,
                    Gender = dto.Gender,
                    RentalStatus = dto.RentalStatus,
                    PurchaseStatus = dto.PurchaseStatus,
                    Images = dto.Images
                };

                var result = await _service.UpdateAsync(productDto);
                if (!result) return NotFound("Product not found or update failed.");
                
                // Check if images were updated
                var imagesUpdated = dto.Images?.Any() == true;
                var message = imagesUpdated 
                    ? "Product updated successfully. Old images have been automatically deleted from Cloudinary."
                    : "Product updated successfully.";
                
                return Ok(new ApiResponse<string>(message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>(ex.Message, null));
            }
        }

        /// <summary>
        /// Admin/Staff: Update product without changing ProviderId
        /// </summary>
        [HttpPut("admin/{id}")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> AdminUpdate(Guid id, [FromBody] AdminProductUpdateDTO dto)
        {
            try
            {
                // Get existing product first to preserve ProviderId
                var existingProduct = await _service.GetByIdAsync(id);
                if (existingProduct == null) 
                    return NotFound(new ApiResponse<string>("Product not found.", null));
                
                var currentStatus = existingProduct.AvailabilityStatus.ToLower();
                var isArchivedOrDeleted = currentStatus == "archived" || currentStatus == "deleted";

                // If product is archived, only allow status change.
                if (isArchivedOrDeleted)
                {
                    // Check if any field other than AvailabilityStatus was changed.
                    // The DTO contains all form fields, so we compare them to the current DB values.
                    bool otherFieldsChanged =
                        dto.Name != existingProduct.Name ||
                        dto.Description != existingProduct.Description ||
                        dto.CategoryId != existingProduct.CategoryId ||
                        dto.Size != existingProduct.Size ||
                        dto.Color != existingProduct.Color ||
                        dto.PricePerDay != existingProduct.PricePerDay ||
                        (dto.PurchasePrice ?? 0) != existingProduct.PurchasePrice ||
                        (dto.PurchaseQuantity ?? 0) != existingProduct.PurchaseQuantity ||
                        (dto.RentalQuantity ?? 0) != existingProduct.RentalQuantity ||
                        dto.SecurityDeposit != existingProduct.SecurityDeposit ||
                        dto.RentalStatus != existingProduct.RentalStatus ||
                        dto.PurchaseStatus != existingProduct.PurchaseStatus ||
                        dto.Gender != existingProduct.Gender;

                    if (otherFieldsChanged)
                    {
                        return BadRequest(new ApiResponse<string>(
                            "This product is archived. To edit its details, first change its status to an active state (like 'Available'), save the change, and then edit other details.", 
                            null));
                    }
                }
                
                // ✅ CHECK CONTENT MODERATION - ONLY when changing to "Available" status
                // Skip moderation check if:
                // 1. Changing to Archived/Deleted (allow archiving/deleting without check)
                // 2. Status is Pending and changing to Archived (allow staff to archive violated products)
                var newStatus = dto.AvailabilityStatus ?? existingProduct.AvailabilityStatus;
                var isChangingToAvailable = newStatus.Equals("Available", StringComparison.OrdinalIgnoreCase);
                var isChangingToArchived = newStatus.Equals("Archived", StringComparison.OrdinalIgnoreCase) || 
                                          newStatus.Equals("Deleted", StringComparison.OrdinalIgnoreCase);
                
                // Only check moderation when:
                // - Changing TO "Available" status (must ensure content is appropriate)
                // - OR updating Name/Description while ALREADY in "Available" status
                bool shouldCheckModeration = isChangingToAvailable || 
                    (currentStatus == "available" && !isChangingToArchived);
                
                if (shouldCheckModeration)
                {
                    var moderationResult = await _moderationService.CheckProductContentAsync(dto.Name, dto.Description);
                    
                    if (!moderationResult.IsAppropriate)
                    {
                        // Return 400 with violation details
                        return BadRequest(new ApiResponse<ContentModerationResultDTO>(
                            "Product contains inappropriate content and cannot be updated.",
                            moderationResult
                        ));
                    }
                }
                
                // Update fields but KEEP original ProviderId
                var productDto = new ProductDTO
                {
                    Id = id,
                    ProviderId = existingProduct.ProviderId, // Preserve original provider
                    Name = dto.Name,
                    Description = dto.Description,
                    CategoryId = dto.CategoryId,
                    Category = dto.Category,
                    Size = dto.Size,
                    Color = dto.Color,
                    PricePerDay = dto.PricePerDay,
                    PurchasePrice = dto.PurchasePrice ?? 0,
                    PurchaseQuantity = dto.PurchaseQuantity ?? 0,
                    RentalQuantity = dto.RentalQuantity ?? 0,
                    SecurityDeposit = dto.SecurityDeposit,
                    Gender = dto.Gender,
                    RentalStatus = dto.RentalStatus,
                    PurchaseStatus = dto.PurchaseStatus,
                    AvailabilityStatus = dto.AvailabilityStatus ?? existingProduct.AvailabilityStatus,
                    Images = existingProduct.Images // Admin keeps existing images
                };

                var result = await _service.UpdateAsync(productDto);
                if (!result) return BadRequest(new ApiResponse<string>("Update failed.", null));
                
                return Ok(new ApiResponse<string>("Product updated successfully.", null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>(ex.Message, null));
            }
        }

        [HttpPut("update-status/{id}")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] ProductStatusUpdateDto request)
        {
            if (id != request.ProductId)
            {
                return BadRequest("Product ID in route does not match Product ID in request body.");
            }

            var result = await _service.UpdateProductStatusAsync(
               request
            );

            if (!result)
            {
                return NotFound("Product not found or status update failed.");
            }

            return NoContent();
        }

        [HttpGet("by-type/{productType}")]
        [AllowAnonymous]
        public IActionResult GetByProductType(string productType)
        {
            var products = _service.GetAll();
            
            var filteredProducts = productType.ToUpper() switch
            {
                "BOTH" => products.Where(p => p.ProductType == "BOTH"),
                "RENTAL" => products.Where(p => p.ProductType == "RENTAL"),
                "PURCHASE" => products.Where(p => p.ProductType == "PURCHASE"),
                "UNAVAILABLE" => products.Where(p => p.ProductType == "UNAVAILABLE"),
                _ => products
            };

            var result = filteredProducts.Select(p => new
            {
                p.Id,
                p.Name,
                p.ProductType,
                p.PricePerDay,
                p.PurchasePrice,
                p.SecurityDeposit,
                IsRentalAvailable = p.IsRentalAvailable,
                IsPurchaseAvailable = p.IsPurchaseAvailable,
                PrimaryPrice = p.GetPrimaryPriceDisplay(),
                Stats = p.GetStatsDisplay(),
                Deposit = p.GetDepositDisplay()
            }).ToList();

            return Ok(new
            {
                ProductType = productType.ToUpper(),
                Count = result.Count,
                Products = result
            });
        }

        [HttpDelete("{id}")]
        [Authorize] // Chỉ provider được xóa sản phẩm của mình
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var providerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                
                // Get product để check ownership
                var product = await _service.GetByIdAsync(id);
                if (product == null) 
                    return NotFound(new ApiResponse<string>("Product not found.", null));
                
                // Check ownership
                if (product.ProviderId != providerId)
                    return Forbid("You can only delete your own products.");
                
                // Kiểm tra xem product có references không
                var hasOrderItems = await _service.HasOrderItemsAsync(id);
                var hasTransactions = product.RentCount > 0 || product.BuyCount > 0;
                
                // TẤT CẢ sản phẩm khi xóa đều chuyển sang archived
                product.AvailabilityStatus = "archived";
                var updateResult = await _service.UpdateAsync(product);
                
                if (!updateResult)
                    return BadRequest(new ApiResponse<string>("Failed to archive product.", null));
                
                // Tạo message chi tiết nếu có references
                string message;
                if (hasOrderItems || hasTransactions)
                {
                    var reasons = new List<string>();
                    if (hasOrderItems) reasons.Add("has order history");
                    if (product.RentCount > 0) reasons.Add($"{product.RentCount} rental(s)");
                    if (product.BuyCount > 0) reasons.Add($"{product.BuyCount} purchase(s)");
                    
                    message = $"Product archived successfully. Reason: {string.Join(", ", reasons)}.";
                }
                else
                {
                    message = "Product archived successfully.";
                }
                
                return Ok(new ApiResponse<string>(message, "Archived"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error: {ex.Message}", null));
            }
        }

        [HttpPut("restore/{id}")]
        [Authorize] // Chỉ provider được restore sản phẩm của mình
        public async Task<IActionResult> RestoreProduct(Guid id)
        {
            try
            {
                var providerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                
                // Get product để check ownership
                var product = await _service.GetByIdAsync(id);
                if (product == null) 
                    return NotFound("Product not found.");
                
                // Check ownership
                if (product.ProviderId != providerId)
                    return Forbid("You can only restore your own products.");
                
                // Check if product can be restored
                if (product.AvailabilityStatus != "archived" && product.AvailabilityStatus != "deleted")
                    return BadRequest("Only archived or deleted products can be restored.");
                
                // Restore product - change status to available
                product.AvailabilityStatus = "available";
                var result = await _service.UpdateAsync(product);
                
                if (!result)
                    return BadRequest("Failed to restore product.");
                
                return Ok(new ApiResponse<string>("Product has been restored to active status.", "Restored"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>(ex.Message, null));
            }
        }

        /// <summary>
        /// Update product images - automatically deletes old images from Cloudinary
        /// </summary>
        [HttpPut("{id}/images")]
        [Authorize] // Chỉ provider được update ảnh sản phẩm của mình
        public async Task<IActionResult> UpdateProductImages(Guid id, [FromBody] List<ProductImageDTO> newImages)
        {
            try
            {
                var providerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                
                // Get existing product để check ownership
                var existingProduct = await _service.GetByIdAsync(id);
                if (existingProduct == null)
                    return NotFound(new ApiResponse<string>("Product not found.", null));
                
                // Check ownership
                if (existingProduct.ProviderId != providerId)
                    return Forbid("You can only update images of your own products.");
                
                // Validate images
                if (newImages == null || !newImages.Any())
                    return BadRequest(new ApiResponse<string>("At least one image is required.", null));
                
                // Check if at least one image is marked as primary
                if (!newImages.Any(img => img.IsPrimary))
                    return BadRequest(new ApiResponse<string>("At least one image must be marked as primary.", null));
                
                // Call service method to update images (Repository → Service → Controller pattern)
                var result = await _service.UpdateProductImagesAsync(id, newImages);
                if (!result)
                    return BadRequest(new ApiResponse<string>("Failed to update product images.", null));
                
                return Ok(new ApiResponse<string>(
                    "Product images updated successfully. Old images have been automatically deleted from Cloudinary.", 
                    null
                ));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error updating images: {ex.Message}", null));
            }
        }

        // ============================================
        // ADMIN ENDPOINTS
        // ============================================

        /// <summary>
        /// Admin: Get product statistics
        /// </summary>
        [HttpGet("admin/statistics")]
        [Authorize(Roles = "admin,staff")]
        public IActionResult GetStatistics()
        {
            try
            {
                var allProducts = _service.GetAll();
                
                var stats = new
                {
                    TotalProducts = allProducts.Count(),
                    Available = allProducts.Count(p => p.AvailabilityStatus.ToLower() == "available"),
                    Pending = allProducts.Count(p => p.AvailabilityStatus.ToLower() == "pending"),
                    Rejected = allProducts.Count(p => p.AvailabilityStatus.ToLower() == "rejected"),
                    Archived = allProducts.Count(p => p.AvailabilityStatus.ToLower() == "archived"),
                    Deleted = allProducts.Count(p => p.AvailabilityStatus.ToLower() == "deleted"),
                    TotalRented = allProducts.Sum(p => p.RentCount),
                    TotalSold = allProducts.Sum(p => p.BuyCount)
                };
                
                return Ok(new ApiResponse<object>("Statistics retrieved successfully.", stats));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>(ex.Message, null));
            }
        }

        /// <summary>
        /// Admin: Get all products with filtering
        /// </summary>
        [HttpGet("admin/all")]
        [Authorize(Roles = "admin,staff")]
        public IActionResult GetAllForAdmin(
            [FromQuery] string? searchTerm,
            [FromQuery] string? availabilityStatus,
            [FromQuery] Guid? providerId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;

                var query = _service.GetAll();

                // Filter by search term
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var lowerSearchTerm = searchTerm.ToLower();
                    query = query.Where(p =>
                        p.Name.ToLower().Contains(lowerSearchTerm) ||
                        (p.Description != null && p.Description.ToLower().Contains(lowerSearchTerm)) ||
                        p.ProviderName.ToLower().Contains(lowerSearchTerm)
                    );
                }

                // Filter by availability status
                if (!string.IsNullOrWhiteSpace(availabilityStatus) && availabilityStatus.ToLower() != "all")
                {
                    var lowerStatus = availabilityStatus.ToLower();
                    query = query.Where(p => p.AvailabilityStatus.ToLower() == lowerStatus);
                }

                // Filter by provider
                if (providerId.HasValue)
                {
                    query = query.Where(p => p.ProviderId == providerId.Value);
                }

                var totalCount = query.Count();
                var items = query
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var pagedResult = new PagedResult<ProductDTO>
                {
                    Items = items,
                    TotalCount = totalCount,
                    CurrentPage = page,
                    PageSize = pageSize
                };

                return Ok(new ApiResponse<PagedResult<ProductDTO>>("Products retrieved successfully.", pagedResult));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>(ex.Message, null));
            }
        }

        /// <summary>
        /// Admin: Get pending products
        /// </summary>
        [HttpGet("admin/pending")]
        [Authorize(Roles = "admin,staff")]
        public IActionResult GetPendingProducts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;

                var query = _service.GetAll()
                    .Where(p => p.AvailabilityStatus.ToLower() == "pending");

                var totalCount = query.Count();
                var items = query
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var pagedResult = new PagedResult<ProductDTO>
                {
                    Items = items,
                    TotalCount = totalCount,
                    CurrentPage = page,
                    PageSize = pageSize
                };

                return Ok(new ApiResponse<PagedResult<ProductDTO>>("Pending products retrieved successfully.", pagedResult));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>(ex.Message, null));
            }
        }

        /// <summary>
        /// Admin: Get products by provider
        /// </summary>
        [HttpGet("admin/by-provider")]
        [Authorize(Roles = "admin,staff")]
        public IActionResult GetProductsByProvider(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;

                var allProducts = _service.GetAll();
                
                var groupedByProvider = allProducts
                    .GroupBy(p => new { p.ProviderId, p.ProviderName })
                    .Select(g => new
                    {
                        ProviderId = g.Key.ProviderId,
                        ProviderName = g.Key.ProviderName,
                        TotalProducts = g.Count(),
                        AvailableProducts = g.Count(p => p.AvailabilityStatus.ToLower() == "available"),
                        PendingProducts = g.Count(p => p.AvailabilityStatus.ToLower() == "pending"),
                        RejectedProducts = g.Count(p => p.AvailabilityStatus.ToLower() == "rejected"),
                        Products = g.OrderByDescending(p => p.CreatedAt).ToList()
                    })
                    .OrderByDescending(g => g.TotalProducts)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var totalProviders = allProducts
                    .Select(p => p.ProviderId)
                    .Distinct()
                    .Count();

                var result = new
                {
                    TotalProviders = totalProviders,
                    CurrentPage = page,
                    PageSize = pageSize,
                    Data = groupedByProvider
                };

                return Ok(new ApiResponse<object>("Products grouped by provider retrieved successfully.", result));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>(ex.Message, null));
            }
        }

        /// <summary>
        /// Admin: Approve or reject a product
        /// </summary>
        [HttpPut("admin/approve-reject/{id}")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> ApproveOrRejectProduct(
            Guid id,
            [FromBody] ProductStatusUpdateDto request)
        {
            try
            {
                if (id != request.ProductId)
                {
                    return BadRequest("Product ID mismatch.");
                }

                var result = await _service.UpdateProductStatusAsync(request);
                
                if (!result)
                {
                    return NotFound("Product not found or status update failed.");
                }

                return Ok(new ApiResponse<string>("Product status updated successfully.", request.NewAvailabilityStatus));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>(ex.Message, null));
            }
        }

        /// <summary>
        /// Admin: Delete any product (hard delete or archive)
        /// </summary>
        [HttpDelete("admin/delete/{id}")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> AdminDeleteProduct(Guid id, [FromQuery] bool hardDelete = false)
        {
            try
            {
                var product = await _service.GetByIdAsync(id);
                if (product == null) 
                    return NotFound("Product not found.");

                if (hardDelete)
                {
                    // Hard delete
                    var deleteResult = await _service.DeleteAsync(id);
                    if (!deleteResult)
                        return BadRequest("Failed to delete product.");
                    
                    return Ok(new ApiResponse<string>("Product permanently deleted.", "Deleted"));
                }
                else
                {
                    // Soft delete - archive
                    product.AvailabilityStatus = "archived";
                    var updateResult = await _service.UpdateAsync(product);
                    
                    if (!updateResult)
                        return BadRequest("Failed to archive product.");
                    
                    return Ok(new ApiResponse<string>("Product archived successfully.", "Archived"));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>(ex.Message, null));
            }
        }

        /// <summary>
        /// Admin: Manually flag product for violation and send chat notification to provider
        /// </summary>
        [HttpPost("admin/{id}/flag")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> FlagProductViolation(
            Guid id, 
            [FromBody] ManualFlagRequest request)
        {
            try
            {
                var product = await _service.GetByIdAsync(id);
                if (product == null) 
                    return NotFound("Product not found.");

                // Update product status to pending
                product.AvailabilityStatus = "pending";
                var updateResult = await _service.UpdateAsync(product);
                
                if (!updateResult)
                    return BadRequest("Failed to update product status.");

                // Send CHAT notification to provider (instead of email)
                if (product.ProviderId != Guid.Empty)
                {
                    try
                    {
                        // Get current staff ID from claims
                        var staffIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        if (Guid.TryParse(staffIdClaim, out var staffId))
                        {
                            await _conversationService.SendViolationMessageToProviderAsync(
                                staffId: staffId,
                                providerId: product.ProviderId,
                                productId: product.Id,
                                productName: product.Name,
                                reason: request.Reason ?? "Content violates community guidelines (Manual Review)",
                                violatedTerms: string.Join(", ", request.ViolatedTerms ?? new List<string>())
                            );
                            
                            return Ok(new ApiResponse<string>(
                                "Product flagged successfully and chat notification sent to provider.", 
                                "Flagged"));
                        }
                        else
                        {
                            return Ok(new ApiResponse<string>(
                                "Product flagged successfully but failed to get staff ID for notification.", 
                                "Flagged"));
                        }
                    }
                    catch (Exception chatEx)
                    {
                        // Log but don't fail the operation
                        Console.WriteLine($"Failed to send chat notification: {chatEx.Message}");
                        return Ok(new ApiResponse<string>(
                            "Product flagged successfully but chat notification failed.", 
                            "Flagged"));
                    }
                }

                return Ok(new ApiResponse<string>(
                    "Product flagged successfully.", 
                    "Flagged"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>(ex.Message, null));
            }
        }

        /// <summary>
        /// Admin: Approve/Unflag a pending product
        /// </summary>
        [HttpPut("admin/{id}/approve")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> ApproveProduct(
            Guid id, 
            [FromBody] ApproveProductRequest request)
        {
            try
            {
                var product = await _service.GetByIdAsync(id);
                if (product == null) 
                    return NotFound("Product not found.");

                // Update product status
                product.AvailabilityStatus = request.NewStatus ?? "available";
                var updateResult = await _service.UpdateAsync(product);
                
                if (!updateResult)
                    return BadRequest("Failed to update product status.");

                return Ok(new ApiResponse<string>(
                    "Product approved successfully.", 
                    product.AvailabilityStatus));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>(ex.Message, null));
            }
        }

        /// <summary>
        /// Admin/Staff: Manually re-check product content moderation
        /// </summary>
        [HttpPost("admin/{id}/recheck-moderation")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> RecheckModeration(Guid id)
        {
            try
            {
                var product = await _service.GetByIdAsync(id);
                if (product == null) 
                    return NotFound("Product not found.");

                // Run content moderation check
                var moderationResult = await _moderationService.CheckProductContentAsync(
                    product.Name,
                    product.Description
                );

                // Update product status based on result
                if (moderationResult.IsAppropriate)
                {
                    // Passed - set to available if currently pending
                    if (product.AvailabilityStatus.ToLower() == "pending")
                    {
                        product.AvailabilityStatus = "available";
                        await _service.UpdateAsync(product);
                    }
                    
                    return Ok(new ApiResponse<object>(
                        "Product passed moderation check.", 
                        new { 
                            IsAppropriate = true,
                            NewStatus = product.AvailabilityStatus,
                            Message = "Product content is appropriate"
                        }));
                }
                else
                {
                    // Failed - set to pending and send chat notification
                    product.AvailabilityStatus = "pending";
                    await _service.UpdateAsync(product);

                    // Send CHAT notification to provider (instead of email)
                    if (product.ProviderId != Guid.Empty)
                    {
                        try
                        {
                            // Get current staff ID from claims
                            var staffIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                            if (Guid.TryParse(staffIdClaim, out var staffId))
                            {
                                await _conversationService.SendViolationMessageToProviderAsync(
                                    staffId: staffId,
                                    providerId: product.ProviderId,
                                    productId: product.Id,
                                    productName: product.Name,
                                    reason: moderationResult.Reason ?? "Content violates community guidelines",
                                    violatedTerms: string.Join(", ", moderationResult.ViolatedTerms ?? new List<string>())
                                );
                                
                                Console.WriteLine($"[RECHECK] Chat notification sent to Provider {product.ProviderId}");
                            }
                        }
                        catch (Exception chatEx)
                        {
                            Console.WriteLine($"[RECHECK] ERROR sending chat notification: {chatEx.Message}");
                        }
                    }

                    return Ok(new ApiResponse<object>(
                        "Product violated moderation rules and has been flagged.", 
                        new { 
                            IsAppropriate = false,
                            NewStatus = "pending",
                            Reason = moderationResult.Reason,
                            ViolatedTerms = moderationResult.ViolatedTerms
                        }));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Re-check failed: {ex.Message}", null));
            }
        }
    }
}

// DTO for manual flagging
public class ManualFlagRequest
{
    public string? Reason { get; set; }
    public List<string>? ViolatedTerms { get; set; }
}

// DTO for approve product
public class ApproveProductRequest
{
    public string? NewStatus { get; set; }
}

// DTO for re-check moderation
public class RecheckModerationRequest
{
    public Guid ProductId { get; set; }
}
