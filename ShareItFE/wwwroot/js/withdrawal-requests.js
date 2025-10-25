// Provider Withdrawal Requests Management

let allWithdrawals = [];
let filteredWithdrawals = [];
let currentWithdrawal = null;
let currentPage = 1;
let itemsPerPage = 8;
let currentTab = 'pending'; // 'pending' or 'processed'

// Initialize on page load
document.addEventListener('DOMContentLoaded', function () {
    initializeEventListeners();
    loadWithdrawals();
});

// Event Listeners
function initializeEventListeners() {
    // Search
    const searchInput = document.getElementById('searchInput');
    const clearSearch = document.getElementById('clear-search');

    searchInput.addEventListener('input', function () {
        if (this.value) {
            clearSearch.style.display = 'block';
        } else {
            clearSearch.style.display = 'none';
        }
        applyFilters();
    });

    clearSearch.addEventListener('click', function () {
        searchInput.value = '';
        this.style.display = 'none';
        applyFilters();
    });

    // Filter toggle
    document.getElementById('filterBtn').addEventListener('click', function () {
        const filterPanel = document.getElementById('filterPanel');
        if (filterPanel.style.display === 'none') {
            filterPanel.style.display = 'block';
        } else {
            filterPanel.style.display = 'none';
        }
    });

    // Reset filters
    document.getElementById('reset-filters').addEventListener('click', function () {
        document.getElementById('searchInput').value = '';
        document.getElementById('statusSelect').value = '';
        document.getElementById('paymentMethodSelect').value = '';
        document.getElementById('sortDateSelect').value = '';
        document.getElementById('sortAmountSelect').value = '';
        document.getElementById('clear-search').style.display = 'none';
        applyFilters();
    });

    // Status filter
    document.getElementById('statusSelect').addEventListener('change', applyFilters);
    
    // Payment method filter
    document.getElementById('paymentMethodSelect').addEventListener('change', applyFilters);
    
    // Sort filters - allow combining both
    document.getElementById('sortDateSelect').addEventListener('change', applyFilters);
    document.getElementById('sortAmountSelect').addEventListener('change', applyFilters);

    // Tab switching
    const tabButtons = document.querySelectorAll('.tab-button');
    tabButtons.forEach(button => {
        button.addEventListener('click', function () {
            const targetTab = this.getAttribute('data-tab');
            switchTab(targetTab);
        });
    });
}

// Switch tabs
function switchTab(tab) {
    currentTab = tab;
    currentPage = 1; // Reset to first page when switching tabs
    
    // Update tab buttons
    const tabButtons = document.querySelectorAll('.tab-button');
    tabButtons.forEach(btn => {
        if (btn.getAttribute('data-tab') === tab) {
            btn.classList.add('active');
            btn.setAttribute('aria-selected', 'true');
        } else {
            btn.classList.remove('active');
            btn.setAttribute('aria-selected', 'false');
        }
    });

    // Update tab panes
    const tabPanes = document.querySelectorAll('.tab-pane');
    tabPanes.forEach(pane => {
        if (pane.id === tab + '-pane') {
            pane.classList.add('active');
            pane.style.display = 'block';
        } else {
            pane.classList.remove('active');
            pane.style.display = 'none';
        }
    });
    
    // Re-render tables with pagination
    renderTables();
}

// Load withdrawal requests from API
async function loadWithdrawals() {
    try {
        const response = await fetch('/Admin/WithdrawalRequests?handler=Withdrawals', {
            method: 'GET',
            headers: {
                'Accept': 'application/json'
            }
        });

        const result = await response.json();

        if (result.success) {
            const apiResponse = JSON.parse(result.data);
            allWithdrawals = apiResponse.data || [];
            applyFilters();
        } else {
            showError('Failed to load withdrawal requests: ' + result.message);
            renderEmptyState('pending-tbody', true);
            renderEmptyState('processed-tbody', false);
        }
    } catch (error) {
        console.error('Error loading withdrawals:', error);
        showError('An error occurred while loading withdrawal requests');
        renderEmptyState('pending-tbody', true);
        renderEmptyState('processed-tbody', false);
    }
}

// Apply filters and render tables
function applyFilters() {
    const searchQuery = document.getElementById('searchInput').value.toLowerCase();
    const statusFilter = document.getElementById('statusSelect').value;
    const paymentMethodFilter = document.getElementById('paymentMethodSelect').value;
    const sortDate = document.getElementById('sortDateSelect').value;
    const sortAmount = document.getElementById('sortAmountSelect').value;

    // Filter
    filteredWithdrawals = allWithdrawals.filter(w => {
        const matchesSearch = !searchQuery ||
            w.providerName.toLowerCase().includes(searchQuery) ||
            w.providerEmail?.toLowerCase().includes(searchQuery) ||
            w.id.toLowerCase().includes(searchQuery);

        const matchesStatus = !statusFilter || w.status === statusFilter;
        
        // For now, all withdrawals use bank transfer
        // This can be extended when payment method is added to the API response
        const matchesPaymentMethod = !paymentMethodFilter || 
            (paymentMethodFilter === 'bank_transfer'); // Default to bank transfer

        return matchesSearch && matchesStatus && matchesPaymentMethod;
    });

    // Sort - Can combine both date and amount
    filteredWithdrawals.sort((a, b) => {
        let result = 0;
        
        // Primary sort: by date if selected
        if (sortDate) {
            const dateA = new Date(a.requestDate);
            const dateB = new Date(b.requestDate);
            result = sortDate === 'asc' ? dateA - dateB : dateB - dateA;
            
            // If dates are equal and amount sort is selected, use amount as secondary sort
            if (result === 0 && sortAmount) {
                result = sortAmount === 'asc' ? a.amount - b.amount : b.amount - a.amount;
            }
        }
        // If no date sort, but amount sort is selected
        else if (sortAmount) {
            result = sortAmount === 'asc' ? a.amount - b.amount : b.amount - a.amount;
        }
        // Default: newest first
        else {
            result = new Date(b.requestDate) - new Date(a.requestDate);
        }
        
        return result;
    });

    currentPage = 1; // Reset to first page when filtering
    renderTables();
    updateStats();
}

// Render tables
function renderTables() {
    const pending = filteredWithdrawals.filter(w => w.status === 'Initiated');
    const processed = filteredWithdrawals.filter(w => w.status === 'Completed' || w.status === 'Rejected');

    // Get current tab data
    const currentData = currentTab === 'pending' ? pending : processed;
    
    // Calculate pagination
    const totalPages = Math.ceil(currentData.length / itemsPerPage);
    const startIndex = (currentPage - 1) * itemsPerPage;
    const endIndex = startIndex + itemsPerPage;
    const paginatedData = currentData.slice(startIndex, endIndex);

    // Render tables
    if (currentTab === 'pending') {
        renderTable('pending-tbody', paginatedData, true);
    } else {
        renderTable('processed-tbody', paginatedData, false);
    }

    updateResultsInfo();
    updatePagination(currentData.length, totalPages);
}

// Render table
function renderTable(tbodyId, data, showActions) {
    const tbody = document.getElementById(tbodyId);

    if (!data || data.length === 0) {
        renderEmptyState(tbodyId, showActions);
        return;
    }

    const rows = data.map(w => createTableRow(w, showActions)).join('');
    tbody.innerHTML = rows;
}

// Create table row
function createTableRow(withdrawal, showActions) {
    const statusClass = getStatusClass(withdrawal.status);
    const statusIcon = getStatusIcon(withdrawal.status);
    const statusText = getStatusText(withdrawal.status);

    if (showActions) {
        // Pending requests table
        return `
            <tr>
                <td>
                    <div class="date-display">
                        <i class="fas fa-calendar"></i>
                        <span>${formatDate(withdrawal.requestDate)}</span>
                    </div>
                </td>
                <td>
                    <div class="provider-info">
                        <div class="provider-name">${escapeHtml(withdrawal.providerName)}</div>
                        <div class="provider-email">${escapeHtml(withdrawal.providerEmail || '')}</div>
                    </div>
                </td>
                <td>
                    <span class="amount-value">${formatCurrency(withdrawal.amount)}</span>
                </td>
                <td>
                    <div class="payment-method">
                        <i class="fas fa-university"></i>
                        <span>Bank Transfer</span>
                    </div>
                </td>
                <td>
                    <span class="status-badge ${statusClass}">
                        <i class="${statusIcon}"></i>
                        <span>${statusText}</span>
                    </span>
                </td>
                <td>
                    <div class="action-buttons">
                        <button class="btn-action btn-view" onclick="viewWithdrawal('${withdrawal.id}')" title="View Details">
                            <i class="fas fa-eye"></i>
                        </button>
                        <button class="btn-action btn-approve" onclick="quickApprove('${withdrawal.id}')" title="Mark as Paid">
                            <i class="fas fa-check-circle"></i>
                        </button>
                        <button class="btn-action btn-reject" onclick="quickReject('${withdrawal.id}')" title="Reject">
                            <i class="fas fa-times-circle"></i>
                        </button>
                    </div>
                </td>
            </tr>
        `;
    } else {
        // Processed requests table
        return `
            <tr>
                <td>
                    <div class="date-display">
                        <i class="fas fa-calendar"></i>
                        <span>${formatDate(withdrawal.requestDate)}</span>
                    </div>
                </td>
                <td>
                    <div class="provider-info">
                        <div class="provider-name">${escapeHtml(withdrawal.providerName)}</div>
                        <div class="provider-email">${escapeHtml(withdrawal.providerEmail || '')}</div>
                    </div>
                </td>
                <td>
                    <span class="amount-value">${formatCurrency(withdrawal.amount)}</span>
                </td>
                <td>
                    <span class="status-badge ${statusClass}">
                        <i class="${statusIcon}"></i>
                        <span>${statusText}</span>
                    </span>
                </td>
                <td>
                    <div class="date-display">
                        ${withdrawal.processedAt ? `<i class="fas fa-check"></i><span>${formatDate(withdrawal.processedAt)}</span>` : '-'}
                    </div>
                </td>
                <td>
                    <div class="action-buttons">
                        <button class="btn-action btn-view" onclick="viewWithdrawal('${withdrawal.id}')" title="View Details">
                            <i class="fas fa-eye"></i>
                        </button>
                    </div>
                </td>
            </tr>
        `;
    }
}

// View withdrawal details
async function viewWithdrawal(id) {
    try {
        const response = await fetch(`/Admin/WithdrawalRequests?handler=WithdrawalById&id=${id}`, {
            method: 'GET',
            headers: {
                'Accept': 'application/json'
            }
        });

        const result = await response.json();

        if (result.success) {
            const apiResponse = JSON.parse(result.data);
            currentWithdrawal = apiResponse.data;
            showWithdrawalModal(currentWithdrawal);
        } else {
            showError('Failed to load withdrawal details');
        }
    } catch (error) {
        console.error('Error loading withdrawal:', error);
        showError('An error occurred while loading withdrawal details');
    }
}

// Show withdrawal modal
function showWithdrawalModal(withdrawal) {
    // Populate modal fields
    document.getElementById('modalRequestId').textContent = formatId(withdrawal.id);
    document.getElementById('modalRequestDate').textContent = formatDate(withdrawal.requestDate);
    document.getElementById('modalProviderName').textContent = withdrawal.providerName;
    document.getElementById('modalProviderEmail').textContent = withdrawal.providerEmail || '-';
    document.getElementById('modalAmount').textContent = formatCurrency(withdrawal.amount);

    // Bank details
    document.getElementById('modalBankName').textContent = withdrawal.bankName;
    document.getElementById('modalAccountHolder').textContent = withdrawal.accountHolderName;
    document.getElementById('modalAccountNumber').textContent = withdrawal.accountNumber;
    document.getElementById('modalRoutingNumber').textContent = withdrawal.routingNumber || '-';

    // Provider notes
    if (withdrawal.notes) {
        document.getElementById('providerNotesSection').style.display = 'block';
        document.getElementById('modalProviderNotes').textContent = withdrawal.notes;
    } else {
        document.getElementById('providerNotesSection').style.display = 'none';
    }

    // Status
    const statusBadge = document.getElementById('modalStatus');
    const statusClass = getStatusClass(withdrawal.status);
    const statusIcon = getStatusIcon(withdrawal.status);
    const statusText = getStatusText(withdrawal.status);
    statusBadge.className = 'status-badge-large ' + statusClass;
    statusBadge.innerHTML = `<i class="${statusIcon}"></i><span>${statusText}</span>`;

    // Show/hide sections based on status
    if (withdrawal.status === 'Initiated') {
        document.getElementById('processedSection').style.display = 'none';
        document.getElementById('actionButtonsSection').style.display = 'flex';
    } else {
        document.getElementById('processedSection').style.display = 'block';
        document.getElementById('actionButtonsSection').style.display = 'none';

        document.getElementById('modalProcessedDate').textContent = withdrawal.processedAt ? formatDate(withdrawal.processedAt) : '-';
        document.getElementById('modalProcessedBy').textContent = withdrawal.processedByAdminName || '-';

        if (withdrawal.status === 'Completed') {
            document.getElementById('adminNotesSection').style.display = withdrawal.adminNotes ? 'block' : 'none';
            document.getElementById('modalAdminNotes').textContent = withdrawal.adminNotes || '';
            document.getElementById('transactionIdSection').style.display = withdrawal.externalTransactionId ? 'block' : 'none';
            document.getElementById('modalTransactionId').textContent = withdrawal.externalTransactionId || '';
            document.getElementById('rejectionSection').style.display = 'none';
        } else if (withdrawal.status === 'Rejected') {
            document.getElementById('rejectionSection').style.display = 'block';
            document.getElementById('modalRejectionReason').textContent = withdrawal.rejectionReason || '';
            document.getElementById('adminNotesSection').style.display = 'none';
            document.getElementById('transactionIdSection').style.display = 'none';
        }
    }

    // Show modal
    const modal = new bootstrap.Modal(document.getElementById('withdrawalDetailModal'));
    modal.show();
}

// Quick approve
function quickApprove(id) {
    const withdrawal = allWithdrawals.find(w => w.id === id);
    if (withdrawal) {
        currentWithdrawal = withdrawal;
        showApproveModal();
    }
}

// Quick reject
function quickReject(id) {
    const withdrawal = allWithdrawals.find(w => w.id === id);
    if (withdrawal) {
        currentWithdrawal = withdrawal;
        showRejectModal();
    }
}

// Show approve modal
function showApproveModal() {
    if (!currentWithdrawal) return;

    // Populate approve modal
    document.getElementById('approveAmount').textContent = formatCurrency(currentWithdrawal.amount);
    document.getElementById('approveBankInfo').textContent = `${currentWithdrawal.bankName} - ${currentWithdrawal.accountNumber}`;

    // Clear previous inputs
    document.getElementById('approveTransactionId').value = '';
    document.getElementById('approveNotes').value = '';

    // Hide withdrawal detail modal if open
    const detailModal = bootstrap.Modal.getInstance(document.getElementById('withdrawalDetailModal'));
    if (detailModal) {
        detailModal.hide();
    }

    // Show approve modal
    const modal = new bootstrap.Modal(document.getElementById('approveModal'));
    modal.show();
}

// Show reject modal
function showRejectModal() {
    if (!currentWithdrawal) return;

    // Populate reject modal
    document.getElementById('rejectProviderName').textContent = currentWithdrawal.providerName;
    document.getElementById('rejectAmount').textContent = formatCurrency(currentWithdrawal.amount);

    // Clear previous input
    document.getElementById('rejectReason').value = '';

    // Hide withdrawal detail modal if open
    const detailModal = bootstrap.Modal.getInstance(document.getElementById('withdrawalDetailModal'));
    if (detailModal) {
        detailModal.hide();
    }

    // Show reject modal
    const modal = new bootstrap.Modal(document.getElementById('rejectModal'));
    modal.show();
}

// Process approval
async function processApproval() {
    if (!currentWithdrawal) return;

    const transactionId = document.getElementById('approveTransactionId').value.trim();
    const adminNotes = document.getElementById('approveNotes').value.trim();

    try {
        const response = await fetch('/Admin/WithdrawalRequests?handler=ProcessWithdrawal', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify({
                withdrawalRequestId: currentWithdrawal.id,
                status: 'Completed',
                externalTransactionId: transactionId || null,
                adminNotes: adminNotes || null
            })
        });

        const result = await response.json();

        if (result.success) {
            showSuccess('Withdrawal request approved successfully');
            
            // Hide modal
            const modal = bootstrap.Modal.getInstance(document.getElementById('approveModal'));
            modal.hide();

            // Reload data
            await loadWithdrawals();
        } else {
            showError(result.message || 'Failed to approve withdrawal');
        }
    } catch (error) {
        console.error('Error approving withdrawal:', error);
        showError('An error occurred while approving the withdrawal');
    }
}

// Process rejection
async function processRejection() {
    if (!currentWithdrawal) return;

    const rejectionReason = document.getElementById('rejectReason').value.trim();

    if (!rejectionReason) {
        showError('Please provide a rejection reason');
        return;
    }

    try {
        const response = await fetch('/Admin/WithdrawalRequests?handler=ProcessWithdrawal', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify({
                withdrawalRequestId: currentWithdrawal.id,
                status: 'Rejected',
                rejectionReason: rejectionReason
            })
        });

        const result = await response.json();

        if (result.success) {
            showSuccess('Withdrawal request rejected');
            
            // Hide modal
            const modal = bootstrap.Modal.getInstance(document.getElementById('rejectModal'));
            modal.hide();

            // Reload data
            await loadWithdrawals();
        } else {
            showError(result.message || 'Failed to reject withdrawal');
        }
    } catch (error) {
        console.error('Error rejecting withdrawal:', error);
        showError('An error occurred while rejecting the withdrawal');
    }
}

// Update stats
function updateStats() {
    const pendingCount = filteredWithdrawals.filter(w => w.status === 'Initiated').length;
    const processedCount = filteredWithdrawals.filter(w => w.status === 'Completed' || w.status === 'Rejected').length;

    // Update header badge
    const headerBadge = document.getElementById('header-pending-badge');
    const headerCount = document.getElementById('header-pending-count');
    if (pendingCount > 0) {
        headerBadge.style.display = 'flex';
        headerCount.textContent = pendingCount;
    } else {
        headerBadge.style.display = 'none';
    }

    // Update tab badges
    document.getElementById('pending-count-badge').textContent = pendingCount;
    document.getElementById('processed-count-badge').textContent = processedCount;
}

// Update results info
function updateResultsInfo() {
    const pending = filteredWithdrawals.filter(w => w.status === 'Initiated');
    const processed = filteredWithdrawals.filter(w => w.status === 'Completed' || w.status === 'Rejected');
    const currentData = currentTab === 'pending' ? pending : processed;
    const totalResults = currentData.length;
    const resultsInfo = document.getElementById('results-info');
    
    if (totalResults === 0) {
        resultsInfo.textContent = 'No withdrawal requests found';
    } else {
        const startIndex = (currentPage - 1) * itemsPerPage;
        const endIndex = Math.min(startIndex + itemsPerPage, totalResults);
        resultsInfo.textContent = `Showing ${startIndex + 1} to ${endIndex} of ${totalResults} withdrawal request${totalResults !== 1 ? 's' : ''}`;
    }
}

// Update pagination
function updatePagination(totalItems, totalPages) {
    const paginationWrapper = document.getElementById('paginationWrapper');
    const prevBtn = document.getElementById('prevBtn');
    const nextBtn = document.getElementById('nextBtn');
    const paginationPages = document.getElementById('paginationPages');
    
    // Show/hide pagination
    if (totalItems <= itemsPerPage) {
        paginationWrapper.style.display = 'none';
        return;
    }
    paginationWrapper.style.display = 'flex';
    
    // Update showing info
    const startIndex = (currentPage - 1) * itemsPerPage;
    const endIndex = Math.min(startIndex + itemsPerPage, totalItems);
    document.getElementById('showingFrom').textContent = startIndex + 1;
    document.getElementById('showingTo').textContent = endIndex;
    document.getElementById('totalItems').textContent = totalItems;
    
    // Update prev/next buttons
    prevBtn.disabled = currentPage === 1;
    nextBtn.disabled = currentPage === totalPages;
    
    // Render page numbers
    renderPageNumbers(totalPages);
}

// Render page numbers
function renderPageNumbers(totalPages) {
    const paginationPages = document.getElementById('paginationPages');
    paginationPages.innerHTML = '';
    
    if (totalPages <= 1) return;
    
    const maxPagesToShow = 5;
    let startPage = Math.max(1, currentPage - Math.floor(maxPagesToShow / 2));
    let endPage = Math.min(totalPages, startPage + maxPagesToShow - 1);
    
    if (endPage - startPage + 1 < maxPagesToShow) {
        startPage = Math.max(1, endPage - maxPagesToShow + 1);
    }
    
    // First page
    if (startPage > 1) {
        paginationPages.appendChild(createPageButton(1));
        if (startPage > 2) {
            paginationPages.appendChild(createEllipsis());
        }
    }
    
    // Page numbers
    for (let i = startPage; i <= endPage; i++) {
        paginationPages.appendChild(createPageButton(i));
    }
    
    // Last page
    if (endPage < totalPages) {
        if (endPage < totalPages - 1) {
            paginationPages.appendChild(createEllipsis());
        }
        paginationPages.appendChild(createPageButton(totalPages));
    }
}

// Create page button
function createPageButton(pageNum) {
    const button = document.createElement('div');
    button.className = 'pagination-page' + (pageNum === currentPage ? ' active' : '');
    button.textContent = pageNum;
    button.onclick = () => goToPage(pageNum);
    return button;
}

// Create ellipsis
function createEllipsis() {
    const ellipsis = document.createElement('div');
    ellipsis.className = 'pagination-page ellipsis';
    ellipsis.textContent = '...';
    return ellipsis;
}

// Change page
function changePage(delta) {
    const pending = filteredWithdrawals.filter(w => w.status === 'Initiated');
    const processed = filteredWithdrawals.filter(w => w.status === 'Completed' || w.status === 'Rejected');
    const currentData = currentTab === 'pending' ? pending : processed;
    const totalPages = Math.ceil(currentData.length / itemsPerPage);
    
    const newPage = currentPage + delta;
    if (newPage >= 1 && newPage <= totalPages) {
        currentPage = newPage;
        renderTables();
    }
}

// Go to specific page
function goToPage(pageNum) {
    currentPage = pageNum;
    renderTables();
}

// Change items per page
function changeItemsPerPage() {
    const select = document.getElementById('itemsPerPage');
    itemsPerPage = parseInt(select.value);
    currentPage = 1; // Reset to first page
    renderTables();
}

// Render empty state
function renderEmptyState(tbodyId, isPending) {
    const tbody = document.getElementById(tbodyId);
    const colSpan = isPending ? 6 : 6;
    
    tbody.innerHTML = `
        <tr>
            <td colspan="${colSpan}">
                <div class="empty-state">
                    <i class="fas fa-wallet"></i>
                    <h3>No Withdrawal Requests</h3>
                    <p>${isPending ? 'No pending withdrawal requests at this time' : 'No processed withdrawal requests found'}</p>
                </div>
            </td>
        </tr>
    `;
}

// Utility Functions
function formatCurrency(amount) {
    return new Intl.NumberFormat('vi-VN', {
        style: 'currency',
        currency: 'VND'
    }).format(amount);
}

function formatDate(dateString) {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return new Intl.DateTimeFormat('en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    }).format(date);
}

function formatId(id) {
    if (!id) return '-';
    return id.substring(0, 8).toUpperCase();
}

function maskAccountNumber(accountNumber) {
    if (!accountNumber || accountNumber.length < 4) return accountNumber;
    return '****' + accountNumber.substring(accountNumber.length - 4);
}

function getStatusClass(status) {
    switch (status) {
        case 'Initiated':
            return 'status-pending';
        case 'Completed':
            return 'status-completed';
        case 'Rejected':
            return 'status-rejected';
        default:
            return '';
    }
}

function getStatusIcon(status) {
    switch (status) {
        case 'Initiated':
            return 'fas fa-clock';
        case 'Completed':
            return 'fas fa-check-circle';
        case 'Rejected':
            return 'fas fa-times-circle';
        default:
            return 'fas fa-question-circle';
    }
}

function getStatusText(status) {
    switch (status) {
        case 'Initiated':
            return 'Initiated';
        case 'Completed':
            return 'Processed';
        case 'Rejected':
            return 'Rejected';
        default:
            return status;
    }
}

function escapeHtml(text) {
    if (!text) return '';
    const map = {
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#039;'
    };
    return text.replace(/[&<>"']/g, m => map[m]);
}

function showSuccess(message) {
    // Using toastManager if available
    if (window.toastManager) {
        window.toastManager.success(message);
    } else if (window.showSuccessToast) {
        window.showSuccessToast(message);
    } else {
        alert(message);
    }
}

function showError(message) {
    // Using toastManager if available
    if (window.toastManager) {
        window.toastManager.error(message);
    } else if (window.showErrorToast) {
        window.showErrorToast(message);
    } else {
        alert('Error: ' + message);
    }
}
