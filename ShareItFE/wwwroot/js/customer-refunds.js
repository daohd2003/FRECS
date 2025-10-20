// Global variables
let allRefunds = []; // Store all refunds for filtering
let filteredRefunds = []; // Store filtered results
let currentPage = 1;
let pageSize = 10;
let selectedRefund = null;

// Initialize page
document.addEventListener('DOMContentLoaded', function () {
    loadRefunds();
    initializeEventHandlers();
});

// Event handlers
function initializeEventHandlers() {
    // Search input handler
    const searchInput = document.getElementById('searchInput');
    const clearSearchBtn = document.getElementById('clear-search');

    if (searchInput) {
        searchInput.addEventListener('input', function() {
            if (this.value) {
                clearSearchBtn.classList.remove('hidden');
            } else {
                clearSearchBtn.classList.add('hidden');
            }
            applyFilters();
        });
    }

    // Clear search button
    if (clearSearchBtn) {
        clearSearchBtn.addEventListener('click', () => {
            searchInput.value = '';
            clearSearchBtn.classList.add('hidden');
            applyFilters();
        });
    }

    // Filter change handler
    const statusFilter = document.getElementById('statusSelect');
    if (statusFilter) {
        statusFilter.addEventListener('change', applyFilters);
    }

    // Reset filters button
    const resetFiltersBtn = document.getElementById('reset-filters');
    if (resetFiltersBtn) {
        resetFiltersBtn.addEventListener('click', () => {
            searchInput.value = '';
            statusFilter.value = '';
            clearSearchBtn.classList.add('hidden');
            applyFilters();
        });
    }

    // Filter panel toggle
    const filterBtn = document.getElementById('filterBtn');
    const filterPanel = document.getElementById('filterPanel');
    if (filterBtn) {
        filterBtn.addEventListener('click', () => {
            filterPanel.classList.toggle('hidden');
        });
    }
}

// Load all refunds
function loadRefunds() {
    const token = accessToken;
    if (!token) {
        showNotification('Not authenticated. Please login.', 'error');
        return;
    }

    fetch('/Admin/CustomerRefunds?handler=Refunds', {
        headers: {
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
        }
    })
    .then(response => {
        if (!response.ok) {
            throw new Error('Failed to load refunds');
        }
        return response.json();
    })
    .then(result => {
        if (result.success) {
            const apiResponse = JSON.parse(result.data);
            if (apiResponse.data && Array.isArray(apiResponse.data)) {
                allRefunds = apiResponse.data;
                applyFilters();
            } else {
                showNotification('Invalid data format received', 'error');
            }
        } else {
            showNotification('Error: ' + (result.message || 'Unknown error'), 'error');
        }
    })
    .catch(error => {
        console.error('Error loading refunds:', error);
        showNotification('Failed to load refund data', 'error');
        displayEmptyState();
    });
}

// Apply search and filters
function applyFilters() {
    const searchTerm = document.getElementById('searchInput').value.toLowerCase();
    const statusFilterValue = document.getElementById('statusSelect').value;

    let filtered = allRefunds;

    // Search by order code, customer name, or email
    if (searchTerm) {
        filtered = filtered.filter(refund => 
            (refund.orderCode && refund.orderCode.toLowerCase().includes(searchTerm)) ||
            (refund.customerName && refund.customerName.toLowerCase().includes(searchTerm)) ||
            (refund.customerEmail && refund.customerEmail.toLowerCase().includes(searchTerm)) ||
            (refund.refundCode && refund.refundCode.toLowerCase().includes(searchTerm))
        );
    }

    // Filter by status
    if (statusFilterValue) {
        filtered = filtered.filter(refund => {
            const status = String(refund.status || '').toLowerCase();
            return status === statusFilterValue.toLowerCase();
        });
    }

    filteredRefunds = filtered;
    currentPage = 1; // Reset to first page
    updateResultsInfo();
    displayCurrentPage();
    renderPagination();
}

// Update results info
function updateResultsInfo() {
    const total = filteredRefunds.length;
    document.getElementById('results-info').textContent = `Showing ${total} refund request${total !== 1 ? 's' : ''}`;
}

// Display current page
function displayCurrentPage() {
    const pendingRefunds = filteredRefunds.filter(r => String(r.status || '').toLowerCase() === 'initiated');
    const processedRefunds = filteredRefunds.filter(r => String(r.status || '').toLowerCase() !== 'initiated');

    displayPendingRefunds(pendingRefunds);
    displayProcessedRefunds(processedRefunds);

    // Update badges
    document.getElementById('pending-count-badge').textContent = pendingRefunds.length;
    document.getElementById('processed-count-badge').textContent = processedRefunds.length;
    
    // Update header pending badge
    const headerPendingBadge = document.getElementById('header-pending-badge');
    const headerPendingCount = document.getElementById('header-pending-count');
    if (pendingRefunds.length > 0) {
        headerPendingCount.textContent = pendingRefunds.length;
        headerPendingBadge.style.display = 'inline-block';
    } else {
        headerPendingBadge.style.display = 'none';
    }
}

// Display pending refunds
function displayPendingRefunds(refunds) {
    const tbody = document.getElementById('pending-tbody');
    tbody.innerHTML = '';

    if (refunds.length === 0) {
        tbody.innerHTML = `
            <tr>
                <td colspan="7" class="text-center py-12">
                    <div class="empty-state">
                        <i class="fas fa-inbox fa-3x text-gray-400 mb-3"></i>
                        <h3>No Pending Refunds</h3>
                        <p>All refund requests have been processed</p>
                    </div>
                </td>
            </tr>
        `;
        return;
    }

    refunds.forEach(refund => {
        const row = createRefundRow(refund, true);
        tbody.innerHTML += row;
    });
}

// Display processed refunds
function displayProcessedRefunds(refunds) {
    const tbody = document.getElementById('processed-tbody');
    tbody.innerHTML = '';

    if (refunds.length === 0) {
        tbody.innerHTML = `
            <tr>
                <td colspan="7" class="text-center py-12">
                    <div class="empty-state">
                        <i class="fas fa-history fa-3x text-gray-400 mb-3"></i>
                        <h3>No Refund History</h3>
                        <p>No refunds have been processed yet</p>
                    </div>
                </td>
            </tr>
        `;
        return;
    }

    refunds.forEach(refund => {
        const row = createRefundRow(refund, false);
        tbody.innerHTML += row;
    });
}

// Create refund table row
function createRefundRow(refund, isPending) {
    const requestDate = new Date(refund.createdAt).toLocaleString('en-US', { 
        month: 'short', day: 'numeric', year: 'numeric', hour: '2-digit', minute: '2-digit' 
    });
    
    const statusBadge = getStatusBadge(refund.status);
    const bankInfo = refund.customerBankName ? 
        '<i class="fas fa-university text-primary"></i> <span>Bank Transfer</span>' : 
        '<span class="text-muted">Not specified</span>';

    if (isPending) {
        return `
            <tr>
                <td><i class="far fa-calendar"></i> ${requestDate}</td>
                <td>
                    <div class="customer-info">
                        <strong>${escapeHtml(refund.customerName)}</strong>
                        <small>${escapeHtml(refund.customerEmail)}</small>
                    </div>
                </td>
                <td><span class="order-code">${escapeHtml(refund.orderCode)}</span></td>
                <td>
                    <span class="amount">${formatCurrency(refund.refundAmount)}</span>
                    ${refund.totalPenaltyAmount > 0 ? `
                        <small class="text-muted d-block">
                            (Original: ${formatCurrency(refund.originalDepositAmount)} - Penalty: ${formatCurrency(refund.totalPenaltyAmount)})
                        </small>
                    ` : ''}
                </td>
                <td>${bankInfo}</td>
                <td>${statusBadge}</td>
                <td>
                    <button class="btn-action btn-view" onclick="viewRefundDetail('${refund.id}')" title="View Details">
                        <i class="fas fa-eye"></i> View Details
                    </button>
                </td>
            </tr>
        `;
    } else {
        const processedDate = refund.processedAt ? 
            new Date(refund.processedAt).toLocaleString('en-US', { 
                month: 'short', day: 'numeric', year: 'numeric', hour: '2-digit', minute: '2-digit' 
            }) : 'N/A';

        return `
            <tr>
                <td><i class="far fa-calendar"></i> ${requestDate}</td>
                <td>
                    <div class="customer-info">
                        <strong>${escapeHtml(refund.customerName)}</strong>
                        <small>${escapeHtml(refund.customerEmail)}</small>
                    </div>
                </td>
                <td><span class="order-code">${escapeHtml(refund.orderCode)}</span></td>
                <td><span class="amount">${formatCurrency(refund.refundAmount)}</span></td>
                <td>${statusBadge}</td>
                <td>${processedDate}</td>
                <td>
                    <button class="btn-action btn-view" onclick="viewRefundDetail('${refund.id}')" title="View Details">
                        <i class="fas fa-eye"></i> View Details
                    </button>
                </td>
            </tr>
        `;
    }
}

// Get status badge HTML
function getStatusBadge(status) {
    const statusLower = String(status || '').toLowerCase();
    const statusMap = {
        'initiated': '<span class="status-badge status-pending"><i class="fas fa-clock"></i> Initiated</span>',
        'completed': '<span class="status-badge status-completed"><i class="fas fa-check-circle"></i> Completed</span>',
        'failed': '<span class="status-badge status-failed"><i class="fas fa-times-circle"></i> Failed</span>'
    };
    return statusMap[statusLower] || `<span class="status-badge">${status}</span>`;
}

// Render pagination
function renderPagination() {
    const totalPages = Math.ceil(filteredRefunds.length / pageSize);
    const paginationContainer = document.getElementById('pagination-container');
    paginationContainer.innerHTML = '';

    if (totalPages <= 1) {
        return;
    }

    // Previous button
    const prevDisabled = currentPage <= 1 ? 'disabled' : '';
    paginationContainer.innerHTML += `
        <button onclick="changePage(${currentPage - 1})" class="btn ${prevDisabled}">
            <i class="fas fa-chevron-left"></i>
        </button>
    `;

    // Page numbers
    const paginationItems = getPaginationItems(currentPage, totalPages);
    paginationItems.forEach(item => {
        if (item === '...') {
            paginationContainer.innerHTML += `<span class="btn disabled">...</span>`;
        } else {
            const isActive = item === currentPage;
            paginationContainer.innerHTML += `
                <button onclick="changePage(${item})" class="btn ${isActive ? 'btn-primary' : 'btn-secondary'}">
                    ${item}
                </button>
            `;
        }
    });

    // Next button
    const nextDisabled = currentPage >= totalPages ? 'disabled' : '';
    paginationContainer.innerHTML += `
        <button onclick="changePage(${currentPage + 1})" class="btn ${nextDisabled}">
            <i class="fas fa-chevron-right"></i>
        </button>
    `;
}

// Get pagination items
function getPaginationItems(currentPage, totalPages) {
    const items = [];
    
    if (totalPages <= 7) {
        for (let i = 1; i <= totalPages; i++) {
            items.push(i);
        }
    } else {
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
    const totalPages = Math.ceil(filteredRefunds.length / pageSize);
    if (page < 1 || page > totalPages) return;
    
    currentPage = page;
    displayCurrentPage();
    updateResultsInfo();
    renderPagination();
}

// Display empty state
function displayEmptyState() {
    const pendingTbody = document.getElementById('pending-tbody');
    const processedTbody = document.getElementById('processed-tbody');
    
    const emptyHtml = `
        <tr>
            <td colspan="7" class="text-center py-12">
                <div class="empty-state">
                    <i class="fas fa-exclamation-circle fa-3x text-gray-400 mb-3"></i>
                    <h3>Failed to Load Data</h3>
                    <p>Please try refreshing the page</p>
                </div>
            </td>
        </tr>
    `;
    
    pendingTbody.innerHTML = emptyHtml;
    processedTbody.innerHTML = emptyHtml;
}

// View refund detail
window.viewRefundDetail = async function(refundId) {
    const token = accessToken;
    if (!token) {
        showNotification('Not authenticated. Please login.', 'error');
        return;
    }

    try {
        const response = await fetch(`${apiBaseUrl}/api/depositrefunds/${refundId}`, {
            headers: {
                'Authorization': `Bearer ${token}`
            }
        });

        if (response.ok) {
            const apiResponse = await response.json();
            if (apiResponse.data) {
                selectedRefund = apiResponse.data;
                populateRefundModal(selectedRefund);
                const modal = new bootstrap.Modal(document.getElementById('refundDetailModal'));
                modal.show();
            } else {
                showNotification('Failed to load refund details', 'error');
            }
        } else {
            showNotification('Failed to load refund details', 'error');
        }
    } catch (error) {
        console.error('Error fetching refund details:', error);
        showNotification('Failed to load refund details', 'error');
    }
};

// Populate refund modal
function populateRefundModal(refund) {
    document.getElementById('modalRefundCode').textContent = refund.refundCode || `RF-${refund.id.substring(0, 8).toUpperCase()}`;
    document.getElementById('modalRequestDate').textContent = new Date(refund.createdAt).toLocaleDateString('en-US', { 
        year: 'numeric', month: 'long', day: 'numeric', hour: '2-digit', minute: '2-digit' 
    });
    document.getElementById('modalCustomerName').textContent = refund.customerName;
    document.getElementById('modalCustomerEmail').textContent = refund.customerEmail;
    document.getElementById('modalOrderCode').textContent = refund.orderCode || `ORD-${refund.orderId.substring(0, 8).toUpperCase()}`;
    
    document.getElementById('modalOriginalDeposit').textContent = formatCurrency(refund.originalDepositAmount);
    document.getElementById('modalPenaltyAmount').textContent = formatCurrency(refund.totalPenaltyAmount);
    document.getElementById('modalRefundAmount').textContent = formatCurrency(refund.refundAmount);

    // Bank details
    if (refund.bankInfo && refund.bankInfo.bankName) {
        document.getElementById('bankDetailsSection').classList.remove('d-none');
        document.getElementById('noBankInfo').classList.add('d-none');
        document.getElementById('modalBankName').textContent = refund.bankInfo.bankName;
        document.getElementById('modalAccountNumber').textContent = refund.bankInfo.accountNumber || 'N/A';
        document.getElementById('modalAccountHolder').textContent = refund.bankInfo.accountHolderName;
        document.getElementById('modalRoutingNumber').textContent = refund.bankInfo.routingNumber || 'N/A';
    } else {
        document.getElementById('bankDetailsSection').classList.add('d-none');
        document.getElementById('noBankInfo').classList.remove('d-none');
    }

    document.getElementById('modalNotes').textContent = refund.notes || 'No additional notes';

    const statusBadge = document.getElementById('modalStatus');
    statusBadge.textContent = refund.statusDisplay || refund.status;
    statusBadge.className = `status-badge status-${String(refund.status || '').toLowerCase()}`;

    // Show/hide action section
    const actionSection = document.getElementById('adminActionSection');
    const actionButtons = document.getElementById('modalActionButtons');
    if (String(refund.status || '').toLowerCase() === 'initiated') {
        actionSection.classList.remove('d-none');
        actionButtons.classList.remove('d-none');
    } else {
        actionSection.classList.add('d-none');
        actionButtons.classList.add('d-none');
    }
}

// Approve refund
window.approveRefund = async function() {
    if (!selectedRefund) return;

    const notes = document.getElementById('adminNotesInput').value.trim();

    if (!selectedRefund.bankInfo || !selectedRefund.bankInfo.bankName) {
        showNotification('Cannot process refund: Customer has not registered any bank account.', 'error');
        return;
    }

    if (!confirm(`Confirm that you have transferred ${formatCurrency(selectedRefund.refundAmount)} to:\n\nBank: ${selectedRefund.bankInfo.bankName}\nAccount: ${selectedRefund.bankInfo.accountNumber}\nHolder: ${selectedRefund.bankInfo.accountHolderName}`)) {
        return;
    }

    try {
        const bankAccountId = selectedRefund.bankInfo.bankAccountId;
        
        const response = await fetch(`${apiBaseUrl}/api/depositrefunds/process`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${accessToken}`
            },
            body: JSON.stringify({
                refundId: selectedRefund.id,
                isApproved: true,
                bankAccountId: bankAccountId,
                notes: notes || `Refund processed to ${selectedRefund.bankInfo.accountHolderName} - ${selectedRefund.bankInfo.accountNumber}`
            })
        });

        if (!response.ok) {
            throw new Error('Failed to approve refund');
        }

        showNotification('Refund marked as paid successfully!', 'success');
        setTimeout(() => location.reload(), 1500);
    } catch (error) {
        console.error('Error approving refund:', error);
        showNotification('Failed to approve refund', 'error');
    }
};

// Reject refund
window.rejectRefund = async function() {
    if (!selectedRefund) return;

    const notes = document.getElementById('adminNotesInput').value.trim();

    if (!notes) {
        showNotification('Please provide a reason for rejection', 'error');
        return;
    }

    if (!confirm(`Are you sure you want to reject this refund request for ${selectedRefund.customerName}?`)) {
        return;
    }

    try {
        const response = await fetch(`${apiBaseUrl}/api/depositrefunds/process`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${accessToken}`
            },
            body: JSON.stringify({
                refundId: selectedRefund.id,
                isApproved: false,
                bankAccountId: null,
                notes: notes
            })
        });

        if (!response.ok) {
            throw new Error('Failed to reject refund');
        }

        showNotification('Refund rejected successfully', 'success');
        setTimeout(() => location.reload(), 1500);
    } catch (error) {
        console.error('Error rejecting refund:', error);
        showNotification('Failed to reject refund', 'error');
    }
};

// Helper functions
function formatCurrency(amount) {
    return new Intl.NumberFormat('vi-VN', { style: 'decimal' }).format(amount) + ' ₫';
}

function escapeHtml(text) {
    const map = {
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#039;'
    };
    return String(text || '').replace(/[&<>"']/g, m => map[m]);
}

function showNotification(message, type = 'info') {
    const colors = {
        success: 'bg-green-100 text-green-800 border-green-300',
        error: 'bg-red-100 text-red-800 border-red-300',
        info: 'bg-blue-100 text-blue-800 border-blue-300'
    };

    const icons = {
        success: 'fa-check-circle',
        error: 'fa-exclamation-circle',
        info: 'fa-info-circle'
    };

    const notification = document.createElement('div');
    notification.className = `fixed top-4 right-4 z-50 max-w-sm w-full p-4 rounded-lg border ${colors[type] || colors.info} shadow-lg transition-all`;
    notification.innerHTML = `
        <div class="flex items-start gap-3">
            <i class="fas ${icons[type] || icons.info} text-lg mt-0.5"></i>
            <p class="text-sm font-medium flex-1">${message}</p>
            <button onclick="this.closest('div').parentElement.remove()" class="text-gray-600 hover:text-gray-800">
                <i class="fas fa-times"></i>
            </button>
        </div>
    `;

    document.body.appendChild(notification);

    setTimeout(() => {
        notification.remove();
    }, 4000);
}
