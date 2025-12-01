// Admin Dashboard JavaScript

// Format order status text for display
function formatOrderStatus(status) {
    const statusMap = {
        'approved': 'Paid',
        'Approved': 'Paid',
        'pending': 'Pending',
        'Pending': 'Pending',
        'in_transit': 'In Transit',
        'In_transit': 'In Transit',
        'in_use': 'In Use',
        'In_use': 'In Use',
        'returning': 'Returning',
        'Returning': 'Returning',
        'returned': 'Returned',
        'Returned': 'Returned',
        'cancelled': 'Cancelled',
        'Cancelled': 'Cancelled',
        'returned_with_issue': 'Returned with Issue',
        'Returned_with_issue': 'Returned with Issue'
    };
    return statusMap[status] || status.replace('_', ' ');
}

// Chart.js configuration
Chart.defaults.font.family = "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif";
Chart.defaults.color = '#718096';

// Revenue Over Time Chart
const revenueChartCtx = document.getElementById('revenueChart');
if (revenueChartCtx) {
    const labels = revenueData.map(d => {
        const date = new Date(d.date);
        return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    });
    const revenues = revenueData.map(d => d.revenue);
    const orders = revenueData.map(d => d.orders);

    new Chart(revenueChartCtx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [
                {
                    label: 'Revenue (₫)',
                    data: revenues,
                    borderColor: '#7c3aed',
                    backgroundColor: 'rgba(124, 58, 237, 0.1)',
                    borderWidth: 2,
                    fill: true,
                    tension: 0.4,
                    pointRadius: 4,
                    pointHoverRadius: 6,
                    pointBackgroundColor: '#7c3aed',
                    pointBorderColor: '#fff',
                    pointBorderWidth: 2,
                    yAxisID: 'y'
                },
                {
                    label: 'Orders',
                    data: orders,
                    borderColor: '#4299e1',
                    backgroundColor: 'rgba(66, 153, 225, 0.1)',
                    borderWidth: 2,
                    fill: true,
                    tension: 0.4,
                    pointRadius: 4,
                    pointHoverRadius: 6,
                    pointBackgroundColor: '#4299e1',
                    pointBorderColor: '#fff',
                    pointBorderWidth: 2,
                    yAxisID: 'y1'
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: {
                mode: 'index',
                intersect: false,
            },
            plugins: {
                legend: {
                    display: true,
                    position: 'top',
                    align: 'end',
                    labels: {
                        usePointStyle: true,
                        padding: 15,
                        font: {
                            size: 12,
                            weight: '500'
                        }
                    }
                },
                tooltip: {
                    backgroundColor: 'rgba(26, 32, 44, 0.95)',
                    padding: 12,
                    cornerRadius: 8,
                    titleFont: {
                        size: 13,
                        weight: '600'
                    },
                    bodyFont: {
                        size: 12
                    },
                    callbacks: {
                        label: function(context) {
                            let label = context.dataset.label || '';
                            if (label) {
                                label += ': ';
                            }
                            if (context.datasetIndex === 0) {
                                label += context.parsed.y.toLocaleString('vi-VN') + '₫';
                            } else {
                                label += context.parsed.y.toLocaleString();
                            }
                            return label;
                        }
                    }
                }
            },
            scales: {
                x: {
                    grid: {
                        display: false
                    },
                    ticks: {
                        font: {
                            size: 11
                        }
                    }
                },
                y: {
                    type: 'linear',
                    display: true,
                    position: 'left',
                    grid: {
                        color: 'rgba(0, 0, 0, 0.05)',
                        drawBorder: false
                    },
                    ticks: {
                        font: {
                            size: 11
                        },
                        callback: function(value) {
                            return value.toLocaleString('vi-VN') + '₫';
                        }
                    },
                    title: {
                        display: true,
                        text: 'Revenue (₫)',
                        font: {
                            size: 12,
                            weight: '600'
                        },
                        color: '#7c3aed'
                    }
                },
                y1: {
                    type: 'linear',
                    display: true,
                    position: 'right',
                    grid: {
                        drawOnChartArea: false
                    },
                    ticks: {
                        font: {
                            size: 11
                        }
                    },
                    title: {
                        display: true,
                        text: 'Orders',
                        font: {
                            size: 12,
                            weight: '600'
                        },
                        color: '#4299e1'
                    }
                }
            }
        }
    });
}

// Payment Method Chart (Doughnut)
const paymentMethodChartCtx = document.getElementById('paymentMethodChart');
if (paymentMethodChartCtx) {
    const total = paymentMethods.vnpay + paymentMethods.sepay;
    
    new Chart(paymentMethodChartCtx, {
        type: 'doughnut',
        data: {
            labels: ['VNPay', 'SEPay'],
            datasets: [{
                data: [
                    paymentMethods.vnpay,
                    paymentMethods.sepay
                ],
                backgroundColor: [
                    '#7c3aed',
                    '#4299e1'
                ],
                borderColor: '#ffffff',
                borderWidth: 3,
                hoverOffset: 10
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: {
                        padding: 15,
                        usePointStyle: true,
                        font: {
                            size: 12,
                            weight: '500'
                        },
                        generateLabels: function(chart) {
                            const data = chart.data;
                            if (data.labels.length && data.datasets.length) {
                                return data.labels.map((label, i) => {
                                    const value = data.datasets[0].data[i];
                                    const percentage = total > 0 ? ((value / total) * 100).toFixed(1) : 0;
                                    return {
                                        text: `${label}: ${value} (${percentage}%)`,
                                        fillStyle: data.datasets[0].backgroundColor[i],
                                        hidden: false,
                                        index: i
                                    };
                                });
                            }
                            return [];
                        }
                    }
                },
                tooltip: {
                    backgroundColor: 'rgba(26, 32, 44, 0.95)',
                    padding: 12,
                    cornerRadius: 8,
                    titleFont: {
                        size: 13,
                        weight: '600'
                    },
                    bodyFont: {
                        size: 12
                    },
                    callbacks: {
                        label: function(context) {
                            const label = context.label || '';
                            const value = context.parsed || 0;
                            const percentage = total > 0 ? ((value / total) * 100).toFixed(1) : 0;
                            return `${label}: ${value} transactions (${percentage}%)`;
                        }
                    }
                }
            }
        }
    });
}

// Transaction Status Chart (Doughnut)
const transactionStatusChartCtx = document.getElementById('transactionStatusChart');
if (transactionStatusChartCtx) {
    const totalTx = transactionStatus.completed + transactionStatus.failed + transactionStatus.pending + transactionStatus.initiated;
    
    new Chart(transactionStatusChartCtx, {
        type: 'doughnut',
        data: {
            labels: ['Completed', 'Failed', 'Pending', 'Initiated'],
            datasets: [{
                data: [
                    transactionStatus.completed,
                    transactionStatus.failed,
                    transactionStatus.pending,
                    transactionStatus.initiated
                ],
                backgroundColor: [
                    '#48bb78',
                    '#f56565',
                    '#ecc94b',
                    '#a0aec0'
                ],
                borderColor: '#ffffff',
                borderWidth: 3,
                hoverOffset: 10
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: {
                        padding: 15,
                        usePointStyle: true,
                        font: {
                            size: 12,
                            weight: '500'
                        },
                        generateLabels: function(chart) {
                            const data = chart.data;
                            if (data.labels.length && data.datasets.length) {
                                return data.labels.map((label, i) => {
                                    const value = data.datasets[0].data[i];
                                    const percentage = totalTx > 0 ? ((value / totalTx) * 100).toFixed(1) : 0;
                                    return {
                                        text: `${label}: ${value} (${percentage}%)`,
                                        fillStyle: data.datasets[0].backgroundColor[i],
                                        hidden: false,
                                        index: i
                                    };
                                });
                            }
                            return [];
                        }
                    }
                },
                tooltip: {
                    backgroundColor: 'rgba(26, 32, 44, 0.95)',
                    padding: 12,
                    cornerRadius: 8,
                    titleFont: {
                        size: 13,
                        weight: '600'
                    },
                    bodyFont: {
                        size: 12
                    },
                    callbacks: {
                        label: function(context) {
                            const label = context.label || '';
                            const value = context.parsed || 0;
                            const percentage = totalTx > 0 ? ((value / totalTx) * 100).toFixed(1) : 0;
                            return `${label}: ${value} transactions (${percentage}%)`;
                        }
                    }
                }
            }
        }
    });
}

// User Distribution Chart (Pie)
const userDistributionChartCtx = document.getElementById('userDistributionChart');
if (userDistributionChartCtx) {
    const totalUsers = userDistribution.customers + userDistribution.providers + userDistribution.staff;
    
    new Chart(userDistributionChartCtx, {
        type: 'pie',
        data: {
            labels: ['Customers', 'Providers', 'Staff'],
            datasets: [{
                data: [
                    userDistribution.customers,
                    userDistribution.providers,
                    userDistribution.staff
                ],
                backgroundColor: [
                    '#7c3aed',
                    '#4299e1',
                    '#48bb78'
                ],
                borderColor: '#ffffff',
                borderWidth: 3,
                hoverOffset: 10
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: {
                        padding: 15,
                        usePointStyle: true,
                        font: {
                            size: 12,
                            weight: '500'
                        },
                        generateLabels: function(chart) {
                            const data = chart.data;
                            if (data.labels.length && data.datasets.length) {
                                return data.labels.map((label, i) => {
                                    const value = data.datasets[0].data[i];
                                    const percentage = totalUsers > 0 ? ((value / totalUsers) * 100).toFixed(1) : 0;
                                    return {
                                        text: `${label}: ${value} (${percentage}%)`,
                                        fillStyle: data.datasets[0].backgroundColor[i],
                                        hidden: false,
                                        index: i
                                    };
                                });
                            }
                            return [];
                        }
                    }
                },
                tooltip: {
                    backgroundColor: 'rgba(26, 32, 44, 0.95)',
                    padding: 12,
                    cornerRadius: 8,
                    titleFont: {
                        size: 13,
                        weight: '600'
                    },
                    bodyFont: {
                        size: 12
                    },
                    callbacks: {
                        label: function(context) {
                            const label = context.label || '';
                            const value = context.parsed || 0;
                            const percentage = totalUsers > 0 ? ((value / totalUsers) * 100).toFixed(1) : 0;
                            return `${label}: ${value} users (${percentage}%)`;
                        }
                    }
                }
            }
        }
    });
}

// Date Preset Functions
function setDatePreset(preset) {
    // Remove active class from all buttons
    document.querySelectorAll('.preset-btn').forEach(btn => {
        btn.classList.remove('active');
    });
    
    // Add active class to clicked button
    event.target.classList.add('active');
    
    // Add loading state
    event.target.disabled = true;
    const originalText = event.target.textContent;
    event.target.innerHTML = '<span style="opacity: 0.7;">Loading...</span>';
    
    const today = new Date();
    let startDate, endDate = today;

    switch (preset) {
        case 'Today':
            startDate = new Date(today);
            break;
        case 'Last 7 Days':
            startDate = new Date(today);
            startDate.setDate(startDate.getDate() - 7);
            break;
        case 'Last 30 Days':
            startDate = new Date(today);
            startDate.setDate(startDate.getDate() - 30);
            break;
        case 'This Month':
            startDate = new Date(today.getFullYear(), today.getMonth(), 1);
            break;
        case 'Last Month':
            startDate = new Date(today.getFullYear(), today.getMonth() - 1, 1);
            endDate = new Date(today.getFullYear(), today.getMonth(), 0);
            break;
        default:
            // Restore button if invalid preset
            event.target.disabled = false;
            event.target.textContent = originalText;
            return;
    }

    // Format dates as YYYY-MM-DD
    const formatDate = (date) => {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    };

    // Update inputs
    document.getElementById('startDate').value = formatDate(startDate);
    document.getElementById('endDate').value = formatDate(endDate);
    
    // Set preset value in hidden input
    document.getElementById('presetInput').value = preset;

    // Submit form
    document.getElementById('dateFilterForm').submit();
}

// Custom Date Function (when user manually picks dates)
function setCustomDate() {
    // Set preset to "Custom" when user manually selects dates
    document.getElementById('presetInput').value = 'Custom';
    
    // Validate date range
    const startDate = document.getElementById('startDate').value;
    const endDate = document.getElementById('endDate').value;
    
    if (startDate && endDate) {
        const start = new Date(startDate);
        const end = new Date(endDate);
        
        if (end < start) {
            // Show error message using toast if available, otherwise alert
            if (window.toastManager) {
                window.toastManager.error('End date must be after start date. Please select a valid date range.');
            } else {
                alert('End date must be after start date. Please select a valid date range.');
            }
            return; // Don't submit form
        }
    }
    
    // Submit form
    document.getElementById('dateFilterForm').submit();
}

// Export Dashboard Function
function exportDashboard() {
    // Simple print for now - can be extended to PDF/Excel export
    window.print();
}

// Detail Modal Functions
function showDetailModal(type, filter) {
    const modal = document.getElementById('detailModal');
    const title = document.getElementById('modalTitle');
    const body = document.getElementById('modalBody');
    
    // Set title based on type and filter
    const titles = {
        'products-all': 'All Products',
        'products-available': 'Available Products',
        'products-rented': 'Rented Products',
        'products-sold': 'Sold Products',
        'orders-all': 'All Orders',
        'orders-in_use': 'Orders In Use',
        'orders-returned_with_issue': 'Orders Returned with Issue',
        'orders-completed': 'Completed Orders',
        'reports-pending': 'Pending Reports',
        'violations-active': 'Active Violations',
        'users-banned': 'Banned Users'
    };
    
    title.textContent = titles[`${type}-${filter}`] || 'Details';
    
    // Show modal
    modal.style.display = 'flex';
    document.body.style.overflow = 'hidden';
    
    // Load content based on type and filter
    loadModalContent(type, filter, body);
}

function closeDetailModal() {
    const modal = document.getElementById('detailModal');
    modal.style.display = 'none';
    document.body.style.overflow = '';
}

async function loadModalContent(type, filter, body) {
    body.innerHTML = '<div class="modal-loading">Loading data...</div>';
    
    try {
        let data = null;
        
        switch(type) {
            case 'products':
                data = await fetchProductsData(filter);
                break;
            case 'orders':
                data = await fetchOrdersData(filter);
                break;
            case 'reports':
                data = await fetchReportsData(filter);
                break;
            case 'violations':
                data = await fetchViolationsData(filter);
                break;
            case 'users':
                data = await fetchUsersData(filter);
                break;
            default:
                body.innerHTML = '<div class="modal-error">Invalid request</div>';
                return;
        }
        
        // Store data for pagination
        window.modalData = {
            data: data,
            type: type,
            filter: filter,
            currentPage: 1
        };
        
        // Render with pagination
        renderModalWithPagination(type, filter);
        
        // Add search functionality
        setupModalSearch(type, filter);
    } catch (error) {
        const errorMessage = error.message || 'An unexpected error occurred.';
        body.innerHTML = `
            <div style="display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 3rem; text-align: center;">
                <svg style="width: 4rem; height: 4rem; margin-bottom: 1rem; color: #ef4444;" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <circle cx="12" cy="12" r="10"></circle>
                    <line x1="12" y1="8" x2="12" y2="12"></line>
                    <line x1="12" y1="16" x2="12.01" y2="16"></line>
                </svg>
                <p style="font-weight: 600; font-size: 1.125rem; color: #1f2937; margin-bottom: 0.5rem;">Unable to Load Data</p>
                <p style="font-size: 0.875rem; color: #6b7280; max-width: 400px;">${errorMessage}</p>
                <button onclick="loadModalContent('${type}', '${filter}', document.getElementById('modalBody'))" 
                        style="margin-top: 1.5rem; padding: 0.625rem 1.5rem; background: #7c3aed; color: white; border: none; border-radius: 0.5rem; cursor: pointer; font-size: 0.875rem; font-weight: 500;">
                    Try Again
                </button>
            </div>
        `;
    }
}

// Fetch data functions
async function fetchProductsData(filter) {
    const startDate = document.getElementById('startDate')?.value;
    const endDate = document.getElementById('endDate')?.value;
    const url = `${window.apiSettings.baseUrl}/dashboard/details/products?filter=${filter}&startDate=${startDate}&endDate=${endDate}`;
    
    const token = window.adminChatConfig?.accessToken || getCookie('AccessToken');
    
    if (!token) {
        throw new Error('Authentication required. Please login again.');
    }
    
    const response = await fetch(url, {
        headers: {
            'Authorization': `Bearer ${token}`
        }
    });
    
    if (!response.ok) {
        if (response.status === 401) {
            throw new Error('Authentication expired. Please refresh the page.');
        } else if (response.status === 403) {
            throw new Error('Access denied.');
        }
        throw new Error('Failed to load products data.');
    }
    
    const result = await response.json();
    return result.data;
}

async function fetchOrdersData(filter) {
    const startDate = document.getElementById('startDate')?.value;
    const endDate = document.getElementById('endDate')?.value;
    const url = `${window.apiSettings.baseUrl}/dashboard/details/orders?filter=${filter}&startDate=${startDate}&endDate=${endDate}`;
    
    const token = window.adminChatConfig?.accessToken || getCookie('AccessToken');
    
    if (!token) {
        throw new Error('Authentication required. Please login again.');
    }
    
    const response = await fetch(url, {
        headers: {
            'Authorization': `Bearer ${token}`
        }
    });
    
    if (!response.ok) {
        if (response.status === 401) {
            throw new Error('Authentication expired. Please refresh the page.');
        }
        throw new Error('Failed to load orders data.');
    }
    
    const result = await response.json();
    return result.data;
}

async function fetchReportsData(filter) {
    const startDate = document.getElementById('startDate')?.value;
    const endDate = document.getElementById('endDate')?.value;
    const url = `${window.apiSettings.baseUrl}/dashboard/details/reports?filter=${filter}&startDate=${startDate}&endDate=${endDate}`;
    
    const token = window.adminChatConfig?.accessToken || getCookie('AccessToken');
    
    if (!token) {
        throw new Error('Authentication required. Please login again.');
    }
    
    const response = await fetch(url, {
        headers: {
            'Authorization': `Bearer ${token}`
        }
    });
    
    if (!response.ok) {
        if (response.status === 401) {
            throw new Error('Authentication expired. Please refresh the page.');
        }
        throw new Error('Failed to load reports data.');
    }
    
    const result = await response.json();
    return result.data;
}

async function fetchViolationsData(filter) {
    const startDate = document.getElementById('startDate')?.value;
    const endDate = document.getElementById('endDate')?.value;
    const url = `${window.apiSettings.baseUrl}/dashboard/details/violations?filter=${filter}&startDate=${startDate}&endDate=${endDate}`;
    
    const token = window.adminChatConfig?.accessToken || getCookie('AccessToken');
    
    if (!token) {
        throw new Error('Authentication required. Please login again.');
    }
    
    const response = await fetch(url, {
        headers: {
            'Authorization': `Bearer ${token}`
        }
    });
    
    if (!response.ok) {
        if (response.status === 401) {
            throw new Error('Authentication expired. Please refresh the page.');
        }
        throw new Error('Failed to load violations data.');
    }
    
    const result = await response.json();
    return result.data;
}

async function fetchUsersData(filter) {
    const startDate = document.getElementById('startDate')?.value;
    const endDate = document.getElementById('endDate')?.value;
    const url = `${window.apiSettings.baseUrl}/dashboard/details/users?filter=${filter}&startDate=${startDate}&endDate=${endDate}`;
    
    const token = window.adminChatConfig?.accessToken || getCookie('AccessToken');
    
    if (!token) {
        throw new Error('Authentication required. Please login again.');
    }
    
    const response = await fetch(url, {
        headers: {
            'Authorization': `Bearer ${token}`
        }
    });
    
    if (!response.ok) {
        if (response.status === 401) {
            throw new Error('Authentication expired. Please refresh the page.');
        }
        throw new Error('Failed to load users data.');
    }
    
    const result = await response.json();
    return result.data;
}

// Helper to get cookie
function getCookie(name) {
    const value = `; ${document.cookie}`;
    const parts = value.split(`; ${name}=`);
    if (parts.length === 2) return parts.pop().split(';').shift();
    return '';
}

// Pagination Helper
function paginateData(data, page = 1, itemsPerPage = 20) {
    const startIndex = (page - 1) * itemsPerPage;
    const endIndex = startIndex + itemsPerPage;
    return {
        data: data.slice(startIndex, endIndex),
        totalPages: Math.ceil(data.length / itemsPerPage),
        currentPage: page,
        totalItems: data.length,
        itemsPerPage: itemsPerPage
    };
}

// Generate pagination HTML - Always visible for consistent layout
function generatePaginationHTML(paginationInfo, type, filter) {
    const { currentPage, totalPages, totalItems, itemsPerPage } = paginationInfo;
    
    const startItem = totalItems > 0 ? ((currentPage - 1) * itemsPerPage) + 1 : 0;
    const endItem = Math.min(currentPage * itemsPerPage, totalItems);
    
    let paginationHTML = '<div class="modal-pagination">';
    paginationHTML += `<div class="pagination-info">Showing ${startItem}-${endItem} of ${totalItems} items</div>`;
    paginationHTML += '<div class="pagination-buttons">';
    
    // Previous button
    paginationHTML += `<button class="pagination-btn" 
                        onclick="changeDetailModalPage('${type}', '${filter}', ${currentPage - 1})" 
                        ${currentPage <= 1 ? 'disabled' : ''}>‹ Previous</button>`;
    
    // Page numbers (only if more than 1 page)
    if (totalPages > 1) {
        const maxVisiblePages = 5;
        let startPage = Math.max(1, currentPage - Math.floor(maxVisiblePages / 2));
        let endPage = Math.min(totalPages, startPage + maxVisiblePages - 1);
        
        if (endPage - startPage < maxVisiblePages - 1) {
            startPage = Math.max(1, endPage - maxVisiblePages + 1);
        }
        
        if (startPage > 1) {
            paginationHTML += `<button class="pagination-btn" onclick="changeDetailModalPage('${type}', '${filter}', 1)">1</button>`;
            if (startPage > 2) {
                paginationHTML += '<span class="pagination-ellipsis">...</span>';
            }
        }
        
        for (let i = startPage; i <= endPage; i++) {
            paginationHTML += `<button class="pagination-btn ${i === currentPage ? 'active' : ''}" onclick="changeDetailModalPage('${type}', '${filter}', ${i})">${i}</button>`;
        }
        
        if (endPage < totalPages) {
            if (endPage < totalPages - 1) {
                paginationHTML += '<span class="pagination-ellipsis">...</span>';
            }
            paginationHTML += `<button class="pagination-btn" onclick="changeDetailModalPage('${type}', '${filter}', ${totalPages})">${totalPages}</button>`;
        }
    } else {
        paginationHTML += `<span class="pagination-info">Page 1 of ${totalPages || 1}</span>`;
    }
    
    // Next button
    paginationHTML += `<button class="pagination-btn" 
                        onclick="changeDetailModalPage('${type}', '${filter}', ${currentPage + 1})" 
                        ${currentPage >= totalPages || totalPages === 0 ? 'disabled' : ''}>Next ›</button>`;
    
    paginationHTML += '</div></div>';
    return paginationHTML;
}

// Store original data for pagination
window.modalData = {
    data: [],
    type: '',
    filter: '',
    currentPage: 1
};

// Change page function for detail modal
function changeDetailModalPage(type, filter, page) {
    window.modalData.currentPage = page;
    renderModalWithPagination(type, filter);
}

// Render modal with pagination
function renderModalWithPagination(type, filter) {
    const modalBody = document.getElementById('modalBody');
    const { data, currentPage } = window.modalData;
    
    const paginationInfo = paginateData(data, currentPage, 20);
    let content = '';
    
    switch(type) {
        case 'products':
            content = renderProductsTablePaginated(paginationInfo, filter);
            break;
        case 'orders':
            content = renderOrdersTablePaginated(paginationInfo, filter);
            break;
        case 'reports':
            content = renderReportsTablePaginated(paginationInfo);
            break;
        case 'violations':
            content = renderViolationsTablePaginated(paginationInfo);
            break;
        case 'users':
            content = renderUsersTablePaginated(paginationInfo);
            break;
    }
    
    modalBody.innerHTML = content;
    setupModalSearch(type, filter);
}

// Paginated render functions
function renderProductsTablePaginated(paginationInfo, filter) {
    const { data, totalItems, currentPage, itemsPerPage } = paginationInfo;
    
    if (!data || data.length === 0) {
        return '<div class="modal-empty">No products found.</div>';
    }
    
    return `
        <div class="modal-data-table">
            <table class="detail-table">
                <thead>
                    <tr>
                        <th>Image</th>
                        <th>Product Name</th>
                        <th>Provider</th>
                        <th>Price/Day</th>
                        <th>Status</th>
                        <th>Created Date</th>
                    </tr>
                </thead>
                <tbody>
                    ${data.map(item => `
                        <tr class="table-row-clickable" onclick="window.location.href='/products/detail/${item.id}'">
                            <td>
                                <img src="${item.imageUrl || '/images/placeholder.png'}" alt="${item.name}" class="table-img" />
                            </td>
                            <td><strong>${item.name}</strong></td>
                            <td>${item.providerName}</td>
                            <td>${item.pricePerDay.toLocaleString('vi-VN')}₫</td>
                            <td><span class="status-badge status-${item.status.toLowerCase()}">${item.status}</span></td>
                            <td>${new Date(item.createdAt).toLocaleDateString('vi-VN')}</td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
            ${generatePaginationHTML(paginationInfo, 'products', filter)}
        </div>
    `;
}

function renderOrdersTablePaginated(paginationInfo, filter) {
    const { data } = paginationInfo;
    
    if (!data || data.length === 0) {
        return '<div class="modal-empty">No orders found.</div>';
    }
    
    return `
        <div class="modal-data-table">
            <table class="detail-table">
                <thead>
                    <tr>
                        <th>Order #</th>
                        <th>Customer</th>
                        <th>Provider</th>
                        <th>Total Amount</th>
                        <th>Status</th>
                        <th>Date</th>
                    </tr>
                </thead>
                <tbody>
                    ${data.map(item => `
                        <tr class="table-row-clickable" onclick="window.location.href='/provider/order/${item.id}'">
                            <td><strong>${item.orderNumber}</strong></td>
                            <td>${item.customerName}</td>
                            <td>${item.providerName}</td>
                            <td>${item.totalAmount.toLocaleString('vi-VN')}₫</td>
                            <td><span class="status-badge status-${item.status.toLowerCase().replace('_', '-')}">${formatOrderStatus(item.status)}</span></td>
                            <td>${new Date(item.createdAt).toLocaleDateString('vi-VN')}</td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
            ${generatePaginationHTML(paginationInfo, 'orders', filter)}
        </div>
    `;
}

function renderReportsTablePaginated(paginationInfo) {
    const { data } = paginationInfo;
    
    if (!data || data.length === 0) {
        return '<div class="modal-empty">No reports found.</div>';
    }
    
    return `
        <div class="modal-data-table">
            <table class="detail-table">
                <thead>
                    <tr>
                        <th>Subject</th>
                        <th>Reporter</th>
                        <th>Status</th>
                        <th>Date</th>
                    </tr>
                </thead>
                <tbody>
                    ${data.map(item => `
                        <tr class="table-row-clickable" onclick="window.location.href='/reportmanagement?reportId=${item.id}'">
                            <td><strong>${item.subject}</strong></td>
                            <td>${item.reporterName}</td>
                            <td><span class="status-badge status-${item.status.toLowerCase()}">${item.status}</span></td>
                            <td>${new Date(item.createdAt).toLocaleDateString('vi-VN')}</td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
            ${generatePaginationHTML(paginationInfo, 'reports', 'pending')}
        </div>
    `;
}

function renderViolationsTablePaginated(paginationInfo) {
    const { data } = paginationInfo;
    
    if (!data || data.length === 0) {
        return '<div class="modal-empty">No violations found.</div>';
    }
    
    return `
        <div class="modal-data-table">
            <table class="detail-table">
                <thead>
                    <tr>
                        <th>Description</th>
                        <th>Customer</th>
                        <th>Fine Amount</th>
                        <th>Status</th>
                        <th>Date</th>
                    </tr>
                </thead>
                <tbody>
                    ${data.map(item => `
                        <tr class="table-row-clickable" onclick="window.location.href='/provider/createviolation?violationId=${item.id}'">
                            <td><strong>${item.description}</strong></td>
                            <td>${item.customerName}</td>
                            <td>${item.fineAmount ? item.fineAmount.toLocaleString('vi-VN') + '₫' : 'N/A'}</td>
                            <td><span class="status-badge status-${item.status.toLowerCase()}">${item.status}</span></td>
                            <td>${new Date(item.createdAt).toLocaleDateString('vi-VN')}</td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
            ${generatePaginationHTML(paginationInfo, 'violations', 'active')}
        </div>
    `;
}

function renderUsersTablePaginated(paginationInfo) {
    const { data } = paginationInfo;
    
    if (!data || data.length === 0) {
        return '<div class="modal-empty">No users found.</div>';
    }
    
    return `
        <div class="modal-data-table">
            <table class="detail-table">
                <thead>
                    <tr>
                        <th>Full Name</th>
                        <th>Email</th>
                        <th>Role</th>
                        <th>Status</th>
                        <th>Joined Date</th>
                    </tr>
                </thead>
                <tbody>
                    ${data.map(item => {
                        // Determine navigation URL based on role
                        const navUrl = item.role.toLowerCase() === 'staff' 
                            ? '/admin/staffmanagement' 
                            : '/admin/usermanagement?userId=' + item.id;
                        
                        return `
                        <tr class="table-row-clickable" onclick="window.location.href='${navUrl}'">
                            <td><strong>${item.fullName}</strong></td>
                            <td>${item.email}</td>
                            <td><span class="role-badge">${item.role}</span></td>
                            <td><span class="status-badge ${item.isActive ? 'status-active' : 'status-banned'}">${item.isActive ? 'Active' : 'Banned'}</span></td>
                            <td>${new Date(item.createdAt).toLocaleDateString('vi-VN')}</td>
                        </tr>
                        `;
                    }).join('')}
                </tbody>
            </table>
            ${generatePaginationHTML(paginationInfo, 'users', 'banned')}
        </div>
    `;
}

function setupModalSearch(type, filter) {
    const searchInput = document.getElementById('modalSearch');
    const filterSelect = document.getElementById('modalFilter');
    
    // Show search and hide filter dropdown (we already filtered by clicking the stat)
    if (searchInput) {
        searchInput.style.display = 'block';
        searchInput.placeholder = `Search ${type}...`;
        
        // Add search event listener
        searchInput.addEventListener('input', function(e) {
            const searchTerm = e.target.value.toLowerCase();
            const rows = document.querySelectorAll('.table-row-clickable');
            
            rows.forEach(row => {
                const text = row.textContent.toLowerCase();
                if (text.includes(searchTerm)) {
                    row.style.display = '';
                } else {
                    row.style.display = 'none';
                }
            });
            
            // Update footer count
            const visibleRows = Array.from(rows).filter(r => r.style.display !== 'none');
            const footer = document.querySelector('.table-footer p');
            if (footer) {
                footer.textContent = `Showing ${visibleRows.length} of ${rows.length} items. Click on any row to view details.`;
            }
        });
    }
    
    // Hide filter select (already filtered)
    if (filterSelect) filterSelect.style.display = 'none';
}

// Close modal on ESC key
document.addEventListener('keydown', function(e) {
    if (e.key === 'Escape') {
        closeDetailModal();
    }
});

// Smooth scroll for anchor links
document.querySelectorAll('a[href^="#"]').forEach(anchor => {
    anchor.addEventListener('click', function (e) {
        e.preventDefault();
        const target = document.querySelector(this.getAttribute('href'));
        if (target) {
            target.scrollIntoView({
                behavior: 'smooth',
                block: 'start'
            });
        }
    });
});

// Auto-refresh dashboard data every 5 minutes (optional)
let autoRefreshEnabled = false;
let autoRefreshInterval;

function toggleAutoRefresh() {
    autoRefreshEnabled = !autoRefreshEnabled;
    
    if (autoRefreshEnabled) {
        autoRefreshInterval = setInterval(() => {
            location.reload();
        }, 5 * 60 * 1000); // 5 minutes
    } else {
        clearInterval(autoRefreshInterval);
    }
}

// Number animation on page load
function animateValue(element, start, end, duration) {
    const range = end - start;
    const increment = range / (duration / 16); // 60fps
    let current = start;
    
    const timer = setInterval(() => {
        current += increment;
        if ((increment > 0 && current >= end) || (increment < 0 && current <= end)) {
            current = end;
            clearInterval(timer);
        }
        
        if (element.textContent.includes('₫')) {
            element.textContent = current.toLocaleString('vi-VN') + '₫';
        } else if (element.textContent.includes('%')) {
            element.textContent = current.toFixed(1) + '%';
        } else {
            element.textContent = Math.floor(current).toLocaleString();
        }
    }, 16);
}

// Initialize animations on load
document.addEventListener('DOMContentLoaded', function() {
    // Animate KPI values
    const kpiValues = document.querySelectorAll('.kpi-value');
    kpiValues.forEach(el => {
        const text = el.textContent.trim();
        const numericValue = parseFloat(text.replace(/[₫,đ]/g, ''));
        if (!isNaN(numericValue) && numericValue > 0) {
            el.setAttribute('data-target', numericValue);
            animateValue(el, 0, numericValue, 1000);
        }
    });

    // Add entrance animations
    const cards = document.querySelectorAll('.kpi-card, .metric-card, .chart-card, .table-card');
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.style.opacity = '0';
                entry.target.style.transform = 'translateY(20px)';
                setTimeout(() => {
                    entry.target.style.transition = 'opacity 0.5s ease, transform 0.5s ease';
                    entry.target.style.opacity = '1';
                    entry.target.style.transform = 'translateY(0)';
                }, 100);
                observer.unobserve(entry.target);
            }
        });
    }, { threshold: 0.1 });

    cards.forEach(card => observer.observe(card));
});

// Tooltip functionality (optional enhancement)
function initTooltips() {
    const tooltipElements = document.querySelectorAll('[data-tooltip]');
    tooltipElements.forEach(el => {
        el.addEventListener('mouseenter', function(e) {
            const tooltip = document.createElement('div');
            tooltip.className = 'custom-tooltip';
            tooltip.textContent = this.getAttribute('data-tooltip');
            document.body.appendChild(tooltip);
            
            const rect = this.getBoundingClientRect();
            tooltip.style.left = rect.left + (rect.width / 2) - (tooltip.offsetWidth / 2) + 'px';
            tooltip.style.top = rect.top - tooltip.offsetHeight - 8 + 'px';
        });
        
        el.addEventListener('mouseleave', function() {
            const tooltip = document.querySelector('.custom-tooltip');
            if (tooltip) {
                tooltip.remove();
            }
        });
    });
}

// Initialize tooltips if needed
// initTooltips();



// Providers Tab Functions

function filterProviders() {
    const searchInput = document.getElementById('providerSearchInput');
    const filter = searchInput.value.toLowerCase();
    const table = document.getElementById('providersTable');
    const rows = table.getElementsByTagName('tr');

    for (let i = 1; i < rows.length; i++) {
        const row = rows[i];
        const providerName = row.getAttribute('data-provider-name');
        if (providerName && providerName.includes(filter)) {
            row.style.display = '';
        } else {
            row.style.display = 'none';
        }
    }
}

function sortProvidersTable(columnIndex) {
    const table = document.getElementById('providersTable');
    if (!table) return;
    
    const tbody = table.getElementsByTagName('tbody')[0];
    const rows = Array.from(tbody.getElementsByTagName('tr'));
    const th = table.getElementsByTagName('th')[columnIndex];
    
    // Toggle sort direction
    const isAsc = th.classList.contains('sort-asc');
    
    // Remove all sort classes
    table.querySelectorAll('th').forEach(header => {
        header.classList.remove('sort-asc', 'sort-desc');
    });
    
    // Add appropriate sort class
    th.classList.add(isAsc ? 'sort-desc' : 'sort-asc');
    
    // Sort rows
    rows.sort((a, b) => {
        let aValue, bValue;
        
        if (columnIndex === 0) {
            // Provider name
            aValue = a.getAttribute('data-provider-name');
            bValue = b.getAttribute('data-provider-name');
        } else {
            // Numeric columns
            const aCells = a.getElementsByTagName('td');
            const bCells = b.getElementsByTagName('td');
            aValue = parseFloat(aCells[columnIndex].getAttribute('data-value') || aCells[columnIndex].textContent.replace(/[^0-9.]/g, ''));
            bValue = parseFloat(bCells[columnIndex].getAttribute('data-value') || bCells[columnIndex].textContent.replace(/[^0-9.]/g, ''));
        }
        
        if (isAsc) {
            return aValue > bValue ? -1 : 1;
        } else {
            return aValue < bValue ? -1 : 1;
        }
    });
    
    // Re-append sorted rows
    rows.forEach(row => tbody.appendChild(row));
}
