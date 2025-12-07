// Customer Deposit Refunds Management

let allRefunds = [];
let filteredRefunds = [];
let currentRefund = null;
let currentPage = 1;
let itemsPerPage = 8;
let currentTab = 'pending'; // 'pending' or 'processed'

// Initialize on page load
document.addEventListener('DOMContentLoaded', function () {
    initializeEventListeners();
    loadRefunds();
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
        document.getElementById('sortDateSelect').value = '';
        document.getElementById('sortAmountSelect').value = '';
        document.getElementById('clear-search').style.display = 'none';
            applyFilters();
        });

    // Status filter
    document.getElementById('statusSelect').addEventListener('change', applyFilters);
    
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

// Load refund requests from API
async function loadRefunds() {
    try {
        const response = await fetch('/Admin/CustomerRefunds?handler=Refunds', {
            method: 'GET',
            headers: {
                'Accept': 'application/json'
            }
        });

        const result = await response.json();

        if (result.success) {
            const apiResponse = JSON.parse(result.data);
            allRefunds = apiResponse.data || [];
                applyFilters();
        } else {
            showError('Failed to load refund requests: ' + result.message);
            renderEmptyState('pending-tbody', true);
            renderEmptyState('processed-tbody', false);
        }
    } catch (error) {
        console.error('Error loading refunds:', error);
        showError('An error occurred while loading refund requests');
        renderEmptyState('pending-tbody', true);
        renderEmptyState('processed-tbody', false);
    }
}

// Apply filters and render tables
function applyFilters() {
    const searchQuery = document.getElementById('searchInput').value.toLowerCase();
    const statusFilter = document.getElementById('statusSelect').value;
    const sortDate = document.getElementById('sortDateSelect').value;
    const sortAmount = document.getElementById('sortAmountSelect').value;

    // Filter
    filteredRefunds = allRefunds.filter(r => {
        const matchesSearch = !searchQuery ||
            r.customerName.toLowerCase().includes(searchQuery) ||
            r.customerEmail?.toLowerCase().includes(searchQuery) ||
            r.orderCode?.toLowerCase().includes(searchQuery) ||
            r.refundCode?.toLowerCase().includes(searchQuery);

        const matchesStatus = !statusFilter || r.statusDisplay.toLowerCase() === statusFilter.toLowerCase();

        return matchesSearch && matchesStatus;
    });

    // Sort - Can combine both date and amount
    filteredRefunds.sort((a, b) => {
        let result = 0;
        
        // Primary sort: by date if selected
        if (sortDate) {
            const dateA = new Date(a.createdAt);
            const dateB = new Date(b.createdAt);
            result = sortDate === 'asc' ? dateA - dateB : dateB - dateA;
            
            // If dates are equal and amount sort is selected, use amount as secondary sort
            if (result === 0 && sortAmount) {
                result = sortAmount === 'asc' ? a.refundAmount - b.refundAmount : b.refundAmount - a.refundAmount;
            }
        }
        // If no date sort, but amount sort is selected
        else if (sortAmount) {
            result = sortAmount === 'asc' ? a.refundAmount - b.refundAmount : b.refundAmount - a.refundAmount;
        }
        // Default: newest first
        else {
            result = new Date(b.createdAt) - new Date(a.createdAt);
        }
        
        return result;
    });

    currentPage = 1; // Reset to first page when filtering
    renderTables();
    updateStats();
}

// Render tables
function renderTables() {
    const pending = filteredRefunds.filter(r => r.statusDisplay.toLowerCase() === 'initiated');
    const processed = filteredRefunds.filter(r => r.statusDisplay.toLowerCase() === 'completed' || r.statusDisplay.toLowerCase() === 'failed');

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

    const rows = data.map(r => createTableRow(r, showActions)).join('');
    tbody.innerHTML = rows;
}

// Create table row
function createTableRow(refund, showActions) {
    const statusClass = getStatusClass(refund.statusDisplay);
    const statusIcon = getStatusIcon(refund.statusDisplay);
    const statusText = getStatusText(refund.statusDisplay);

    if (showActions) {
        // Pending requests table
        return `
            <tr>
                <td>
                    <div class="date-display">
                        <i class="fas fa-calendar"></i>
                        <span>${formatDate(refund.createdAt)}</span>
                    </div>
                </td>
                <td>
                    <div class="provider-info">
                        <div class="provider-name">${escapeHtml(refund.customerName)}</div>
                        <div class="provider-email">${escapeHtml(refund.customerEmail || '')}</div>
                    </div>
                </td>
                <td>
                    <span class="fw-semibold">${escapeHtml(refund.orderCode)}</span>
                </td>
                <td>
                    <span class="amount-value">${formatCurrency(refund.refundAmount)}</span>
                </td>
                <td>
                    <span class="status-badge ${statusClass}">
                        <i class="${statusIcon}"></i>
                        <span>${statusText}</span>
                    </span>
                </td>
                <td>
                    <div class="action-buttons">
                        <button class="btn-action btn-view" onclick="viewRefund('${refund.id}')" title="View Details">
                            <i class="fas fa-eye"></i>
                        </button>
                        <button class="btn-action btn-approve" onclick="quickApprove('${refund.id}')" title="Mark as Paid">
                            <i class="fas fa-check-circle"></i>
                        </button>
                        <button class="btn-action btn-reject" onclick="quickReject('${refund.id}')" title="Reject">
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
                        <span>${formatDate(refund.createdAt)}</span>
                    </div>
                </td>
                <td>
                    <div class="provider-info">
                        <div class="provider-name">${escapeHtml(refund.customerName)}</div>
                        <div class="provider-email">${escapeHtml(refund.customerEmail || '')}</div>
                    </div>
                </td>
                <td>
                    <span class="fw-semibold">${escapeHtml(refund.orderCode)}</span>
                </td>
                <td>
                    <span class="amount-value">${formatCurrency(refund.refundAmount)}</span>
                </td>
                <td>
                    <span class="status-badge ${statusClass}">
                        <i class="${statusIcon}"></i>
                        <span>${statusText}</span>
                    </span>
                </td>
                <td>
                    <div class="date-display">
                        ${refund.processedAt ? `<i class="fas fa-check"></i><span>${formatDate(refund.processedAt)}</span>` : '-'}
                    </div>
                </td>
                <td>
                    <div class="action-buttons">
                        <button class="btn-action btn-view" onclick="viewRefund('${refund.id}')" title="View Details">
                            <i class="fas fa-eye"></i>
                    </button>
                    </div>
                </td>
            </tr>
        `;
    }
}

// View refund details
async function viewRefund(id) {
    try {
        const response = await fetch(`/Admin/CustomerRefunds?handler=RefundById&id=${id}`, {
            method: 'GET',
            headers: {
                'Accept': 'application/json'
            }
        });

        const result = await response.json();

        if (result.success) {
            const apiResponse = JSON.parse(result.data);
            currentRefund = apiResponse.data;
            showRefundModal(currentRefund);
        } else {
            showError('Failed to load refund details');
        }
    } catch (error) {
        console.error('Error loading refund:', error);
        showError('An error occurred while loading refund details');
    }
}

// Show refund modal
function showRefundModal(refund) {
    // Populate modal fields
    document.getElementById('modalRefundCode').textContent = refund.refundCode;
    document.getElementById('modalRequestDate').textContent = formatDate(refund.createdAt);
    document.getElementById('modalCustomerName').textContent = refund.customerName;
    document.getElementById('modalCustomerEmail').textContent = refund.customerEmail || '-';
    document.getElementById('modalOrderCode').textContent = refund.orderCode;
    
    // Amount details
    document.getElementById('modalOriginalDeposit').textContent = formatCurrency(refund.originalDepositAmount);
    document.getElementById('modalPenaltyAmount').textContent = formatCurrency(refund.totalPenaltyAmount);
    document.getElementById('modalRefundAmount').textContent = formatCurrency(refund.refundAmount);

    // Bank details
    if (refund.bankInfo) {
        document.getElementById('bankDetailsSection').style.display = 'block';
        document.getElementById('noBankInfo').classList.add('d-none');
        document.getElementById('modalBankName').textContent = refund.bankInfo.bankName;
        document.getElementById('modalAccountHolder').textContent = refund.bankInfo.accountHolderName;
        document.getElementById('modalAccountNumber').textContent = refund.bankInfo.accountNumber;
        document.getElementById('modalRoutingNumber').textContent = refund.bankInfo.routingNumber || '-';
    } else {
        document.getElementById('bankDetailsSection').style.display = 'none';
        document.getElementById('noBankInfo').classList.remove('d-none');
    }

    // Notes
    if (refund.notes) {
        document.getElementById('notesSection').style.display = 'block';
        document.getElementById('modalNotes').textContent = refund.notes;
    } else {
        document.getElementById('notesSection').style.display = 'none';
    }

    // Status
    const statusBadge = document.getElementById('modalStatus');
    const statusClass = getStatusClass(refund.statusDisplay);
    const statusIcon = getStatusIcon(refund.statusDisplay);
    const statusText = getStatusText(refund.statusDisplay);
    statusBadge.className = 'status-badge-large ' + statusClass;
    statusBadge.innerHTML = `<i class="${statusIcon}"></i><span>${statusText}</span>`;

    // Show/hide sections based on status
    if (refund.statusDisplay.toLowerCase() === 'initiated') {
        document.getElementById('processedSection').style.display = 'none';
        document.getElementById('actionButtonsSection').style.display = 'flex';
        document.getElementById('reopenButtonSection').style.display = 'none';
    } else {
        document.getElementById('processedSection').style.display = 'block';
        document.getElementById('actionButtonsSection').style.display = 'none';

        document.getElementById('modalProcessedDate').textContent = refund.processedAt ? formatDate(refund.processedAt) : '-';
        document.getElementById('modalProcessedBy').textContent = refund.processedByAdminName || '-';

        // Show reopen button only for rejected refunds
        if (refund.statusDisplay.toLowerCase() === 'failed') {
            document.getElementById('reopenButtonSection').style.display = 'block';
        } else {
            document.getElementById('reopenButtonSection').style.display = 'none';
        }

        // Show transaction ID if completed
        if (refund.statusDisplay.toLowerCase() === 'completed' && refund.externalTransactionId) {
            document.getElementById('transactionIdSection').style.display = 'block';
            document.getElementById('modalTransactionId').textContent = refund.externalTransactionId;
        } else {
            document.getElementById('transactionIdSection').style.display = 'none';
        }
    }

    // Show modal
    const modal = new bootstrap.Modal(document.getElementById('refundDetailModal'));
    modal.show();
}

// Quick approve
function quickApprove(id) {
    const refund = allRefunds.find(r => r.id === id);
    if (refund) {
        currentRefund = refund;
        showApproveModal();
    }
}

// Quick reject
function quickReject(id) {
    const refund = allRefunds.find(r => r.id === id);
    if (refund) {
        currentRefund = refund;
        showRejectModal();
    }
}

// Show approve modal
function showApproveModal() {
    if (!currentRefund) return;

    // Populate approve modal
    document.getElementById('approveAmount').textContent = formatCurrency(currentRefund.refundAmount);
    
    let bankInfo = '-';
    if (currentRefund.customerBankName && currentRefund.customerAccountNumber) {
        bankInfo = `${currentRefund.customerBankName} - ${currentRefund.customerAccountNumber}`;
    }
    document.getElementById('approveBankInfo').textContent = bankInfo;

    // Clear previous inputs
    document.getElementById('approveTransactionId').value = '';
    document.getElementById('approveNotes').value = '';

    // Hide refund detail modal if open
    const detailModal = bootstrap.Modal.getInstance(document.getElementById('refundDetailModal'));
    if (detailModal) {
        detailModal.hide();
    }

    // Show approve modal
    const modal = new bootstrap.Modal(document.getElementById('approveModal'));
    modal.show();
}

// Show reject modal
function showRejectModal() {
    if (!currentRefund) return;

    // Populate reject modal
    document.getElementById('rejectCustomerName').textContent = currentRefund.customerName;
    document.getElementById('rejectAmount').textContent = formatCurrency(currentRefund.refundAmount);

    // Clear previous input
    document.getElementById('rejectReason').value = '';

    // Hide refund detail modal if open
    const detailModal = bootstrap.Modal.getInstance(document.getElementById('refundDetailModal'));
    if (detailModal) {
        detailModal.hide();
    }

    // Show reject modal
    const modal = new bootstrap.Modal(document.getElementById('rejectModal'));
    modal.show();
}

// Process approval
async function processApproval() {
    if (!currentRefund) return;

    const transactionId = document.getElementById('approveTransactionId').value.trim();
    const adminNotes = document.getElementById('approveNotes').value.trim();

    try {
        const response = await fetch('/Admin/CustomerRefunds?handler=ProcessRefund', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify({
                refundId: currentRefund.id,
                isApproved: true,
                bankAccountId: currentRefund.refundBankAccountId,
                notes: adminNotes || null,
                externalTransactionId: transactionId || null
            })
        });

        const result = await response.json();

        if (result.success) {
            showSuccess('Refund request approved successfully');
            
            // Hide modal
            const modal = bootstrap.Modal.getInstance(document.getElementById('approveModal'));
            modal.hide();

            // Reload data
            await loadRefunds();
        } else {
            showError(result.message || 'Failed to approve refund');
        }
    } catch (error) {
        console.error('Error approving refund:', error);
        showError('An error occurred while approving the refund');
    }
}

// Process rejection
async function processRejection() {
    if (!currentRefund) return;

    const rejectionReason = document.getElementById('rejectReason').value.trim();

    if (!rejectionReason) {
        showError('Please provide a rejection reason');
        return;
    }

    try {
        const response = await fetch('/Admin/CustomerRefunds?handler=ProcessRefund', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify({
                refundId: currentRefund.id,
                isApproved: false,
                notes: rejectionReason
            })
        });

        const result = await response.json();

        if (result.success) {
            showSuccess('Refund request rejected');
            
            // Hide modal
            const modal = bootstrap.Modal.getInstance(document.getElementById('rejectModal'));
            modal.hide();

            // Reload data
            await loadRefunds();
        } else {
            showError(result.message || 'Failed to reject refund');
        }
    } catch (error) {
        console.error('Error rejecting refund:', error);
        showError('An error occurred while rejecting the refund');
    }
}

// Reopen refund
async function reopenRefund() {
    if (!currentRefund) return;

    if (!confirm('Are you sure you want to reopen this refund request? It will move back to pending status.')) {
        return;
    }

    try {
        const response = await fetch(`/Admin/CustomerRefunds?handler=ReopenRefund&refundId=${currentRefund.id}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            }
        });

        const result = await response.json();

        if (result.success) {
            showSuccess('Refund request reopened successfully');
            
            // Hide modal
            const modal = bootstrap.Modal.getInstance(document.getElementById('refundDetailModal'));
            if (modal) {
                modal.hide();
            }

            // Reload data
            await loadRefunds();
        } else {
            showError(result.message || 'Failed to reopen refund');
        }
    } catch (error) {
        console.error('Error reopening refund:', error);
        showError('An error occurred while reopening the refund');
    }
}

// Update stats
function updateStats() {
    const pendingCount = filteredRefunds.filter(r => r.statusDisplay.toLowerCase() === 'initiated').length;
    const processedCount = filteredRefunds.filter(r => r.statusDisplay.toLowerCase() === 'completed' || r.statusDisplay.toLowerCase() === 'failed').length;

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
    const pending = filteredRefunds.filter(r => r.statusDisplay.toLowerCase() === 'initiated');
    const processed = filteredRefunds.filter(r => r.statusDisplay.toLowerCase() === 'completed' || r.statusDisplay.toLowerCase() === 'failed');
    const currentData = currentTab === 'pending' ? pending : processed;
    const totalResults = currentData.length;
    const resultsInfo = document.getElementById('results-info');
    
    if (totalResults === 0) {
        resultsInfo.textContent = 'No refund requests found';
    } else {
        const startIndex = (currentPage - 1) * itemsPerPage;
        const endIndex = Math.min(startIndex + itemsPerPage, totalResults);
        resultsInfo.textContent = `Showing ${startIndex + 1} to ${endIndex} of ${totalResults} refund request${totalResults !== 1 ? 's' : ''}`;
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
    const pending = filteredRefunds.filter(r => r.statusDisplay.toLowerCase() === 'initiated');
    const processed = filteredRefunds.filter(r => r.statusDisplay.toLowerCase() === 'completed' || r.statusDisplay.toLowerCase() === 'failed');
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
    const colSpan = isPending ? 6 : 7;
    
    tbody.innerHTML = `
        <tr>
            <td colspan="${colSpan}">
                <div class="empty-state">
                    <i class="fas fa-hand-holding-usd"></i>
                    <h3>No Refund Requests</h3>
                    <p>${isPending ? 'No pending refund requests at this time' : 'No processed refund requests found'}</p>
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
        minute: '2-digit',
        timeZone: 'Asia/Ho_Chi_Minh'
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
    switch (status.toLowerCase()) {
        case 'initiated':
            return 'status-pending';
        case 'completed':
            return 'status-completed';
        case 'failed':
            return 'status-rejected';
        default:
            return '';
    }
}

function getStatusIcon(status) {
    switch (status.toLowerCase()) {
        case 'initiated':
            return 'fas fa-clock';
        case 'completed':
            return 'fas fa-check-circle';
        case 'failed':
            return 'fas fa-times-circle';
        default:
            return 'fas fa-question-circle';
    }
}

function getStatusText(status) {
    switch (status.toLowerCase()) {
        case 'initiated':
            return 'Initiated';
        case 'completed':
            return 'Completed';
        case 'failed':
            return 'Failed';
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
