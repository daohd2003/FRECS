// Category Management JavaScript
document.addEventListener('DOMContentLoaded', function() {
    // Configuration
    const config = window.categoryManagementConfig || {};
    
    // DOM Elements
    const addCategoryBtn = document.getElementById('addCategoryBtn');
    const categoryModal = document.getElementById('categoryModal');
    const categoryForm = document.getElementById('categoryForm');
    const closeCategoryModal = document.getElementById('closeCategoryModal');
    const cancelCategory = document.getElementById('cancelCategory');
    const saveCategory = document.getElementById('saveCategory');
    const confirmModal = document.getElementById('confirmModal');
    const cancelConfirm = document.getElementById('cancelConfirm');
    const confirmDelete = document.getElementById('confirmDelete');
    
    // Form Elements
    const categoryIdInput = document.getElementById('categoryId');
    const modalModeInput = document.getElementById('modalMode');
    const categoryNameInput = document.getElementById('categoryName');
    const categoryDescriptionInput = document.getElementById('categoryDescription');
    const categoryImageInput = document.getElementById('categoryImage');
    const imagePreview = document.getElementById('imagePreview');
    const statusToggle = document.getElementById('statusToggle');
    const statusDescription = document.getElementById('statusDescription');
    const saveButtonText = document.getElementById('saveButtonText');
    const modalTitle = document.getElementById('modalTitle');
    const modalSubtitle = document.getElementById('modalSubtitle');
    
    // Character counters
    const nameCharCount = document.getElementById('nameCharCount');
    const descCharCount = document.getElementById('descCharCount');
    
    // Search and filter elements
    const searchInput = document.getElementById('searchInput');
    const statusFilter = document.getElementById('statusFilter');
    const sortFilter = document.getElementById('sortFilter');
    
    // State
    let currentCategoryId = null;
    let isActive = true;
    let allCategories = [];
    let filteredCategories = [];
    let currentPage = 1;
    let itemsPerPage = 10;
    
    // Initialize
    init();
    
    function init() {
        setupEventListeners();
        setupCharacterCounters();
        setupImageUpload();
        setupSearchAndFilter();
        loadAllCategories();
        setupImageZoom();
    }
    
    function setupEventListeners() {
        // Modal controls
        addCategoryBtn?.addEventListener('click', openAddModal);
        closeCategoryModal?.addEventListener('click', closeModal);
        cancelCategory?.addEventListener('click', closeModal);
        saveCategory?.addEventListener('click', handleSaveCategory);
        
        // Confirmation modal
        cancelConfirm?.addEventListener('click', closeConfirmModal);
        confirmDelete?.addEventListener('click', handleDeleteCategory);
        
        // Status toggle
        statusToggle?.addEventListener('click', toggleStatus);
        
        // Action buttons (delegated events)
        document.addEventListener('click', handleActionClick);
        
        // Close modals on outside click
        categoryModal?.addEventListener('click', function(e) {
            if (e.target === categoryModal) closeModal();
        });
        
        confirmModal?.addEventListener('click', function(e) {
            if (e.target === confirmModal) closeConfirmModal();
        });
    }
    
    function setupCharacterCounters() {
        categoryNameInput?.addEventListener('input', function() {
            if (nameCharCount) {
                nameCharCount.textContent = this.value.length;
            }
        });
        
        categoryDescriptionInput?.addEventListener('input', function() {
            if (descCharCount) {
                descCharCount.textContent = this.value.length;
            }
        });
    }
    
    function setupImageUpload() {
        categoryImageInput?.addEventListener('change', handleImageSelect);
    }
    
    function setupSearchAndFilter() {
        searchInput?.addEventListener('input', filterCategories);
        statusFilter?.addEventListener('change', filterCategories);
        sortFilter?.addEventListener('change', filterCategories);
    }
    
    function handleActionClick(e) {
        // Check for toggle switch
        const toggleSwitch = e.target.closest('.toggle-switch');
        if (toggleSwitch && e.target.classList.contains('toggle-input')) {
            handleToggleStatus(toggleSwitch, e.target);
            return;
        }
        
        // Check for action buttons
        const target = e.target.closest('.action-btn');
        if (!target) return;
        
        const categoryId = target.dataset.categoryId;
        if (!categoryId) return;
        
        if (target.classList.contains('edit-btn')) {
            openEditModal(categoryId);
        } else if (target.classList.contains('delete-btn')) {
            openDeleteModal(categoryId);
        } else if (target.classList.contains('view-btn')) {
            viewCategory(categoryId);
        }
    }
    
    function openAddModal() {
        resetForm();
        modalModeInput.value = 'add';
        modalTitle.textContent = 'Add New Category';
        modalSubtitle.textContent = 'Create a new product category for your rental platform';
        saveButtonText.textContent = 'Create Category';
        categoryModal.style.display = 'flex';
    }
    
    function openEditModal(categoryId) {
        currentCategoryId = categoryId;
        modalModeInput.value = 'edit';
        modalTitle.textContent = 'Edit Category';
        modalSubtitle.textContent = 'Update category information and settings';
        saveButtonText.textContent = 'Update Category';
        
        // Load category data
        loadCategoryData(categoryId);
        categoryModal.style.display = 'flex';
    }
    
    async function loadCategoryData(categoryId) {
        try {
            showLoading(true);
            
            const response = await fetch(`/Staff/CategoryManagement?handler=Category&categoryId=${categoryId}`, {
                method: 'GET',
                headers: {
                    'RequestVerificationToken': getAntiForgeryToken()
                }
            });
            
            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    const category = result.data;
                    populateForm(category);
                } else {
                    showToast('error', result.message);
                }
            } else {
                showToast('error', 'Failed to load category data');
            }
        } catch (error) {
            console.error('Error loading category:', error);
            showToast('error', 'Error loading category data');
        } finally {
            showLoading(false);
        }
    }
    
    function populateForm(category) {
        categoryIdInput.value = category.id;
        categoryNameInput.value = category.name;
        categoryDescriptionInput.value = category.description || '';
        isActive = category.isActive;
        
        // Store original values for comparison
        window.originalCategoryData = {
            name: category.name,
            description: category.description || '',
            isActive: category.isActive,
            imageUrl: category.imageUrl || ''
        };
        
        // Update character counters
        if (nameCharCount) nameCharCount.textContent = category.name.length;
        if (descCharCount) descCharCount.textContent = (category.description || '').length;
        
        // Update status
        updateStatusDisplay();
        
        // Update image preview if exists
        if (category.imageUrl) {
            showImagePreview(category.imageUrl);
        }
    }
    
    function openDeleteModal(categoryId) {
        currentCategoryId = categoryId;
        confirmModal.style.display = 'flex';
    }
    
    function closeModal() {
        categoryModal.style.display = 'none';
        resetForm();
    }
    
    function closeConfirmModal() {
        confirmModal.style.display = 'none';
        currentCategoryId = null;
    }
    
    function resetForm() {
        categoryForm.reset();
        categoryIdInput.value = '';
        modalModeInput.value = 'add';
        isActive = true;
        
        // Reset character counters
        if (nameCharCount) nameCharCount.textContent = '0';
        if (descCharCount) descCharCount.textContent = '0';
        
        // Reset image preview
        resetImagePreview();
        
        // Reset status
        updateStatusDisplay();
    }
    
    function handleImageSelect(e) {
        const file = e.target.files[0];
        if (file) {
            // Validate file
            if (!validateImageFile(file)) {
                return;
            }
            
            // Show preview
            const reader = new FileReader();
            reader.onload = function(e) {
                showImagePreview(e.target.result);
            };
            reader.readAsDataURL(file);
        }
    }
    
    function validateImageFile(file) {
        const allowedTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/gif', 'image/webp'];
        const maxSize = 5 * 1024 * 1024; // 5MB
        
        if (!allowedTypes.includes(file.type)) {
            showToast('error', 'Please select a valid image file (JPG, PNG, GIF, WEBP)');
            return false;
        }
        
        if (file.size > maxSize) {
            showToast('error', 'File size must be less than 5MB');
            return false;
        }
        
        return true;
    }
    
    function showImagePreview(src) {
        imagePreview.innerHTML = `
            <img src="${src}" alt="Category Image" style="width: 100%; height: 100%; object-fit: cover; border-radius: 8px;">
        `;
        imagePreview.classList.add('has-image');
    }
    
    function resetImagePreview() {
        imagePreview.innerHTML = `
            <svg class="preview-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <rect x="3" y="3" width="18" height="18" rx="2" ry="2"></rect>
                <circle cx="8.5" cy="8.5" r="1.5"></circle>
                <polyline points="21,15 16,10 5,21"></polyline>
            </svg>
            <p class="preview-text">No image selected</p>
        `;
        imagePreview.classList.remove('has-image');
    }
    
    function toggleStatus() {
        isActive = !isActive;
        updateStatusDisplay();
    }
    
    function updateStatusDisplay() {
        if (statusToggle && statusDescription) {
            if (isActive) {
                statusToggle.classList.remove('inactive');
                statusToggle.classList.add('active');
                statusToggle.innerHTML = `
                    <svg class="toggle-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="20,6 9,17 4,12"></polyline>
                    </svg>
                    <span>Active</span>
                `;
                statusDescription.textContent = 'Category is active and visible to customers';
            } else {
                statusToggle.classList.remove('active');
                statusToggle.classList.add('inactive');
                statusToggle.innerHTML = `
                    <svg class="toggle-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <line x1="18" y1="6" x2="6" y2="18"></line>
                        <line x1="6" y1="6" x2="18" y2="18"></line>
                    </svg>
                    <span>Inactive</span>
                `;
                statusDescription.textContent = 'Category is inactive and hidden from customers';
            }
        }
    }
    
    async function handleSaveCategory(e) {
        e.preventDefault();
        
        if (!validateForm()) {
            return;
        }
        
        const isEdit = modalModeInput.value === 'edit';
        
        // Check if anything changed in edit mode
        if (isEdit && window.originalCategoryData) {
            const currentName = categoryNameInput.value;
            const currentDesc = categoryDescriptionInput.value;
            const currentActive = isActive;
            const hasNewImage = categoryImageInput.files.length > 0;
            
            const hasChanges = 
                currentName !== window.originalCategoryData.name ||
                currentDesc !== window.originalCategoryData.description ||
                currentActive !== window.originalCategoryData.isActive ||
                hasNewImage;
            
            if (!hasChanges) {
                showToast('info', 'No changes detected');
                closeModal();
                return;
            }
        }
        
        try {
            showLoading(true);
            
            const formData = new FormData();
            formData.append('categoryName', categoryNameInput.value);
            formData.append('categoryDescription', categoryDescriptionInput.value);
            formData.append('isActive', isActive.toString());
            
            // Add image file if selected (directly send file, backend will handle upload)
            const imageFile = categoryImageInput.files[0];
            if (imageFile) {
                formData.append('imageFile', imageFile);
            }
            
            const url = isEdit 
                ? '/Staff/CategoryManagement?handler=UpdateCategory'
                : '/Staff/CategoryManagement?handler=CreateCategory';
            
            if (isEdit) {
                formData.append('categoryId', categoryIdInput.value);
            }
            
            const response = await fetch(url, {
                method: 'POST',
                body: formData,
                headers: {
                    'RequestVerificationToken': getAntiForgeryToken()
                }
            });
            
            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    showToast('success', result.message || (isEdit ? 'Category updated successfully' : 'Category created successfully'));
                    closeModal();
                    // Reload page to show updated data
                    setTimeout(() => {
                        window.location.reload();
                    }, 1500);
                } else {
                    showToast('error', result.message || 'Failed to save category');
                }
            } else {
                const errorText = await response.text();
                showToast('error', `Failed to save category: ${response.status}`);
            }
        } catch (error) {
            console.error('Error saving category:', error);
            showToast('error', 'Error saving category: ' + error.message);
        } finally {
            showLoading(false);
        }
    }
    
    function validateForm() {
        if (!categoryNameInput.value.trim()) {
            showToast('error', 'Category name is required');
            return false;
        }
        
        if (categoryNameInput.value.length > 150) {
            showToast('error', 'Category name must be 150 characters or less');
            return false;
        }
        
        if (categoryDescriptionInput.value.length > 255) {
            showToast('error', 'Description must be 255 characters or less');
            return false;
        }
        
        return true;
    }
    
    async function handleDeleteCategory() {
        if (!currentCategoryId) return;
        
        try {
            showLoading(true);
            
            const response = await fetch(`/Staff/CategoryManagement?handler=DeleteCategory&categoryId=${currentCategoryId}`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': getAntiForgeryToken()
                }
            });
            
            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    showToast('success', result.message || 'Category deleted successfully');
                    closeConfirmModal();
                    // Reload page to show updated data
                    setTimeout(() => {
                        window.location.reload();
                    }, 1500);
                } else {
                    showToast('error', result.message || 'Failed to delete category');
                    closeConfirmModal();
                }
            } else {
                const errorText = await response.text();
                showToast('error', `Failed to delete category: ${response.status}`);
                closeConfirmModal();
            }
        } catch (error) {
            console.error('Error deleting category:', error);
            showToast('error', 'Error deleting category: ' + error.message);
            closeConfirmModal();
        } finally {
            showLoading(false);
        }
    }
    
    async function viewCategory(categoryId) {
        try {
            console.log('=== View Category Debug ===');
            console.log('Requested categoryId:', categoryId, '(type:', typeof categoryId, ')');
            console.log('Available categories:', allCategories.map(c => ({ id: c.id, name: c.name })));
            
            // Case-insensitive comparison for Guid strings
            const categoryIdStr = String(categoryId).toLowerCase();
            const category = allCategories.find(c => String(c.id).toLowerCase() === categoryIdStr);
            
            if (!category) {
                console.error('Category not found for id:', categoryId);
                showToast('error', 'Category not found');
                return;
            }
            
            console.log('Found category:', category.name, 'with id:', category.id);
            
            // Show modal and load products
            showProductsModal(category);
        } catch (error) {
            console.error('Error loading category:', error);
            showToast('error', 'Error loading category products');
        }
    }
    
    // Products Modal Management
    let currentCategoryForProducts = null;
    let allCategoryProducts = [];
    let filteredCategoryProducts = [];
    let currentProductPage = 1;
    const productsPerPage = 10;
    let productSearchTerm = '';
    let productSortBy = 'name-asc';
    let productTypeFilter = 'all';
    
    function showProductsModal(category) {
        currentCategoryForProducts = category;
        const modal = document.getElementById('productsModal');
        const modalTitle = document.getElementById('productsModalTitle');
        const modalSubtitle = document.getElementById('productsModalSubtitle');
        
        if (!modal) return;
        
        // IMPORTANT: Reset all data first to prevent showing old data
        allCategoryProducts = [];
        filteredCategoryProducts = [];
        currentProductPage = 1;
        productSearchTerm = '';
        productSortBy = 'name-asc';
        productTypeFilter = 'all';
        
        // Clear the table body immediately
        const tbody = document.getElementById('productsTableBody');
        if (tbody) tbody.innerHTML = '';
        
        // Reset statistics display
        const totalProductsElem = document.getElementById('modalTotalProducts');
        const showingProductsElem = document.getElementById('modalShowingProducts');
        
        if (totalProductsElem) totalProductsElem.textContent = '0';
        if (showingProductsElem) showingProductsElem.textContent = '0';
        
        // Reset pagination display
        const paginationStart = document.getElementById('paginationStart');
        const paginationEnd = document.getElementById('paginationEnd');
        const paginationTotal = document.getElementById('paginationTotal');
        const paginationContainer = document.getElementById('productsPagination');
        
        if (paginationStart) paginationStart.textContent = '0';
        if (paginationEnd) paginationEnd.textContent = '0';
        if (paginationTotal) paginationTotal.textContent = '0';
        if (paginationContainer) paginationContainer.style.display = 'none';
        
        // Reset search and filters UI
        const searchInput = document.getElementById('productSearchInput');
        const sortSelect = document.getElementById('productSortFilter');
        const typeSelect = document.getElementById('productTypeFilter');
        
        if (searchInput) searchInput.value = '';
        if (sortSelect) sortSelect.value = 'name-asc';
        if (typeSelect) typeSelect.value = 'all';
        
        // Update modal title
        modalTitle.textContent = `Products in "${category.name}"`;
        modalSubtitle.textContent = 'Loading products...';
        
        // Show modal
        modal.style.display = 'flex';
        
        // Show loading state immediately
        showProductsLoading(true);
        
        // Setup event listeners
        setupProductsModalEvents();
        
        // Load products from API
        loadProductsByCategory(category.id);
    }
    
    function setupProductsModalEvents() {
        // Close modal event
        const closeBtn = document.getElementById('closeProductsModal');
        if (closeBtn) {
            closeBtn.onclick = closeProductsModal;
        }
        
        // Search event
        const searchInput = document.getElementById('productSearchInput');
        if (searchInput) {
            searchInput.oninput = (e) => {
                productSearchTerm = e.target.value.toLowerCase();
                currentProductPage = 1;
                filterAndRenderProducts();
            };
        }
        
        // Sort event
        const sortSelect = document.getElementById('productSortFilter');
        if (sortSelect) {
            sortSelect.onchange = (e) => {
                productSortBy = e.target.value;
                currentProductPage = 1;
                filterAndRenderProducts();
            };
        }
        
        // Type filter event
        const typeSelect = document.getElementById('productTypeFilter');
        if (typeSelect) {
            typeSelect.onchange = (e) => {
                productTypeFilter = e.target.value;
                currentProductPage = 1;
                filterAndRenderProducts();
            };
        }
        
        // Pagination events
        const prevBtn = document.getElementById('productsPrevBtn');
        const nextBtn = document.getElementById('productsNextBtn');
        
        if (prevBtn) {
            prevBtn.onclick = () => {
                if (currentProductPage > 1) {
                    currentProductPage--;
                    filterAndRenderProducts();
                }
            };
        }
        
        if (nextBtn) {
            nextBtn.onclick = () => {
                const totalPages = Math.ceil(filteredCategoryProducts.length / productsPerPage);
                if (currentProductPage < totalPages) {
                    currentProductPage++;
                    filterAndRenderProducts();
                }
            };
        }
        
        // Close on background click
        const modal = document.getElementById('productsModal');
        if (modal) {
            modal.onclick = (e) => {
                if (e.target === modal) {
                    closeProductsModal();
                }
            };
        }
    }
    
    async function loadProductsByCategory(categoryId) {
        try {
            showProductsLoading(true);
            
            // Remove /api prefix if it exists in apiBaseUrl since endpoint already has it
            const baseUrl = config.apiBaseUrl.replace(/\/api\/?$/, '');
            const response = await fetch(`${baseUrl}/api/products`, {
                headers: {
                    'Authorization': `Bearer ${config.accessToken}`,
                    'Content-Type': 'application/json'
                }
            });
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: Failed to load products`);
            }
            
            const result = await response.json();
            
            // Handle both direct array and wrapped response
            const products = Array.isArray(result) ? result : (result.data || []);
            
            console.log(`Loading products for categoryId: "${categoryId}"`);
            console.log(`Total products loaded: ${products.length}`);
            
            // Debug: Log sample product categoryIds
            if (products.length > 0) {
                console.log('Sample product categoryIds:', products.slice(0, 3).map(p => ({
                    name: p.name,
                    categoryId: p.categoryId,
                    type: typeof p.categoryId
                })));
            }
            
            // Filter products by categoryId (case-insensitive string comparison)
            const categoryIdStr = String(categoryId).toLowerCase();
            allCategoryProducts = products.filter(p => {
                const productCategoryId = String(p.categoryId || '').toLowerCase();
                return productCategoryId === categoryIdStr;
            });
            
            console.log(`Filtered ${allCategoryProducts.length} products for category "${categoryId}"`);
            
            // Debug: Log first product structure
            if (allCategoryProducts.length > 0) {
                console.log('Sample filtered product:', allCategoryProducts[0]);
                console.log('Product types in category:', [...new Set(allCategoryProducts.map(p => p.productType))]);
            }
            
            // Update subtitle
            const modalSubtitle = document.getElementById('productsModalSubtitle');
            if (modalSubtitle) {
                modalSubtitle.textContent = `${allCategoryProducts.length} product(s) in this category`;
            }
            
            // Render products (filters already reset in showProductsModal)
            if (allCategoryProducts.length > 0) {
                filterAndRenderProducts();
            } else {
                showProductsEmpty(true, 'No products found in this category.');
            }
            
        } catch (error) {
            console.error('Error loading products:', error);
            showToast('error', `Failed to load products: ${error.message}`);
            showProductsEmpty(true, 'Failed to load products. Please try again.');
        } finally {
            showProductsLoading(false);
        }
    }
    
    function filterAndRenderProducts() {
        // Filter by search term
        filteredCategoryProducts = allCategoryProducts.filter(product => {
            const matchesSearch = !productSearchTerm || 
                product.name.toLowerCase().includes(productSearchTerm) ||
                (product.description && product.description.toLowerCase().includes(productSearchTerm));
            
            // Filter by type (case-insensitive comparison)
            const productType = (product.productType || '').toUpperCase();
            const filterType = productTypeFilter.toUpperCase();
            const matchesType = filterType === 'ALL' || productType === filterType;
            
            return matchesSearch && matchesType;
        });
        
        // Sort
        const [sortField, sortOrder] = productSortBy.split('-');
        filteredCategoryProducts.sort((a, b) => {
            let aValue, bValue;
            
            switch(sortField) {
                case 'name':
                    aValue = (a.name || '').toLowerCase();
                    bValue = (b.name || '').toLowerCase();
                    break;
                case 'price':
                    // Use pricePerDay for rental/both, purchasePrice for purchase
                    aValue = a.pricePerDay || a.purchasePrice || 0;
                    bValue = b.pricePerDay || b.purchasePrice || 0;
                    break;
                case 'rating':
                    aValue = a.averageRating || 0;
                    bValue = b.averageRating || 0;
                    break;
                case 'newest':
                    aValue = new Date(a.createdAt || 0);
                    bValue = new Date(b.createdAt || 0);
                    break;
                default:
                    aValue = (a.name || '').toLowerCase();
                    bValue = (b.name || '').toLowerCase();
            }
            
            if (sortOrder === 'asc') {
                return aValue > bValue ? 1 : -1;
            } else {
                return aValue < bValue ? 1 : -1;
            }
        });
        
        // Update stats
        const totalElem = document.getElementById('modalTotalProducts');
        const showingElem = document.getElementById('modalShowingProducts');
        
        if (totalElem) totalElem.textContent = allCategoryProducts.length;
        if (showingElem) showingElem.textContent = filteredCategoryProducts.length;
        
        // Render table and pagination
        renderProductsTable();
        renderProductsPagination();
    }
    
    function renderProductsTable() {
        const tbody = document.getElementById('productsTableBody');
        const emptyState = document.getElementById('productsEmptyState');
        
        if (!tbody) {
            console.warn('Products table body not found');
            return;
        }
        
        console.log(`Rendering ${filteredCategoryProducts.length} filtered products`);
        
        if (filteredCategoryProducts.length === 0) {
            tbody.innerHTML = '';
            showProductsEmpty(true, 'No products match your search criteria.');
            return;
        }
        
        showProductsEmpty(false);
        
        // Paginate
        const startIndex = (currentProductPage - 1) * productsPerPage;
        const endIndex = startIndex + productsPerPage;
        const pageProducts = filteredCategoryProducts.slice(startIndex, endIndex);
        
        console.log(`Displaying products ${startIndex + 1} to ${Math.min(endIndex, filteredCategoryProducts.length)} (page ${currentProductPage})`);
        
        tbody.innerHTML = pageProducts.map(product => `
            <tr>
                <td>
                    <div class="product-image-cell">
                        ${product.primaryImagesUrl
                            ? `<img src="${product.primaryImagesUrl}" alt="${product.name}" />`
                            : `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                    <rect x="3" y="3" width="18" height="18" rx="2" ry="2"></rect>
                                    <circle cx="8.5" cy="8.5" r="1.5"></circle>
                                    <polyline points="21,15 16,10 5,21"></polyline>
                                </svg>`
                    }
                </div>
                </td>
                <td>
                    <div class="product-name-cell">
                        <span class="product-name-text">${product.name}</span>
                        <span class="product-id-text">ID: ${product.id.substring(0, 8)}</span>
                    </div>
                </td>
                <td>
                    <span class="provider-name">${product.providerName || 'N/A'}</span>
                </td>
                <td>
                    <span class="product-type-badge ${product.productType ? product.productType.toLowerCase() : 'unavailable'}">
                        ${product.productType || 'N/A'}
                    </span>
                </td>
                <td>
                    <div class="product-price-cell">
                        ${product.productType === 'RENTAL' || product.productType === 'BOTH'
                            ? `₫${(product.pricePerDay || 0).toLocaleString()}/day`
                            : product.productType === 'PURCHASE'
                                ? `₫${(product.purchasePrice || 0).toLocaleString()}`
                                : 'N/A'
                        }
                        ${product.productType === 'BOTH'
                            ? `<span class="product-price-secondary">Buy: ₫${(product.purchasePrice || 0).toLocaleString()}</span>`
                            : ''
                        }
                </div>
                </td>
                <td>
                    <div class="product-stock-cell">
                        ${product.rentalQuantity > 0
                            ? `<div class="stock-item stock-rental">
                                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                        <path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
                                        <circle cx="8.5" cy="7" r="4"></circle>
                                        <line x1="20" y1="8" x2="20" y2="14"></line>
                                        <line x1="23" y1="11" x2="17" y2="11"></line>
                                    </svg>
                                    Rental: ${product.rentalQuantity}
                                </div>`
                            : ''
                        }
                        ${product.purchaseQuantity > 0
                            ? `<div class="stock-item stock-purchase">
                                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                        <circle cx="9" cy="21" r="1"></circle>
                                        <circle cx="20" cy="21" r="1"></circle>
                                        <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"></path>
                                    </svg>
                                    Purchase: ${product.purchaseQuantity}
                                </div>`
                            : ''
                        }
                        ${!product.rentalQuantity && !product.purchaseQuantity ? '<span class="text-muted">Out of stock</span>' : ''}
            </div>
                </td>
                <td>
                    <div class="product-rating">
                        <span class="rating-stars">★</span>
                        <span class="rating-value">${(product.averageRating || 0).toFixed(1)}</span>
                    </div>
                </td>
                <td>
                    <div class="product-stats-cell">
                        ${product.rentCount > 0
                            ? `<div class="stats-row">
                                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                        <path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
                                        <circle cx="8.5" cy="7" r="4"></circle>
                                    </svg>
                                    ${product.rentCount} rents
                                </div>`
                            : ''
                        }
                        ${product.buyCount > 0
                            ? `<div class="stats-row">
                                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                        <circle cx="9" cy="21" r="1"></circle>
                                        <circle cx="20" cy="21" r="1"></circle>
                                        <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"></path>
                                    </svg>
                                    ${product.buyCount} sales
                                </div>`
                            : ''
                        }
                        ${!product.rentCount && !product.buyCount ? '<span class="text-muted">No activity</span>' : ''}
                    </div>
                </td>
                <td>
                    <span class="product-status-cell ${product.availabilityStatus ? product.availabilityStatus.toLowerCase() : 'unavailable'}">
                        ${product.availabilityStatus || 'N/A'}
                    </span>
                </td>
            </tr>
        `).join('');
    }
    
    function renderProductsPagination() {
        const paginationContainer = document.getElementById('productsPagination');
        
        if (!paginationContainer) {
            console.warn('Pagination container not found');
            return;
        }
        
        const totalPages = Math.ceil(filteredCategoryProducts.length / productsPerPage);
        
        console.log(`Pagination: ${filteredCategoryProducts.length} products, ${totalPages} pages, current page: ${currentProductPage}`);
        
        // Always show pagination info, hide controls if only 1 page
        const paginationStart = document.getElementById('paginationStart');
        const paginationEnd = document.getElementById('paginationEnd');
        const paginationTotal = document.getElementById('paginationTotal');
        
        const startIndex = filteredCategoryProducts.length > 0 ? (currentProductPage - 1) * productsPerPage + 1 : 0;
        const endIndex = Math.min(currentProductPage * productsPerPage, filteredCategoryProducts.length);
        
        if (paginationStart) paginationStart.textContent = startIndex;
        if (paginationEnd) paginationEnd.textContent = endIndex;
        if (paginationTotal) paginationTotal.textContent = filteredCategoryProducts.length;
        
        // Show pagination container
        paginationContainer.style.display = 'flex';
        
        // Get pagination controls
        const paginationNumbers = document.getElementById('paginationNumbers');
        const prevBtn = document.getElementById('productsPrevBtn');
        const nextBtn = document.getElementById('productsNextBtn');
        
        if (totalPages <= 1) {
            // Hide pagination controls but keep info visible
            if (paginationNumbers) paginationNumbers.innerHTML = '';
            if (prevBtn) prevBtn.style.display = 'none';
            if (nextBtn) nextBtn.style.display = 'none';
            return;
        }
        
        // Show and update buttons
        if (prevBtn) {
            prevBtn.style.display = 'flex';
            prevBtn.disabled = currentProductPage === 1;
        }
        
        if (nextBtn) {
            nextBtn.style.display = 'flex';
            nextBtn.disabled = currentProductPage === totalPages;
        }
        
        // Render page numbers
        if (paginationNumbers) {
            let html = '';
        
        for (let i = 1; i <= totalPages; i++) {
                if (
                    i === 1 || 
                    i === totalPages || 
                    (i >= currentProductPage - 1 && i <= currentProductPage + 1)
                ) {
            html += `
                        <button class="page-number-btn ${i === currentProductPage ? 'active' : ''}" 
                                onclick="window.goToProductPage(${i})">
                    ${i}
                </button>
            `;
                } else if (i === currentProductPage - 2 || i === currentProductPage + 2) {
                    html += '<span class="page-dots">...</span>';
                }
            }
            
            paginationNumbers.innerHTML = html;
        }
    }
    
    function showProductsLoading(show) {
        const loadingState = document.getElementById('productsLoadingState');
        const tableContainer = document.querySelector('.products-table-container table');
        
        if (loadingState) {
            loadingState.style.display = show ? 'block' : 'none';
        }
        
        if (tableContainer) {
            tableContainer.style.display = show ? 'none' : 'table';
        }
    }
    
    function showProductsEmpty(show, message = null) {
        const emptyState = document.getElementById('productsEmptyState');
        const tableContainer = document.querySelector('.products-table-container table');
        const paginationContainer = document.getElementById('productsPagination');
        
        if (emptyState) {
            emptyState.style.display = show ? 'block' : 'none';
            if (show && message) {
                const emptyP = emptyState.querySelector('p');
                if (emptyP) emptyP.textContent = message;
            }
        }
        
        if (tableContainer) {
            tableContainer.style.display = show ? 'none' : 'table';
        }
        
        if (paginationContainer) {
            paginationContainer.style.display = show ? 'none' : 'flex';
        }
    }
    
    function closeProductsModal() {
        const modal = document.getElementById('productsModal');
        if (modal) {
            modal.style.display = 'none';
            
            // Reset data
            currentCategoryForProducts = null;
            allCategoryProducts = [];
            filteredCategoryProducts = [];
            currentProductPage = 1;
            productSearchTerm = '';
            productSortBy = 'name-asc';
            productTypeFilter = 'all';
            
            // Reset inputs
            const searchInput = document.getElementById('productSearchInput');
            const sortSelect = document.getElementById('productSortFilter');
            const typeSelect = document.getElementById('productTypeFilter');
            
            if (searchInput) searchInput.value = '';
            if (sortSelect) sortSelect.value = 'name-asc';
            if (typeSelect) typeSelect.value = 'all';
        }
    }
    
    // Global functions for pagination
    window.goToProductPage = function(page) {
        const totalPages = Math.ceil(filteredCategoryProducts.length / productsPerPage);
        if (page < 1 || page > totalPages) return;
        
        currentProductPage = page;
        filterAndRenderProducts();
    };
    
    async function handleToggleStatus(toggleSwitch, checkbox) {
        const categoryId = toggleSwitch.dataset.categoryId;
        const currentStatus = toggleSwitch.dataset.isActive === 'true';
        const newStatus = !currentStatus;
        
        if (!categoryId) return;
        
        try {
            // Add loading state
            toggleSwitch.classList.add('loading');
            
            const formData = new FormData();
            formData.append('categoryId', categoryId);
            formData.append('isActive', newStatus.toString());
            
            const response = await fetch('/Staff/CategoryManagement?handler=ToggleStatus', {
                method: 'POST',
                body: formData,
                headers: {
                    'RequestVerificationToken': getAntiForgeryToken()
                }
            });
            
            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    // Update data attribute
                    toggleSwitch.dataset.isActive = newStatus.toString();
                    
                    // Update label
                    const statusLabel = toggleSwitch.parentElement.querySelector('.status-label');
                    if (statusLabel) {
                        statusLabel.textContent = newStatus ? 'Active' : 'Inactive';
                        statusLabel.classList.toggle('active', newStatus);
                        statusLabel.classList.toggle('inactive', !newStatus);
                    }
                    
                    // Update row data attribute
                    const row = toggleSwitch.closest('tr');
                    if (row) {
                        row.dataset.isActive = newStatus.toString();
                    }
                    
                    showToast('success', `Category ${newStatus ? 'activated' : 'deactivated'} successfully`);
                    
                    // Reload after a short delay to update statistics
                    setTimeout(() => {
                        window.location.reload();
                    }, 1500);
                } else {
                    // Revert checkbox on error
                    checkbox.checked = currentStatus;
                    showToast('error', result.message || 'Failed to update status');
                }
            } else {
                // Revert checkbox on error
                checkbox.checked = currentStatus;
                showToast('error', 'Failed to update category status');
            }
        } catch (error) {
            console.error('Error toggling status:', error);
            // Revert checkbox on error
            checkbox.checked = currentStatus;
            showToast('error', 'Error updating status');
        } finally {
            toggleSwitch.classList.remove('loading');
        }
    }
    
    function loadAllCategories() {
        const rows = document.querySelectorAll('#categoriesTableBody tr');
        allCategories = Array.from(rows).map(row => {
            return {
                element: row,
                id: row.dataset.categoryId,
                name: row.querySelector('.category-name')?.textContent.toLowerCase() || '',
                description: row.querySelector('.category-description')?.textContent.toLowerCase() || '',
                isActive: row.dataset.isActive === 'true',
                createdAt: row.querySelector('.date-info span')?.textContent || ''
            };
        });
        
        filteredCategories = [...allCategories];
        renderCategories();
    }
    
    function filterCategories() {
        const searchTerm = searchInput?.value.toLowerCase() || '';
        const statusFilterValue = statusFilter?.value || 'all';
        const sortValue = sortFilter?.value || '';
        
        // Filter
        filteredCategories = allCategories.filter(cat => {
            const matchesSearch = !searchTerm || 
                cat.name.includes(searchTerm) || 
                cat.description.includes(searchTerm);
            
            const matchesStatus = statusFilterValue === 'all' ||
                (statusFilterValue === 'active' && cat.isActive) ||
                (statusFilterValue === 'inactive' && !cat.isActive);
            
            return matchesSearch && matchesStatus;
        });
        
        // Sort
        if (sortValue) {
            const [sortBy, sortOrder] = sortValue.split('-');
            filteredCategories.sort((a, b) => {
                let aValue, bValue;
                
                switch (sortBy) {
                    case 'name':
                        aValue = a.name;
                        bValue = b.name;
                        break;
                    case 'createdAt':
                    case 'updatedAt':
                        aValue = a.createdAt;
                        bValue = b.createdAt;
                        break;
                    default:
                        return 0;
                }
                
                if (sortOrder === 'asc') {
                    return aValue > bValue ? 1 : -1;
                } else {
                    return aValue < bValue ? 1 : -1;
                }
            });
        }
        
        currentPage = 1;
        renderCategories();
    }
    
    function renderCategories() {
        const tbody = document.querySelector('#categoriesTableBody');
        const emptyState = document.getElementById('emptyState');
        
        if (!tbody) return;
        
        // Hide all rows first
        allCategories.forEach(cat => {
            cat.element.style.display = 'none';
        });
        
        // Calculate pagination
        const totalPages = Math.ceil(filteredCategories.length / itemsPerPage);
        const startIndex = (currentPage - 1) * itemsPerPage;
        const endIndex = startIndex + itemsPerPage;
        const pageCategories = filteredCategories.slice(startIndex, endIndex);
        
        // Show current page categories
        pageCategories.forEach(cat => {
            cat.element.style.display = '';
            tbody.appendChild(cat.element);
        });
        
        // Show/hide empty state
        if (filteredCategories.length === 0) {
            emptyState.style.display = 'block';
        } else {
            emptyState.style.display = 'none';
        }
        
        // Render pagination
        renderPagination(totalPages);
    }
    
    function renderPagination(totalPages) {
        let paginationContainer = document.querySelector('.pagination-container');
        
        if (!paginationContainer) {
            paginationContainer = document.createElement('div');
            paginationContainer.className = 'pagination-container';
            const tableContainer = document.querySelector('.table-container');
            if (tableContainer) {
                tableContainer.appendChild(paginationContainer);
            }
        }
        
        if (totalPages <= 1) {
            paginationContainer.innerHTML = '';
            return;
        }
        
        let paginationHTML = '<div class="pagination">';
        
        // Previous button
        paginationHTML += `
            <button class="pagination-btn ${currentPage === 1 ? 'disabled' : ''}" 
                    onclick="changePage(${currentPage - 1})" 
                    ${currentPage === 1 ? 'disabled' : ''}>
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <polyline points="15,18 9,12 15,6"></polyline>
                </svg>
            </button>
        `;
        
        // Page numbers
        for (let i = 1; i <= totalPages; i++) {
            if (i === 1 || i === totalPages || (i >= currentPage - 2 && i <= currentPage + 2)) {
                paginationHTML += `
                    <button class="pagination-btn ${i === currentPage ? 'active' : ''}" 
                            onclick="changePage(${i})">
                        ${i}
                    </button>
                `;
            } else if (i === currentPage - 3 || i === currentPage + 3) {
                paginationHTML += '<span class="pagination-dots">...</span>';
            }
        }
        
        // Next button
        paginationHTML += `
            <button class="pagination-btn ${currentPage === totalPages ? 'disabled' : ''}" 
                    onclick="changePage(${currentPage + 1})" 
                    ${currentPage === totalPages ? 'disabled' : ''}>
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <polyline points="9,18 15,12 9,6"></polyline>
                </svg>
            </button>
        `;
        
        paginationHTML += `
            <div class="pagination-info">
                Showing ${((currentPage - 1) * itemsPerPage) + 1}-${Math.min(currentPage * itemsPerPage, filteredCategories.length)} 
                of ${filteredCategories.length}
            </div>
        `;
        
        paginationHTML += '</div>';
        paginationContainer.innerHTML = paginationHTML;
    }
    
    window.changePage = function(page) {
        const totalPages = Math.ceil(filteredCategories.length / itemsPerPage);
        if (page < 1 || page > totalPages) return;
        currentPage = page;
        renderCategories();
        window.scrollTo({ top: 0, behavior: 'smooth' });
    };
    
    function showLoading(show) {
        const buttons = document.querySelectorAll('.btn');
        buttons.forEach(btn => {
            if (show) {
                btn.disabled = true;
                btn.classList.add('loading');
            } else {
                btn.disabled = false;
                btn.classList.remove('loading');
            }
        });
    }
    
    function showToast(type, message) {
        // Remove existing toasts
        const existingToasts = document.querySelectorAll('.toast');
        existingToasts.forEach(toast => toast.remove());
        
        // Icon based on type
        let icon = '';
        if (type === 'success') {
            icon = '<polyline points="20,6 9,17 4,12"></polyline>';
        } else if (type === 'error') {
            icon = '<circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line>';
        } else if (type === 'info') {
            icon = '<circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line>';
        } else {
            icon = '<path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line>';
        }
        
        // Create new toast
        const toast = document.createElement('div');
        toast.className = `toast ${type}`;
        toast.innerHTML = `
            <div style="display: flex; align-items: center; gap: 0.5rem;">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    ${icon}
                </svg>
                <span>${message}</span>
            </div>
        `;
        
        document.body.appendChild(toast);
        
        // Trigger animation
        setTimeout(() => {
            toast.classList.add('show');
        }, 10);
        
        // Auto remove after 5 seconds
        setTimeout(() => {
            toast.classList.remove('show');
            setTimeout(() => {
                toast.remove();
            }, 300);
        }, 5000);
    }
    
    function setupImageZoom() {
        document.addEventListener('click', (e) => {
            const thumbnail = e.target.closest('.category-thumbnail');
            if (thumbnail && thumbnail.tagName === 'IMG') {
                showImageZoomModal(thumbnail.src, thumbnail.alt);
            }
        });
    }
    
    function showImageZoomModal(src, alt) {
        const modal = document.createElement('div');
        modal.className = 'image-zoom-modal';
        modal.innerHTML = `
            <div class="image-zoom-backdrop"></div>
            <div class="image-zoom-content">
                <button class="image-zoom-close">&times;</button>
                <img src="${src}" alt="${alt}" />
                <div class="image-zoom-caption">${alt}</div>
            </div>
        `;
        
        document.body.appendChild(modal);
        setTimeout(() => modal.classList.add('show'), 10);
        
        const closeModal = () => {
            modal.classList.remove('show');
            setTimeout(() => modal.remove(), 300);
        };
        
        modal.querySelector('.image-zoom-close').addEventListener('click', closeModal);
        modal.querySelector('.image-zoom-backdrop').addEventListener('click', closeModal);
    }
    
    function getAntiForgeryToken() {
        const tokenElement = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenElement ? tokenElement.value : '';
    }
});
