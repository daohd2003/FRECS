// Global variables
let allStaffList = [];
let filteredStaffList = [];
let currentStaffId = null;
let createStatusActive = true;
let editStatusActive = true;

// Initialize page
$(document).ready(function() {
    loadStaffList();
    initializeEventHandlers();
    checkUrlParams();
    
    // Initialize Lucide icons
    if (typeof lucide !== 'undefined') {
        lucide.createIcons();
    }
});

// Check URL params for auto-opening detail modal
function checkUrlParams() {
    const urlParams = new URLSearchParams(window.location.search);
    const staffId = urlParams.get('staffId');
    const openDetail = urlParams.get('openDetail');
    
    if (staffId && openDetail === 'true') {
        // Wait for staff list to load, then open the edit modal
        const checkStaffLoaded = setInterval(function() {
            if (allStaffList && allStaffList.length > 0) {
                clearInterval(checkStaffLoaded);
                openEditModal(staffId);
                // Clean up URL without page reload
                window.history.replaceState({}, document.title, window.location.pathname);
            }
        }, 100);
        
        // Timeout after 5 seconds
        setTimeout(function() {
            clearInterval(checkStaffLoaded);
        }, 5000);
    }
}

// Event handlers
function initializeEventHandlers() {
    // Create button
    $('#create-staff-btn').click(function() {
        openCreateModal();
    });

    // Create form submit
    $('#create-staff-form').submit(function(e) {
        e.preventDefault();
        createStaff();
    });

    // Edit form submit
    $('#edit-staff-form').submit(function(e) {
        e.preventDefault();
        updateStaff();
    });

    // Search input handler
    $('#search-staff').on('input', function() {
        applyFilters();
    });

    // Filter change handlers
    $('#filter-status, #filter-date-from, #filter-date-to').change(function() {
        applyFilters();
    });

    // Reset filters button
    $('#reset-filters').click(function() {
        $('#search-staff').val('');
        $('#filter-status').val('');
        $('#filter-date-from').val('');
        $('#filter-date-to').val('');
        applyFilters();
    });

    // Password input - show requirements on focus
    $('#create-password').on('focus', function() {
        $('#password-requirements').removeClass('hidden');
    });

    // Password validation on input
    $('#create-password').on('input', function() {
        validatePassword($(this).val());
    });
}

// Load all staff
function loadStaffList() {
    const token = $('input[name="__RequestVerificationToken"]').val();
    
    $.ajax({
        url: '/Admin/StaffManagement?handler=StaffList',
        type: 'GET',
        headers: {
            'RequestVerificationToken': token
        },
        success: function(response) {
            if (response.success) {
                try {
                    const apiResponse = JSON.parse(response.data);
                    
                    if (apiResponse.data && Array.isArray(apiResponse.data)) {
                        allStaffList = apiResponse.data;
                        applyFilters();
                        updateStats();
                    } else {
                        showNotification('Invalid data format received', 'error');
                    }
                } catch (parseError) {
                    console.error('Parse error:', parseError);
                    showNotification('Error parsing API response', 'error');
                }
            } else {
                showNotification('Error: ' + (response.message || 'Unknown error'), 'error');
            }
        },
        error: function(xhr) {
            handleAjaxError(xhr, 'Failed to load staff list');
        }
    });
}

// Apply search and filters
function applyFilters() {
    const searchTerm = $('#search-staff').val().toLowerCase();
    const filterStatus = $('#filter-status').val();
    const filterDateFrom = $('#filter-date-from').val();
    const filterDateTo = $('#filter-date-to').val();

    let filtered = allStaffList;

    // Search by name or email
    if (searchTerm) {
        filtered = filtered.filter(staff => 
            staff.fullName.toLowerCase().includes(searchTerm) ||
            staff.email.toLowerCase().includes(searchTerm)
        );
    }

    // Filter by status
    if (filterStatus) {
        const isActive = filterStatus === 'active';
        filtered = filtered.filter(staff => staff.isActive === isActive);
    }

    // Filter by date range
    if (filterDateFrom) {
        const fromDate = new Date(filterDateFrom);
        filtered = filtered.filter(staff => new Date(staff.createdAt) >= fromDate);
    }

    if (filterDateTo) {
        const toDate = new Date(filterDateTo);
        toDate.setHours(23, 59, 59, 999);
        filtered = filtered.filter(staff => new Date(staff.createdAt) <= toDate);
    }

    filteredStaffList = filtered;
    displayStaffList();
}

// Display staff list
function displayStaffList() {
    const tbody = $('#staff-table-body');
    
    if (filteredStaffList.length === 0) {
        tbody.html(`
            <tr>
                <td colspan="5" class="px-6 py-12 text-center">
                    <div class="flex flex-col items-center justify-center">
                        <i data-lucide="users" class="h-16 w-16 text-gray-300 mb-4"></i>
                        <h3 class="text-lg font-medium text-gray-900 mb-2">No staff found</h3>
                        <p class="text-gray-500">Try adjusting your search or filter criteria.</p>
                    </div>
                </td>
            </tr>
        `);
        lucide.createIcons();
        return;
    }

    let html = '';
    filteredStaffList.forEach(staff => {
        const initials = getInitials(staff.fullName);
        const statusClass = staff.isActive ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800';
        const statusDot = staff.isActive ? 'bg-green-500' : 'bg-red-500';
        const statusText = staff.isActive ? 'Active' : 'Inactive';
        const formattedDate = formatDate(staff.createdAt);

        html += `
            <tr class="hover:bg-gray-50 transition-colors">
                <td class="px-6 py-4 whitespace-nowrap">
                    <div class="flex items-center">
                        <div class="w-10 h-10 bg-purple-600 rounded-full flex items-center justify-center">
                            <span class="text-white font-semibold text-sm">${initials}</span>
                        </div>
                        <div class="ml-4">
                            <div class="text-sm font-medium text-gray-900">${staff.fullName}</div>
                        </div>
                    </div>
                </td>
                <td class="px-6 py-4 whitespace-nowrap">
                    <div class="flex items-center space-x-2">
                        <i data-lucide="mail" class="h-4 w-4 text-gray-400"></i>
                        <span class="text-sm text-gray-900">${staff.email}</span>
                    </div>
                </td>
                <td class="px-6 py-4 whitespace-nowrap">
                    <span class="inline-flex items-center px-3 py-1 text-xs font-semibold rounded-full ${statusClass}">
                        <div class="w-2 h-2 rounded-full mr-2 ${statusDot}"></div>
                        ${statusText}
                    </span>
                </td>
                <td class="px-6 py-4 whitespace-nowrap">
                    <div class="flex items-center space-x-2">
                        <i data-lucide="calendar" class="h-4 w-4 text-gray-400"></i>
                        <span class="text-sm text-gray-900">${formattedDate}</span>
                    </div>
                </td>
                <td class="px-6 py-4 whitespace-nowrap">
                    <div class="flex items-center space-x-3">
                        <button onclick="openChatWithStaff('${staff.id}')" 
                                class="text-purple-600 hover:text-purple-900 transition-colors p-2 hover:bg-purple-50 rounded-lg" 
                                title="Chat with Staff">
                            <i data-lucide="message-circle" class="h-4 w-4"></i>
                        </button>
                        <button onclick="openEditModal('${staff.id}')" 
                                class="text-blue-600 hover:text-blue-900 transition-colors p-2 hover:bg-blue-50 rounded-lg" 
                                title="Edit Staff">
                            <i data-lucide="edit" class="h-4 w-4"></i>
                        </button>
                        <button onclick="openDeleteModal('${staff.id}')" 
                                class="text-red-600 hover:text-red-900 transition-colors p-2 hover:bg-red-50 rounded-lg" 
                                title="Delete Staff">
                            <i data-lucide="trash-2" class="h-4 w-4"></i>
                        </button>
                    </div>
                </td>
            </tr>
        `;
    });

    tbody.html(html);
    lucide.createIcons();
}

// Update statistics
function updateStats() {
    const totalStaff = allStaffList.length;
    const activeStaff = allStaffList.filter(s => s.isActive).length;
    const inactiveStaff = allStaffList.filter(s => !s.isActive).length;
    
    // This month
    const now = new Date();
    const monthlyStaff = allStaffList.filter(s => {
        const createdDate = new Date(s.createdAt);
        return createdDate.getMonth() === now.getMonth() && 
               createdDate.getFullYear() === now.getFullYear();
    }).length;

    $('#total-staff').text(totalStaff);
    $('#active-staff').text(activeStaff);
    $('#inactive-staff').text(inactiveStaff);
    $('#monthly-staff').text(monthlyStaff);
}

// Create staff
function createStaff() {
    const fullName = $('#create-fullname').val();
    const email = $('#create-email').val();
    const password = $('#create-password').val();
    const confirmPassword = $('#create-confirm-password').val();

    // Validate
    if (!fullName || !email || !password || !confirmPassword) {
        showNotification('Please fill in all required fields', 'error');
        return;
    }

    if (password !== confirmPassword) {
        showNotification('Passwords do not match', 'error');
        return;
    }

    if (!isPasswordValid(password)) {
        showNotification('Password does not meet requirements', 'error');
        return;
    }

    const staffData = {
        fullName: fullName,
        email: email,
        password: password,
        confirmPassword: confirmPassword,
        isActive: createStatusActive
    };

    const token = $('input[name="__RequestVerificationToken"]').val();
    
    $.ajax({
        url: '/Admin/StaffManagement?handler=CreateStaff',
        type: 'POST',
        headers: {
            'RequestVerificationToken': token,
            'Content-Type': 'application/json'
        },
        data: JSON.stringify(staffData),
        success: function(response) {
            if (response.success) {
                showNotification('Staff created successfully', 'success');
                closeCreateModal();
                loadStaffList();
            } else {
                const errorMsg = extractErrorMessage(response);
                showNotification(errorMsg, 'error');
            }
        },
        error: function(xhr) {
            handleAjaxError(xhr, 'Failed to create staff');
        }
    });
}

// Open edit modal
function openEditModal(id) {
    currentStaffId = id;
    const staff = allStaffList.find(s => s.id === id);
    
    if (!staff) {
        showNotification('Staff not found', 'error');
        return;
    }

    $('#edit-staff-id').val(staff.id);
    $('#edit-fullname').val(staff.fullName);
    $('#edit-email').val(staff.email);
    
    editStatusActive = staff.isActive;
    updateToggleButton('edit', editStatusActive);

    $('#edit-modal').removeClass('hidden');
}

// Update staff
function updateStaff() {
    const id = $('#edit-staff-id').val();
    const fullName = $('#edit-fullname').val();
    const email = $('#edit-email').val();

    if (!fullName || !email) {
        showNotification('Please fill in all required fields', 'error');
        return;
    }

    const staffData = {
        fullName: fullName,
        email: email,
        isActive: editStatusActive
    };

    const token = $('input[name="__RequestVerificationToken"]').val();
    
    $.ajax({
        url: '/Admin/StaffManagement?handler=UpdateStaff&id=' + id,
        type: 'PUT',
        headers: {
            'RequestVerificationToken': token,
            'Content-Type': 'application/json'
        },
        data: JSON.stringify(staffData),
        success: function(response) {
            if (response.success) {
                showNotification('Staff updated successfully', 'success');
                closeEditModal();
                loadStaffList();
            } else {
                const errorMsg = extractErrorMessage(response);
                showNotification(errorMsg, 'error');
            }
        },
        error: function(xhr) {
            handleAjaxError(xhr, 'Failed to update staff');
        }
    });
}

// Open delete modal
function openDeleteModal(id) {
    currentStaffId = id;
    const staff = allStaffList.find(s => s.id === id);
    
    if (!staff) {
        showNotification('Staff not found', 'error');
        return;
    }

    const initials = getInitials(staff.fullName);
    
    $('#delete-staff-info').html(`
        <div class="flex items-center space-x-3">
            <div class="w-10 h-10 bg-purple-600 rounded-full flex items-center justify-center">
                <span class="text-white font-semibold text-sm">${initials}</span>
            </div>
            <div>
                <p class="font-medium text-gray-900">${staff.fullName}</p>
                <p class="text-sm text-gray-600">${staff.email}</p>
            </div>
        </div>
    `);

    $('#delete-modal').removeClass('hidden');
}

// Confirm delete
function confirmDelete() {
    if (!currentStaffId) return;

    const token = $('input[name="__RequestVerificationToken"]').val();
    
    $.ajax({
        url: '/Admin/StaffManagement?handler=Staff&id=' + currentStaffId,
        type: 'DELETE',
        headers: {
            'RequestVerificationToken': token
        },
        success: function(response) {
            if (response.success) {
                showNotification('Staff deleted successfully', 'success');
                closeDeleteModal();
                loadStaffList();
            } else {
                showNotification('Error: ' + (response.message || 'Unknown error'), 'error');
            }
        },
        error: function(xhr) {
            handleAjaxError(xhr, 'Failed to delete staff');
        }
    });
}

// Send password reset
function sendPasswordReset() {
    if (!currentStaffId) return;

    const token = $('input[name="__RequestVerificationToken"]').val();
    
    $.ajax({
        url: '/Admin/StaffManagement?handler=SendPasswordReset&id=' + currentStaffId,
        type: 'POST',
        headers: {
            'RequestVerificationToken': token
        },
        success: function(response) {
            if (response.success) {
                showNotification('Password reset link sent successfully', 'success');
            } else {
                showNotification('Error: ' + (response.message || 'Unknown error'), 'error');
            }
        },
        error: function(xhr) {
            handleAjaxError(xhr, 'Failed to send password reset');
        }
    });
}

// Modal functions
function openCreateModal() {
    $('#create-fullname').val('');
    $('#create-email').val('');
    $('#create-password').val('');
    $('#create-confirm-password').val('');
    createStatusActive = true;
    updateToggleButton('create', true);
    $('#password-requirements').addClass('hidden');
    resetPasswordRequirements();
    $('#create-modal').removeClass('hidden');
}

function closeCreateModal() {
    $('#create-modal').addClass('hidden');
}

function closeEditModal() {
    $('#edit-modal').addClass('hidden');
    currentStaffId = null;
}

function closeDeleteModal() {
    $('#delete-modal').addClass('hidden');
    currentStaffId = null;
}

// Toggle password visibility
function togglePassword(inputId, iconId) {
    const input = $('#' + inputId);
    const icon = $('#' + iconId);
    
    if (input.attr('type') === 'password') {
        input.attr('type', 'text');
        icon.attr('data-lucide', 'eye-off');
    } else {
        input.attr('type', 'password');
        icon.attr('data-lucide', 'eye');
    }
    
    lucide.createIcons();
}

// Toggle status
function toggleStatus(mode) {
    if (mode === 'create') {
        createStatusActive = !createStatusActive;
        updateToggleButton('create', createStatusActive);
    } else if (mode === 'edit') {
        editStatusActive = !editStatusActive;
        updateToggleButton('edit', editStatusActive);
    }
}

function updateToggleButton(mode, isActive) {
    const toggle = $(`#${mode}-status-toggle`);
    const span = toggle.find('span');
    
    if (isActive) {
        toggle.removeClass('bg-gray-300').addClass('bg-purple-600');
        span.removeClass('translate-x-1').addClass('translate-x-6');
    } else {
        toggle.removeClass('bg-purple-600').addClass('bg-gray-300');
        span.removeClass('translate-x-6').addClass('translate-x-1');
    }
}

// Password validation
function validatePassword(password) {
    const requirements = {
        length: password.length >= 8,
        uppercase: /[A-Z]/.test(password),
        lowercase: /[a-z]/.test(password),
        number: /\d/.test(password),
        special: /[!@#$%^&*(),.?":{}|<>]/.test(password)
    };

    updateRequirement('req-length', requirements.length);
    updateRequirement('req-uppercase', requirements.uppercase);
    updateRequirement('req-lowercase', requirements.lowercase);
    updateRequirement('req-number', requirements.number);
    updateRequirement('req-special', requirements.special);

    return Object.values(requirements).every(v => v);
}

function updateRequirement(id, isMet) {
    const element = $('#' + id);
    const icon = element.find('i');
    
    if (isMet) {
        element.removeClass('text-gray-600').addClass('text-green-600');
        icon.attr('data-lucide', 'check-circle');
    } else {
        element.removeClass('text-green-600').addClass('text-gray-600');
        icon.attr('data-lucide', 'circle');
    }
    
    lucide.createIcons();
}

function resetPasswordRequirements() {
    ['req-length', 'req-uppercase', 'req-lowercase', 'req-number', 'req-special'].forEach(id => {
        updateRequirement(id, false);
    });
}

function isPasswordValid(password) {
    return password.length >= 8 &&
           /[A-Z]/.test(password) &&
           /[a-z]/.test(password) &&
           /\d/.test(password) &&
           /[!@#$%^&*(),.?":{}|<>]/.test(password);
}

// Utility functions
function getInitials(fullName) {
    const names = fullName.split(' ');
    if (names.length === 1) {
        return names[0].substring(0, 2).toUpperCase();
    }
    return (names[0].charAt(0) + names[names.length - 1].charAt(0)).toUpperCase();
}

function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        timeZone: 'Asia/Ho_Chi_Minh'
    });
}

function extractErrorMessage(response) {
    if (response.error) {
        try {
            const errorObj = JSON.parse(response.error);
            return errorObj.message || response.message || 'Operation failed';
        } catch {
            return response.message || 'Operation failed';
        }
    }
    return response.message || 'Operation failed';
}

function handleAjaxError(xhr, defaultMessage) {
    let errorMessage = defaultMessage;
    
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

// Open chat with staff
function openChatWithStaff(staffId) {
    const staff = allStaffList.find(s => s.id === staffId);
    if (!staff) {
        showNotification('Staff not found', 'error');
        return;
    }

    // Open floating chat window
    if (window.openFloatingChat) {
        const avatar = getInitials(staff.fullName);
        window.openFloatingChat(staffId, staff.fullName, avatar);
    } else {
        showNotification('Chat system not available', 'error');
    }
}

function showNotification(message, type) {
    // Using toast notification system
    if (window.toastManager) {
        if (type === 'error') {
            window.toastManager.error(message);
        } else if (type === 'success') {
            window.toastManager.success(message);
        } else if (type === 'warning') {
            window.toastManager.warning(message);
        } else {
            window.toastManager.info(message);
        }
    } else {
        // Fallback to alert if toast manager not loaded
        console.warn('Toast manager not loaded, using alert');
        alert(message);
    }
}

