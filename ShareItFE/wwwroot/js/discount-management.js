// Global variables
let currentDiscountId = null;
let isEditMode = false;
let allDiscountCodes = []; // Store all discount codes for filtering
let filteredDiscountCodes = []; // Store filtered results
let currentPage = 1;
let pageSize = 5; // Set to 1 for testing pagination

// Initialize page
$(document).ready(function() {
    loadDiscountCodes();
    initializeEventHandlers();
    
    // Initialize Lucide icons
    if (typeof lucide !== 'undefined') {
        lucide.createIcons();
    }
});

// Event handlers
function initializeEventHandlers() {
    $('#create-discount-btn').click(function() {
        openModal('create');
    });

    $('#discount-form').submit(function(e) {
        e.preventDefault();
        if (isEditMode) {
            updateDiscountCode();
        } else {
            createDiscountCode();
        }
    });

    $('#discount-type').change(function() {
        updateValueSymbol();
    });

    // Status toggle handler
    $('#status-toggle').change(function() {
        updateStatusLabel();
    });

    // Search input handler
    $('#search-code').on('input', function() {
        const searchTerm = $(this).val();
        if (searchTerm) {
            $('#clear-search').show();
        } else {
            $('#clear-search').hide();
        }
        applyFilters();
    });

    // Clear search button
    $('#clear-search').click(function() {
        $('#search-code').val('');
        $(this).hide();
        applyFilters();
    });

    // Filter change handlers
    $('#filter-type, #filter-usage-type, #filter-status, #filter-date-from, #filter-date-to').change(function() {
        applyFilters();
    });
    
    // Reset filters button
    $('#reset-filters').click(function() {
        $('#search-code').val('');
        $('#clear-search').hide();
        $('#filter-type').val('');
        $('#filter-usage-type').val('');
        $('#filter-status').val('');
        $('#filter-date-from').val('');
        $('#filter-date-to').val('');
        applyFilters();
    });

    // Initialize value symbol
    updateValueSymbol();
}

// Load all discount codes
function loadDiscountCodes() {
    const token = $('input[name="__RequestVerificationToken"]').val();
    
    $.ajax({
        url: '/Admin/Discounts/DiscountCodes?handler=DiscountCodes',
        type: 'GET',
        headers: {
            'RequestVerificationToken': token
        },
        success: function(response) {
            if (response.success) {
                try {
                    const apiResponse = JSON.parse(response.data);
                    
                    if (apiResponse.data && Array.isArray(apiResponse.data)) {
                        allDiscountCodes = apiResponse.data;
                        applyFilters();
                    } else {
                        showNotification('Invalid data format received', 'error');
                    }
                } catch (parseError) {
                    showNotification('Error parsing API response', 'error');
                }
            } else {
                showNotification('Error: ' + (response.message || 'Unknown error'), 'error');
            }
        },
        error: function(xhr) {
            let errorMessage = 'Failed to load discount codes';
            if (xhr.status === 401) {
                errorMessage = 'Unauthorized access. Please login again.';
            } else if (xhr.status === 403) {
                errorMessage = 'Access denied. Admin privileges required.';
            } else if (xhr.status === 404) {
                errorMessage = 'API endpoint not found.';
            } else if (xhr.status === 500) {
                errorMessage = 'Server error. Please try again later.';
            }
            
            showNotification(errorMessage, 'error');
        }
    });
}

// Apply search and filters
function applyFilters() {
    const searchTerm = $('#search-code').val().toLowerCase();
    const filterType = $('#filter-type').val();
    const filterUsageType = $('#filter-usage-type').val();
    const filterStatus = $('#filter-status').val();
    const filterDateFrom = $('#filter-date-from').val();
    const filterDateTo = $('#filter-date-to').val();

    let filtered = allDiscountCodes;

    // Search by code
    if (searchTerm) {
        filtered = filtered.filter(dc => 
            dc.code.toLowerCase().includes(searchTerm)
        );
    }

    // Filter by discount type
    if (filterType) {
        filtered = filtered.filter(dc => dc.discountType === filterType);
    }

    // Filter by usage type
    if (filterUsageType) {
        filtered = filtered.filter(dc => dc.usageType === filterUsageType);
    }

    // Filter by status
    if (filterStatus) {
        filtered = filtered.filter(dc => dc.status === filterStatus);
    }

    // Filter by expiration date range
    if (filterDateFrom) {
        const fromDate = new Date(filterDateFrom);
        filtered = filtered.filter(dc => new Date(dc.expirationDate) >= fromDate);
    }

    if (filterDateTo) {
        const toDate = new Date(filterDateTo);
        toDate.setHours(23, 59, 59, 999); // End of day
        filtered = filtered.filter(dc => new Date(dc.expirationDate) <= toDate);
    }

    filteredDiscountCodes = filtered;
    currentPage = 1; // Reset to first page
    updateResultsInfo();
    displayCurrentPage();
    renderPagination();
}

// Update results info
function updateResultsInfo() {
    const total = filteredDiscountCodes.length;
    const start = (currentPage - 1) * pageSize + 1;
    const end = Math.min(currentPage * pageSize, total);
    
    $('#results-info').text(`Showing ${total} discount code${total !== 1 ? 's' : ''}`);
    $('#page-start').text(total > 0 ? start : 0);
    $('#page-end').text(end);
    $('#page-total').text(total);
}

// Display current page
function displayCurrentPage() {
    const start = (currentPage - 1) * pageSize;
    const end = start + pageSize;
    const pageData = filteredDiscountCodes.slice(start, end);
    displayDiscountCodes(pageData);
}

// Render pagination - Matching Profile Page Style
// THAY THẾ HOÀN TOÀN HÀM CŨ
function renderPagination() {
    const totalPages = Math.ceil(filteredDiscountCodes.length / pageSize);
    const paginationContainer = $('#pagination-container');
    paginationContainer.empty();

    if (totalPages <= 1) {
        paginationContainer.hide();
        return;
    }

    paginationContainer.show();

    // --- Render nút "Previous" với icon ---
    const isFirstPage = currentPage <= 1;
    paginationContainer.append(`
        <button onclick="changePage(${currentPage - 1})" 
                class="btn ${isFirstPage ? 'disabled' : ''}">
            <i data-lucide="chevron-left"></i>
        </button>
    `);

    // --- Render các số trang ---
    const paginationItems = getPaginationItems(currentPage, totalPages);
    paginationItems.forEach(item => {
        if (item === '...') {
            // Thêm class 'ellipsis' để có thể style riêng nếu muốn
            paginationContainer.append(`
                <span class="btn disabled ellipsis">...</span>
            `);
        } else {
            const pageNum = item;
            const isActive = pageNum === currentPage;
            paginationContainer.append(`
                <button onclick="changePage(${pageNum})" 
                        class="btn ${isActive ? 'btn-primary' : 'btn-secondary'}">
                    ${pageNum}
                </button>
            `);
        }
    });

    // --- Render nút "Next" với icon ---
    const isLastPage = currentPage >= totalPages;
    paginationContainer.append(`
        <button onclick="changePage(${currentPage + 1})" 
                class="btn ${isLastPage ? 'disabled' : ''}">
            <i data-lucide="chevron-right"></i>
        </button>
    `);

    // !!! QUAN TRỌNG: Gọi lại hàm này để render các icon vừa thêm vào
    if (typeof lucide !== 'undefined') {
        lucide.createIcons();
    }
}

// Get pagination items (similar to PaginationHelper in C#)
function getPaginationItems(currentPage, totalPages) {
    const items = [];
    
    if (totalPages <= 7) {
        // Show all pages
        for (let i = 1; i <= totalPages; i++) {
            items.push(i);
        }
    } else {
        // Show first, last, current, and some neighbors
        items.push(1);
        
        if (currentPage > 3) {
            items.push('...');
        }
        
        const start = Math.max(2, currentPage - 1);
        const end = Math.min(totalPages - 1, currentPage + 1);
        
        for (let i = start; i <= end; i++) {
            items.push(i);
        }
        
        if (currentPage < totalPages - 2) {
            items.push('...');
        }
        
        items.push(totalPages);
    }
    
    return items;
}

// Change page
function changePage(page) {
    const totalPages = Math.ceil(filteredDiscountCodes.length / pageSize);
    if (page < 1 || page > totalPages) return;
    
    currentPage = page;
    displayCurrentPage();
    updateResultsInfo();
    renderPagination();
    
    // Scroll to top
    window.scrollTo({ top: 0, behavior: 'smooth' });
}

// Mobile pagination
$(document).on('click', '#prev-mobile', function() {
    changePage(currentPage - 1);
});

$(document).on('click', '#next-mobile', function() {
    changePage(currentPage + 1);
});

// Display discount codes in table
function displayDiscountCodes(discountCodes) {
    const tbody = $('#discount-table-body');
    tbody.empty();

    if (!discountCodes || discountCodes.length === 0) {
        tbody.append(`
            <tr>
                <td colspan="7" class="px-6 py-12 text-center">
                    <div class="flex flex-col items-center justify-center text-gray-500">
                        <i data-lucide="inbox" class="h-12 w-12 mb-3 text-gray-400"></i>
                        <p class="text-lg font-medium">No discount codes found</p>
                        <p class="text-sm">Try adjusting your search or filters</p>
                    </div>
                </td>
            </tr>
        `);
        if (typeof lucide !== 'undefined') {
            lucide.createIcons();
        }
        return;
    }

    discountCodes.forEach(function(discount) {
        const statusClass = getStatusBadgeClass(discount.status);
        const expirationDate = new Date(discount.expirationDate).toLocaleDateString('vi-VN');
        const valueDisplay = discount.discountType === 'Percentage' ? `${discount.value}%` : `${discount.value.toLocaleString('vi-VN')} VND`;
        const progress = Math.min((discount.usedCount / discount.quantity * 100), 100);
        const usageTypeText = discount.usageType;
        const usageTypeBadge = discount.usageType === 'Purchase' ? 'bg-blue-100 text-blue-800' : 'bg-green-100 text-green-800';

            const row = `
            <tr data-id="${discount.id}" class="hover:bg-gray-50 transition-colors">
                <td class="px-6 py-4 whitespace-nowrap">
                    <div class="text-sm font-bold text-gray-900">${discount.code}</div>
                </td>
                <td class="px-6 py-4 whitespace-nowrap">
                    <div class="text-sm text-gray-900">${valueDisplay}</div>
                    <div class="text-xs text-gray-500">${discount.discountType}</div>
                </td>
                <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">${expirationDate}</td>
                <td class="px-6 py-4 whitespace-nowrap">
                    <div class="text-sm text-gray-900 mb-1">${discount.usedCount} / ${discount.quantity}</div>
                    <div class="w-full bg-gray-200 rounded-full h-2">
                        <div class="bg-purple-600 h-2 rounded-full transition-all" style="width: ${progress}%"></div>
                        </div>
                    </td>
                <td class="px-6 py-4 whitespace-nowrap">
                    <span class="px-2.5 py-1 inline-flex text-xs leading-5 font-semibold rounded-full ${usageTypeBadge}">
                        ${usageTypeText}
                    </span>
                </td>
                <td class="px-6 py-4 whitespace-nowrap">
                    <span class="px-2.5 py-1 inline-flex text-xs leading-5 font-semibold rounded-full ${statusClass}">
                        ${discount.status}
                    </span>
                    </td>
                <td class="px-6 py-4 whitespace-nowrap text-sm font-medium">
                    <div class="flex gap-2">
                        <button onclick="editDiscountCode('${discount.id}')" 
                                class="text-purple-600 hover:text-purple-900 p-1 rounded hover:bg-purple-50 transition-colors" 
                                title="Edit">
                            <i data-lucide="edit-2" class="h-5 w-5"></i>
                        </button>
                        <button onclick="deleteDiscountCode('${discount.id}')" 
                                class="text-red-600 hover:text-red-900 p-1 rounded hover:bg-red-50 transition-colors" 
                                title="Delete">
                            <i data-lucide="trash-2" class="h-5 w-5"></i>
                        </button>
                    </div>
                    </td>
                </tr>
            `;
        tbody.append(row);
    });

    // Reinitialize Lucide icons
    if (typeof lucide !== 'undefined') {
        lucide.createIcons();
    }
}

// Get status class for styling
function getStatusClass(status) {
    switch (status.toLowerCase()) {
        case 'active':
            return 'status-active';
        case 'expired':
            return 'status-expired';
        case 'inactive':
            return 'status-inactive';
        default:
            return '';
    }
}

// Get status badge class for Tailwind
function getStatusBadgeClass(status) {
    switch(status) {
        case 'Active':
            return 'bg-green-100 text-green-800';
        case 'Inactive':
            return 'bg-gray-100 text-gray-800';
        case 'Expired':
            return 'bg-red-100 text-red-800';
        default:
            return 'bg-gray-100 text-gray-800';
    }
}

// Open modal for create/edit
function openModal(mode, discountData = null) {
    isEditMode = mode === 'edit';
    const modal = $('#discount-modal');
    const title = $('#modal-title');
    const saveBtn = $('#save-code-btn');

    if (isEditMode) {
        title.text('Edit Discount Code');
        saveBtn.html('<i data-lucide="save" class="h-4 w-4 inline mr-2"></i>Update Code');
        populateForm(discountData);
        currentDiscountId = discountData.id;
    } else {
        title.text('Create Discount Code');
        saveBtn.html('<i data-lucide="save" class="h-4 w-4 inline mr-2"></i>Save Code');
        clearForm();
        currentDiscountId = null;
    }

    modal.removeClass('hidden');
    
    // Reinitialize Lucide icons
    if (typeof lucide !== 'undefined') {
        lucide.createIcons();
    }
}

// Close modal
function closeModal() {
    $('#discount-modal').addClass('hidden');
    clearForm();
    isEditMode = false;
    currentDiscountId = null;
}

// Populate form with discount data
function populateForm(discount) {
            $('#discount-code').val(discount.code);
    $('#discount-type').val(discount.discountType);
            $('#discount-value').val(discount.value);
    $('#expiration-date').val(discount.expirationDate.split('T')[0]);
            $('#quantity').val(discount.quantity);
    $('#usage-type').val(discount.usageType);
    
    // Set status toggle
    const isActive = discount.status === 'Active' || discount.status === 0;
    $('#status-toggle').prop('checked', isActive);
    updateStatusLabel();
    updateValueSymbol();
}

// Clear form
function clearForm() {
            $('#discount-form')[0].reset();
    $('#status-toggle').prop('checked', true); // Default to Active
    $('#usage-type').val('Purchase'); // Default to Purchase
    updateStatusLabel();
    updateValueSymbol();
}

// Update value symbol based on discount type
function updateValueSymbol() {
    const discountType = $('#discount-type').val();
    const valueIcon = $('#value-icon');
    
    if (discountType === 'Percentage') {
        valueIcon.text('%');
    } else {
        valueIcon.text('VND');
    }
}

// Update status label based on toggle
function updateStatusLabel() {
    const isActive = $('#status-toggle').is(':checked');
    const statusLabel = $('#status-label');
    
    if (isActive) {
        statusLabel.text('Active');
        statusLabel.css('color', '#28a745');
        } else {
        statusLabel.text('Inactive');
        statusLabel.css('color', '#6c757d');
    }
}

// Create new discount code
function createDiscountCode() {
    const status = $('#status-toggle').is(':checked') ? 'Active' : 'Inactive';
    
    const formData = {
        code: $('#discount-code').val(),
        discountType: $('#discount-type').val(),
        value: parseFloat($('#discount-value').val()),
        expirationDate: $('#expiration-date').val(),
        quantity: parseInt($('#quantity').val()),
        status: status,
        usageType: $('#usage-type').val()
    };

    // Validate form
    if (!validateForm(formData)) {
        return;
    }

    $.ajax({
        url: '/Admin/Discounts/DiscountCodes?handler=CreateDiscountCode',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(formData),
        headers: {
            'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
        },
        success: function(response) {
            if (response.success) {
                const apiResponse = JSON.parse(response.data);
                if (apiResponse.data) {
                    showNotification('Discount code created successfully!', 'success');
                    closeModal();
                    loadDiscountCodes();
                } else {
                    showNotification('Error: ' + apiResponse.message, 'error');
                }
            } else {
                showNotification('Error: ' + response.message, 'error');
            }
        },
        error: function(xhr) {
            showNotification('Failed to create discount code', 'error');
        }
    });
}

// Edit discount code
function editDiscountCode(id) {
    $.ajax({
        url: `/Admin/Discounts/DiscountCodes?handler=DiscountCode&id=${id}`,
        type: 'GET',
        headers: {
            'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
        },
        success: function(response) {
            if (response.success) {
                const apiResponse = JSON.parse(response.data);
                if (apiResponse.data) {
                    openModal('edit', apiResponse.data);
                } else {
                    showNotification('Error: ' + apiResponse.message, 'error');
                }
            } else {
                showNotification('Error: ' + response.message, 'error');
            }
        },
        error: function(xhr) {
            showNotification('Failed to load discount code', 'error');
        }
    });
}

// Update discount code
function updateDiscountCode() {
    const status = $('#status-toggle').is(':checked') ? 'Active' : 'Inactive';
    
    const formData = {
        code: $('#discount-code').val(),
        discountType: $('#discount-type').val(),
        value: parseFloat($('#discount-value').val()),
        expirationDate: $('#expiration-date').val(),
        quantity: parseInt($('#quantity').val()),
        status: status,
        usageType: $('#usage-type').val()
    };

    // Validate form
    if (!validateForm(formData)) {
        return;
    }

    $.ajax({
        url: `/Admin/Discounts/DiscountCodes?handler=UpdateDiscountCode&id=${currentDiscountId}`,
        type: 'PUT',
        contentType: 'application/json',
        data: JSON.stringify(formData),
        headers: {
            'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
        },
        success: function(response) {
            if (response.success) {
                const apiResponse = JSON.parse(response.data);
                if (apiResponse.data) {
                    showNotification('Discount code updated successfully!', 'success');
                    closeModal();
                    loadDiscountCodes();
                } else {
                    showNotification('Error: ' + apiResponse.message, 'error');
            }
        } else {
                showNotification('Error: ' + response.message, 'error');
            }
        },
        error: function(xhr) {
            showNotification('Failed to update discount code', 'error');
        }
    });
}

// Delete discount code
function deleteDiscountCode(id) {
    if (!confirm('Are you sure you want to delete this discount code? This action cannot be undone.')) {
        return;
    }

    $.ajax({
        url: `/Admin/Discounts/DiscountCodes?handler=DiscountCode&id=${id}`,
        type: 'DELETE',
        headers: {
            'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
        },
        success: function(response) {
            if (response.success) {
                showNotification('Discount code deleted successfully!', 'success');
                loadDiscountCodes(); // Reload all data
            } else {
                showNotification('Error: ' + response.message, 'error');
            }
        },
        error: function(xhr) {
            let errorMessage = 'Failed to delete discount code';
            try {
                const errorResponse = JSON.parse(xhr.responseText);
                if (errorResponse.message) {
                    errorMessage = errorResponse.message;
                }
            } catch (e) {
                // Use default message
            }
            
            showNotification(errorMessage, 'error');
        }
    });
}

// Validate form data
function validateForm(formData) {
    // Check required fields
    if (!formData.code || !formData.discountType || !formData.value || !formData.expirationDate || !formData.quantity || !formData.usageType) {
        showNotification('Please fill in all required fields', 'error');
        return false;
    }

    // Validate discount code format
    if (formData.code.length < 3 || formData.code.length > 50) {
        showNotification('Discount code must be between 3 and 50 characters', 'error');
        return false;
    }

    // Validate value
    if (formData.value <= 0) {
        showNotification('Value must be greater than 0', 'error');
        return false;
    }

    // Validate percentage
    if (formData.discountType === 'Percentage' && formData.value > 100) {
        showNotification('Percentage discount cannot exceed 100%', 'error');
        return false;
    }

    // Validate expiration date
    const expirationDate = new Date(formData.expirationDate);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    
    if (expirationDate <= today) {
        showNotification('Expiration date must be in the future', 'error');
        return false;
    }

    // Validate quantity
    if (formData.quantity < 1) {
        showNotification('Quantity must be at least 1', 'error');
        return false;
    }

    return true;
}

// Show notification
function showNotification(message, type = 'info') {
    // Remove existing notifications
    $('.notification').remove();

    const icons = {
        success: 'check-circle',
        error: 'x-circle',
        info: 'info'
    };

    const colors = {
        success: 'bg-green-50 text-green-800 border-green-200',
        error: 'bg-red-50 text-red-800 border-red-200',
        info: 'bg-blue-50 text-blue-800 border-blue-200'
    };

    const iconColors = {
        success: 'text-green-500',
        error: 'text-red-500',
        info: 'text-blue-500'
    };

    const notification = $(`
        <div class="toast-notification fixed top-4 right-4 z-50 max-w-sm w-full transform transition-all duration-300 ease-in-out translate-x-full">
            <div class="${colors[type] || colors.info} border rounded-lg shadow-lg p-4 flex items-start gap-3">
                <i data-lucide="${icons[type] || icons.info}" class="h-5 w-5 ${iconColors[type] || iconColors.info} flex-shrink-0 mt-0.5"></i>
                <p class="text-sm font-medium flex-1">${message}</p>
                <button onclick="$(this).closest('.toast-notification').remove()" class="text-gray-400 hover:text-gray-600">
                    <i data-lucide="x" class="h-4 w-4"></i>
                </button>
            </div>
        </div>
    `);

    $('body').append(notification);

    // Initialize Lucide icons
    if (typeof lucide !== 'undefined') {
        lucide.createIcons();
    }

    // Slide in animation
    setTimeout(() => {
        notification.removeClass('translate-x-full').addClass('translate-x-0');
    }, 10);

    // Auto-hide after 4 seconds
    setTimeout(function() {
        notification.addClass('translate-x-full');
        setTimeout(() => {
            notification.remove();
        }, 300);
    }, 4000);
}

// Close modal when clicking outside
$(document).click(function(e) {
    if ($(e.target).is('#discount-modal')) {
        closeModal();
    }
});

// Close modal with Escape key
$(document).keydown(function(e) {
    if (e.keyCode === 27) { // Escape key
        closeModal();
    }
});