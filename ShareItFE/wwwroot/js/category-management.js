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
    const uploadImageBtn = document.getElementById('uploadImageBtn');
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
    let uploadedImageUrl = '';
    let isActive = true;
    
    // Initialize
    init();
    
    function init() {
        setupEventListeners();
        setupCharacterCounters();
        setupImageUpload();
        setupSearchAndFilter();
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
        uploadImageBtn?.addEventListener('click', handleImageUpload);
    }
    
    function setupSearchAndFilter() {
        searchInput?.addEventListener('input', filterCategories);
        statusFilter?.addEventListener('change', filterCategories);
        sortFilter?.addEventListener('change', filterCategories);
    }
    
    function handleActionClick(e) {
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
        
        // Update character counters
        if (nameCharCount) nameCharCount.textContent = category.name.length;
        if (descCharCount) descCharCount.textContent = (category.description || '').length;
        
        // Update status
        updateStatusDisplay();
        
        // Update image preview if exists
        if (category.imageUrl) {
            showImagePreview(category.imageUrl);
            uploadedImageUrl = category.imageUrl;
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
        uploadedImageUrl = '';
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
            
            // Show upload button
            if (uploadImageBtn) {
                uploadImageBtn.style.display = 'block';
            }
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
        
        if (uploadImageBtn) {
            uploadImageBtn.style.display = 'none';
        }
    }
    
    async function handleImageUpload() {
        const file = categoryImageInput.files[0];
        if (!file) {
            showToast('error', 'Please select an image file first');
            return;
        }
        
        try {
            showLoading(true);
            
            const formData = new FormData();
            formData.append('image', file);
            
            const response = await fetch('/Staff/CategoryCreate?handler=UploadImage', {
                method: 'POST',
                body: formData,
                headers: {
                    'RequestVerificationToken': getAntiForgeryToken()
                }
            });
            
            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    uploadedImageUrl = result.imageUrl;
                    showToast('success', 'Image uploaded successfully');
                    
                    // Hide upload button
                    if (uploadImageBtn) {
                        uploadImageBtn.style.display = 'none';
                    }
                } else {
                    showToast('error', result.message);
                }
            } else {
                showToast('error', 'Failed to upload image');
            }
        } catch (error) {
            console.error('Error uploading image:', error);
            showToast('error', 'Error uploading image');
        } finally {
            showLoading(false);
        }
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
        
        try {
            showLoading(true);
            
            const formData = new FormData();
            formData.append('categoryName', categoryNameInput.value);
            formData.append('categoryDescription', categoryDescriptionInput.value);
            formData.append('isActive', isActive.toString());
            
            // Add image if uploaded
            if (uploadedImageUrl) {
                formData.append('imageUrl', uploadedImageUrl);
            }
            
            // Add image file if selected but not uploaded
            const imageFile = categoryImageInput.files[0];
            if (imageFile && !uploadedImageUrl) {
                formData.append('categoryImage', imageFile);
            }
            
            const isEdit = modalModeInput.value === 'edit';
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
                    showToast('success', result.message);
                    closeModal();
                    // Reload page to show updated data
                    setTimeout(() => {
                        window.location.reload();
                    }, 1000);
                } else {
                    showToast('error', result.message);
                }
            } else {
                showToast('error', 'Failed to save category');
            }
        } catch (error) {
            console.error('Error saving category:', error);
            showToast('error', 'Error saving category');
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
                    showToast('success', result.message);
                    closeConfirmModal();
                    // Reload page to show updated data
                    setTimeout(() => {
                        window.location.reload();
                    }, 1000);
                } else {
                    showToast('error', result.message);
                }
            } else {
                showToast('error', 'Failed to delete category');
            }
        } catch (error) {
            console.error('Error deleting category:', error);
            showToast('error', 'Error deleting category');
        } finally {
            showLoading(false);
        }
    }
    
    function viewCategory(categoryId) {
        // Navigate to category products page or show category details
        window.location.href = `/Staff/CategoryProducts?categoryId=${categoryId}`;
    }
    
    function filterCategories() {
        const searchTerm = searchInput.value.toLowerCase();
        const statusFilterValue = statusFilter.value;
        const sortValue = sortFilter.value;
        
        const rows = document.querySelectorAll('#categoriesTableBody tr');
        let visibleRows = [];
        
        rows.forEach(row => {
            const categoryName = row.querySelector('.category-name').textContent.toLowerCase();
            const categoryDescription = row.querySelector('.category-description').textContent.toLowerCase();
            const statusToggle = row.querySelector('.status-toggle');
            const isActive = statusToggle.classList.contains('active');
            
            // Apply search filter
            const matchesSearch = !searchTerm || 
                categoryName.includes(searchTerm) || 
                categoryDescription.includes(searchTerm);
            
            // Apply status filter
            const matchesStatus = statusFilterValue === 'all' ||
                (statusFilterValue === 'active' && isActive) ||
                (statusFilterValue === 'inactive' && !isActive);
            
            if (matchesSearch && matchesStatus) {
                row.style.display = '';
                visibleRows.push(row);
            } else {
                row.style.display = 'none';
            }
        });
        
        // Apply sorting
        if (sortValue) {
            const [sortBy, sortOrder] = sortValue.split('-');
            visibleRows.sort((a, b) => {
                let aValue, bValue;
                
                switch (sortBy) {
                    case 'name':
                        aValue = a.querySelector('.category-name').textContent.toLowerCase();
                        bValue = b.querySelector('.category-name').textContent.toLowerCase();
                        break;
                    case 'createdAt':
                        // For now, just use the order in DOM
                        aValue = Array.from(rows).indexOf(a);
                        bValue = Array.from(rows).indexOf(b);
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
            
            // Reorder rows
            const tbody = document.querySelector('#categoriesTableBody');
            visibleRows.forEach(row => {
                tbody.appendChild(row);
            });
        }
        
        // Show/hide empty state
        const emptyState = document.getElementById('emptyState');
        if (visibleRows.length === 0) {
            emptyState.style.display = 'block';
        } else {
            emptyState.style.display = 'none';
        }
    }
    
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
        
        // Create new toast
        const toast = document.createElement('div');
        toast.className = `toast ${type}`;
        toast.innerHTML = `
            <div style="display: flex; align-items: center; gap: 0.5rem;">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    ${type === 'success' ? '<polyline points="20,6 9,17 4,12"></polyline>' : 
                      type === 'error' ? '<circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line>' :
                      '<path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line>'}
                </svg>
                <span>${message}</span>
            </div>
        `;
        
        document.body.appendChild(toast);
        
        // Auto remove after 5 seconds
        setTimeout(() => {
            toast.remove();
        }, 5000);
    }
    
    function getAntiForgeryToken() {
        const tokenElement = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenElement ? tokenElement.value : '';
    }
});
