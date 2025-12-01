// Feedback Management JavaScript
const API_BASE_URL = window.apiSettings?.baseUrl 
    ? `${window.apiSettings.baseUrl}/feedbacks/management`
    : 'https://localhost:7256/api/feedbacks/management';

let currentPage = 1;
let currentTab = 'all';
let currentFilters = {
    searchTerm: '',
    rating: null,
    timeFilter: '',
    isBlocked: null,
    pageSize: 10
};

// Initialize
document.addEventListener('DOMContentLoaded', function() {
    initializeEventListeners();
    loadStatistics();
    loadFeedbacks();
});

function initializeEventListeners() {
    // Tab switching
    document.querySelectorAll('.tab-button').forEach(button => {
        button.addEventListener('click', function() {
            switchTab(this.dataset.tab);
        });
    });

    // Search
    const searchInput = document.getElementById('searchInput');
    let searchTimeout;
    searchInput.addEventListener('input', function() {
        clearTimeout(searchTimeout);
        searchTimeout = setTimeout(() => {
            currentFilters.searchTerm = this.value;
            currentPage = 1;
            loadFeedbacks();
        }, 500);
    });

    // Filters
    document.getElementById('ratingFilter').addEventListener('change', function() {
        currentFilters.rating = this.value ? parseInt(this.value) : null;
        currentPage = 1;
        loadFeedbacks();
    });

    document.getElementById('timeFilter').addEventListener('change', function() {
        currentFilters.timeFilter = this.value;
        currentPage = 1;
        loadFeedbacks();
    });

    // Modal close
    document.querySelector('.close-modal')?.addEventListener('click', closeModal);
    window.addEventListener('click', function(event) {
        const modal = document.getElementById('feedbackDetailModal');
        if (event.target === modal) {
            closeModal();
        }
    });
}

function switchTab(tab) {
    currentTab = tab;
    currentPage = 1;
    
    // Update tab buttons
    document.querySelectorAll('.tab-button').forEach(btn => {
        btn.classList.remove('active');
    });
    document.querySelector(`[data-tab="${tab}"]`).classList.add('active');
    
    // Simplified: Blocked tab shows blocked feedbacks only
    if (tab === 'blocked') {
        currentFilters.isBlocked = true;
    } else {
        currentFilters.isBlocked = null;
    }
    
    loadFeedbacks();
}

async function loadStatistics() {
    try {
        const token = localStorage.getItem('AccessToken')
                   || localStorage.getItem('token')
                   || sessionStorage.getItem('AccessToken')
                   || getCookie('AccessToken');
        
        if (!token) {
            console.warn('No token found, skipping statistics load');
            return;
        }
        
        const response = await fetch(`${API_BASE_URL}/statistics`, {
            headers: {
                'Authorization': `Bearer ${token}`
            }
        });
        
        if (!response.ok) throw new Error('Failed to load statistics');
        
        const result = await response.json();
        const stats = result.data;
        
        document.getElementById('totalFeedbacks').textContent = stats.totalFeedbacks;
        document.getElementById('averageRating').textContent = stats.averageRating.toFixed(1);
        document.getElementById('blockedContent').textContent = stats.flaggedContent; // flaggedContent now means blocked
        
        document.getElementById('allCount').textContent = stats.totalFeedbacks;
        document.getElementById('blockedCount').textContent = stats.flaggedContent; // flaggedContent now means blocked
    } catch (error) {
        console.error('Error loading statistics:', error);
        showNotification('Failed to load statistics', 'error');
    }
}

async function loadFeedbacks() {
    try {
        const token = localStorage.getItem('AccessToken')
                   || localStorage.getItem('token')
                   || sessionStorage.getItem('AccessToken')
                   || getCookie('AccessToken');
        
        if (!token) {
            console.warn('No token found, skipping feedbacks load');
            document.getElementById('feedbackTableBody').innerHTML = `
                <tr><td colspan="8" class="text-center" style="color: #d32f2f;">Please login to view feedbacks</td></tr>
            `;
            return;
        }
        
        const params = new URLSearchParams({
            pageNumber: currentPage,
            pageSize: currentFilters.pageSize,
            ...(currentFilters.searchTerm && { searchTerm: currentFilters.searchTerm }),
            ...(currentFilters.rating && { rating: currentFilters.rating }),
            ...(currentFilters.timeFilter && { timeFilter: currentFilters.timeFilter }),
            ...(currentFilters.isBlocked !== null && { isBlocked: currentFilters.isBlocked })
        });
        
        const response = await fetch(`${API_BASE_URL}?${params}`, {
            headers: {
                'Authorization': `Bearer ${token}`
            }
        });
        
        if (!response.ok) throw new Error('Failed to load feedbacks');
        
        const result = await response.json();
        const data = result.data;
        
        renderFeedbackTable(data.items);
        renderPagination(data.page, data.pageSize, data.totalItems);
    } catch (error) {
        console.error('Error loading feedbacks:', error);
        showNotification('Failed to load feedbacks', 'error');
        document.getElementById('feedbackTableBody').innerHTML = `
            <tr><td colspan="8" class="text-center" style="color: #d32f2f;">Error loading feedbacks</td></tr>
        `;
    }
}

function renderFeedbackTable(feedbacks) {
    const tbody = document.getElementById('feedbackTableBody');
    
    if (feedbacks.length === 0) {
        tbody.innerHTML = '<tr><td colspan="8" class="text-center">No feedbacks found</td></tr>';
        return;
    }
    
    // Group feedbacks by product
    const productGroups = {};
    feedbacks.forEach(feedback => {
        const productId = feedback.productId;
        if (!productId) return;
        
        if (!productGroups[productId]) {
            productGroups[productId] = {
                productId: productId,
                productName: feedback.productName,
                productImageUrl: feedback.productImageUrl,
                providerName: feedback.providerName,
                feedbacks: [],
                totalReviews: 0,
                avgRating: 0,
                flaggedCount: 0,
                blockedCount: 0,
                latestDate: null
            };
        }
        
        productGroups[productId].feedbacks.push(feedback);
        productGroups[productId].totalReviews++;
        // Simplified: Just count blocked feedbacks
        if (feedback.isBlocked) productGroups[productId].blockedCount++;
        
        const feedbackDate = new Date(feedback.createdAt);
        if (!productGroups[productId].latestDate || feedbackDate > productGroups[productId].latestDate) {
            productGroups[productId].latestDate = feedbackDate;
        }
    });
    
    // Calculate average ratings
    Object.values(productGroups).forEach(group => {
        const totalRating = group.feedbacks.reduce((sum, fb) => sum + fb.rating, 0);
        group.avgRating = (totalRating / group.totalReviews).toFixed(1);
    });
    
    // Convert to array and sort by latest review
    const products = Object.values(productGroups).sort((a, b) => b.latestDate - a.latestDate);
    
    tbody.innerHTML = products.map((product, index) => `
        <tr data-product-id="${product.productId}">
            <td>${(currentPage - 1) * currentFilters.pageSize + index + 1}</td>
            <td>
                <div class="product-cell">
                    <img src="${product.productImageUrl || '/images/placeholder.png'}" 
                         alt="${product.productName}" 
                         class="product-image">
                    <div class="product-info">
                        <p class="product-name">${product.productName || 'N/A'}</p>
                        <p class="product-provider">by ${product.providerName || 'Unknown'}</p>
                    </div>
                </div>
            </td>
            <td style="text-align: center;">
                <strong style="font-size: 1.25rem; color: #1976d2;">${product.totalReviews}</strong>
            </td>
            <td style="text-align: center;">
                <div style="display: flex; flex-direction: column; align-items: center;">
                    <span class="rating-stars">${'⭐'.repeat(Math.round(product.avgRating))}</span>
                    <span class="rating-number">${product.avgRating}/5</span>
                </div>
            </td>
            <td style="text-align: center;">
                ${formatDate(product.latestDate)}
            </td>
            <td style="text-align: center;">
                <span style="color: ${product.flaggedCount > 0 ? '#d32f2f' : '#666'}; font-weight: ${product.flaggedCount > 0 ? 'bold' : 'normal'};">
                    ${product.flaggedCount}
                </span>
            </td>
            <td style="text-align: center;">
                <span style="color: ${product.blockedCount > 0 ? '#d32f2f' : '#666'}; font-weight: ${product.blockedCount > 0 ? 'bold' : 'normal'};">
                    ${product.blockedCount}
                </span>
            </td>
            <td>
                <div class="action-buttons">
                    <button class="action-btn action-btn-expand" 
                            onclick="toggleExpandRow(this, null)"
                            title="View All Feedbacks">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <polyline points="6 9 12 15 18 9"></polyline>
                        </svg>
                    </button>
                </div>
            </td>
        </tr>
    `).join('');
}

async function toggleExpandRow(button, feedbackId) {
    const row = button.closest('tr');
    const existingExpanded = row.nextElementSibling;
    
    // Check if already expanded
    if (existingExpanded && existingExpanded.classList.contains('expanded-row')) {
        existingExpanded.remove();
        button.innerHTML = `
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <polyline points="6 9 12 15 18 9"></polyline>
            </svg>
        `;
        return;
    }
    
    // Get product ID from current row
    const productId = row.dataset.productId;
    
    if (!productId) {
        console.error('Product ID not found');
        return;
    }
    
    // Show loading (simple text, no spinner)
    const loadingRow = document.createElement('tr');
    loadingRow.className = 'expanded-row';
    loadingRow.innerHTML = `
        <td colspan="8" style="text-align: center; padding: 20px; color: #666;">
            Loading product feedbacks...
        </td>
    `;
    row.after(loadingRow);
    
    try {
        // Fetch all feedbacks for this product
        const token = localStorage.getItem('AccessToken')
                   || localStorage.getItem('token')
                   || sessionStorage.getItem('AccessToken')
                   || getCookie('AccessToken');
        
        // Use management endpoint to get ALL feedbacks (including hidden ones)
        const response = await fetch(`${API_BASE_URL}/product/${productId}?page=1&pageSize=100`, {
            headers: {
                'Authorization': `Bearer ${token}`
            }
        });
        
        if (!response.ok) throw new Error('Failed to load product feedbacks');
        
        const result = await response.json();
        let feedbacks = result.data?.items || result.data || [];
        
        console.log('=== EXPAND PRODUCT DEBUG ===');
        console.log('Current tab:', currentTab);
        console.log('Product feedbacks response:', result);
        console.log('Total feedbacks from API:', feedbacks.length);
        if (feedbacks.length > 0) {
            console.log('First feedback:', feedbacks[0]);
            console.log('All feedbacks:', feedbacks.map(fb => ({
                id: fb.feedbackId || fb.id,
                isFlagged: fb.isFlagged,
                isBlocked: fb.isBlocked,
                comment: fb.comment?.substring(0, 30)
            })));
        }
        
        // Filter feedbacks based on current tab
        if (currentTab === 'blocked') {
            console.log('Applying blocked filter...');
            const beforeFilter = feedbacks.length;
            // Simplified: In Blocked tab, only show blocked feedbacks
            feedbacks = feedbacks.filter(fb => fb.isBlocked === true);
            console.log(`Filtered: ${beforeFilter} → ${feedbacks.length} feedbacks`);
        }
        
        // Remove loading row
        loadingRow.remove();
        
        // Create expanded content with filtered feedbacks
        const expandedRow = document.createElement('tr');
        expandedRow.className = 'expanded-row';
        expandedRow.innerHTML = `
            <td colspan="8">
                <div class="expanded-content">
                    <h4 style="margin-bottom: 15px; color: #1976d2;">
                        ${currentTab === 'blocked' ? 'Blocked Feedbacks' : 'All Feedbacks'} for this Product (${feedbacks.length} total)
                    </h4>
                    <div class="product-feedbacks-list">
                        ${feedbacks.map((fb, index) => `
                            <div class="feedback-item ${(fb.feedbackId || fb.id) === feedbackId ? 'current-feedback' : ''}" style="
                                border: 1px solid #e0e0e0;
                                border-radius: 8px;
                                padding: 15px;
                                margin-bottom: 15px;
                                background: ${fb.id === feedbackId ? '#e3f2fd' : '#fff'};
                            ">
                                <div style="display: flex; justify-content: space-between; align-items: start; margin-bottom: 10px;">
                                    <div style="display: flex; align-items: center; gap: 10px;">
                                        ${fb.profilePictureUrl || fb.customerProfilePicture ? `
                                            <img src="${fb.profilePictureUrl || fb.customerProfilePicture}" 
                                                 alt="${fb.customerName || 'Customer'}"
                                                 class="customer-avatar"
                                                 onerror="this.style.display='none'; this.nextElementSibling.style.display='flex';"
                                                 style="width: 50px; height: 50px; border-radius: 50%; object-fit: cover; border: 2px solid #e0e0e0;">
                                            <div style="display: none; width: 50px; height: 50px; border-radius: 50%; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); align-items: center; justify-content: center; color: white; font-weight: bold; font-size: 1.25rem; border: 2px solid #e0e0e0;">
                                                ${(fb.customerName || 'U').charAt(0).toUpperCase()}
                                            </div>
                                        ` : `
                                            <div style="width: 50px; height: 50px; border-radius: 50%; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); display: flex; align-items: center; justify-content: center; color: white; font-weight: bold; font-size: 1.25rem; border: 2px solid #e0e0e0;">
                                                ${(fb.customerName || 'U').charAt(0).toUpperCase()}
                                            </div>
                                        `}
                                        <div>
                                            <strong style="font-size: 1rem;">${fb.customerName || 'Unknown Customer'}</strong>
                                            <div style="color: #999; font-size: 0.875rem; margin-top: 2px;">
                                                🕒 ${fb.submittedAt ? formatDate(fb.submittedAt) : (fb.createdAt ? formatDate(fb.createdAt) : 'N/A')}
                                            </div>
                                        </div>
                                    </div>
                                    <div style="text-align: right;">
                                        <div>
                                            <span class="rating-stars">${'⭐'.repeat(fb.rating || 0)}</span>
                                            <span class="rating-number">(${fb.rating || 0}/5)</span>
                                        </div>
                                        <div style="margin-top: 5px;">
                                            ${fb.isBlocked ? '<span style="color: #d32f2f; font-size: 0.75rem; font-weight: bold;">🚫 BLOCKED</span>' : ''}
                                        </div>
                                    </div>
                                </div>
                                ${!fb.isVisible && fb.violationReason ? `
                                    <div style="background: #fff3e0; border: 2px solid #ff9800; padding: 12px; border-radius: 4px; margin: 10px 0;">
                                        <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 8px;">
                                            <span style="font-size: 1.5rem;">⚠️</span>
                                            <strong style="color: #e65100;">Content Hidden Due to Violation</strong>
                                        </div>
                                        <p style="margin: 5px 0; color: #d84315; font-size: 0.875rem;">
                                            <strong>Reason:</strong> ${fb.violationReason}
                                        </p>
                                        <details style="margin-top: 10px;">
                                            <summary style="cursor: pointer; color: #666; font-size: 0.875rem;">Show original comment (visible only to you)</summary>
                                            <p style="margin: 10px 0 0 0; padding: 10px; background: #f5f5f5; border-radius: 4px; color: #555; opacity: 0.6;">
                                                ${fb.comment || 'No comment'}
                                            </p>
                                        </details>
                                    </div>
                                ` : `
                                    <div style="background: #f9f9f9; padding: 12px; border-radius: 4px; margin: 10px 0;">
                                        <strong style="color: #333;">Comment:</strong>
                                        <p style="margin: 5px 0 0 0; color: #555; line-height: 1.5;">${fb.comment || 'No comment provided'}</p>
                                    </div>
                                `}
                                ${fb.providerResponse || (fb.providerResponse && fb.providerResponse.responseText) ? `
                                    <div style="background: #e8f5e9; padding: 12px; border-radius: 4px; margin-top: 10px; border-left: 3px solid #4caf50;">
                                        <strong style="color: #2e7d32;">📝 Provider Response:</strong>
                                        <p style="margin: 5px 0 0 0; color: #333;">${fb.providerResponse?.responseText || fb.providerResponse || ''}</p>
                                        ${fb.providerResponse?.responderName ? `
                                            <p style="margin: 5px 0 0 0; color: #666; font-size: 0.75rem;">
                                                by ${fb.providerResponse.responderName} • ${formatDate(fb.providerResponse.respondedAt)}
                                            </p>
                                        ` : ''}
                                    </div>
                                ` : '<div style="color: #999; font-style: italic; margin-top: 10px;">No provider response yet</div>'}
                                <div style="margin-top: 15px; display: flex; gap: 10px; align-items: center;">
                                    <button class="btn btn-sm btn-primary" onclick="viewFeedbackDetail('${fb.feedbackId || fb.id}')" style="
                                        padding: 8px 16px;
                                        font-size: 0.875rem;
                                        border: none;
                                        border-radius: 4px;
                                        background: #1976d2;
                                        color: white;
                                        cursor: pointer;
                                        font-weight: 500;
                                    ">📄 View Details</button>
                                    ${fb.isBlocked || fb.isFlagged ? `
                                        <button class="btn btn-sm btn-success" onclick="unblockFeedback('${fb.feedbackId || fb.id}')" style="
                                            padding: 8px 16px;
                                            font-size: 0.875rem;
                                            border: none;
                                            border-radius: 4px;
                                            background: #388e3c;
                                            color: white;
                                            cursor: pointer;
                                            font-weight: 500;
                                        ">✅ Unblock</button>
                                    ` : `
                                        <button class="btn btn-sm btn-danger" onclick="blockFeedback('${fb.feedbackId || fb.id}')" style="
                                            padding: 8px 16px;
                                            font-size: 0.875rem;
                                            border: none;
                                            border-radius: 4px;
                                            background: #d32f2f;
                                            color: white;
                                            cursor: pointer;
                                            font-weight: 500;
                                        ">🚫 Block</button>
                                    `}
                                    ${fb.orderIdFromFeedback || fb.orderId ? `<span style="color: #666; font-size: 0.75rem;">Order: ${(fb.orderIdFromFeedback || fb.orderId).substring(0, 8)}...</span>` : ''}
                                </div>
                            </div>
                        `).join('')}
                    </div>
                </div>
            </td>
        `;
        
        row.after(expandedRow);
        
        // Update button icon
        button.innerHTML = `
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <polyline points="18 15 12 9 6 15"></polyline>
            </svg>
        `;
        
    } catch (error) {
        console.error('Error loading product feedbacks:', error);
        loadingRow.innerHTML = `
            <td colspan="8" style="text-align: center; padding: 20px; color: #d32f2f;">
                Failed to load product feedbacks
            </td>
        `;
    }
}

async function viewFeedbackDetail(feedbackId) {
    try {
        const token = localStorage.getItem('AccessToken')
                   || localStorage.getItem('token')
                   || sessionStorage.getItem('AccessToken')
                   || getCookie('AccessToken');
        const response = await fetch(`${API_BASE_URL}/${feedbackId}`, {
            headers: {
                'Authorization': `Bearer ${token}`
            }
        });
        
        if (!response.ok) throw new Error('Failed to load feedback detail');
        
        const result = await response.json();
        const feedback = result.data;
        
        renderFeedbackDetail(feedback);
        document.getElementById('feedbackDetailModal').style.display = 'block';
    } catch (error) {
        console.error('Error loading feedback detail:', error);
        showNotification('Failed to load feedback detail', 'error');
    }
}


function renderFeedbackDetail(feedback) {
    const content = document.getElementById('feedbackDetailContent');
    
    // Build Order Item section HTML
    let orderItemHtml = '';
    if (feedback.orderItem) {
        const isRental = feedback.orderItem.transactionType.toLowerCase() === 'rental';
        orderItemHtml = `
        <div class="detail-section">
            <h3>
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M6 2L3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4z"></path>
                    <line x1="3" y1="6" x2="21" y2="6"></line>
                    <path d="M16 10a4 4 0 0 1-8 0"></path>
                </svg>
                Order Information
            </h3>
            <div style="background: #f9f9f9; padding: 1rem; border-radius: 8px; border-left: 4px solid #1976d2;">
                <div style="display: grid; grid-template-columns: repeat(2, 1fr); gap: 0.75rem;">
                    <div>
                        <p style="margin: 0; color: #666; font-size: 0.875rem;">Order ID</p>
                        <p style="margin: 0.25rem 0 0 0; font-weight: 600; font-family: monospace;">${feedback.orderItem.orderId.substring(0, 8)}...</p>
                    </div>
                    <div>
                        <p style="margin: 0; color: #666; font-size: 0.875rem;">Transaction Type</p>
                        <p style="margin: 0.25rem 0 0 0; font-weight: 600;">
                            <span style="background: ${isRental ? '#e3f2fd' : '#f3e5f5'}; 
                                         color: ${isRental ? '#1976d2' : '#7b1fa2'}; 
                                         padding: 2px 8px; border-radius: 4px; font-size: 0.875rem;">
                                ${isRental ? '🔄 Rental' : '🛒 Purchase'}
                            </span>
                        </p>
                    </div>
                    <div>
                        <p style="margin: 0; color: #666; font-size: 0.875rem;">Quantity</p>
                        <p style="margin: 0.25rem 0 0 0; font-weight: 600;">${feedback.orderItem.quantity} item(s)</p>
                    </div>
                    ${feedback.orderItem.rentalDays ? `
                    <div>
                        <p style="margin: 0; color: #666; font-size: 0.875rem;">Rental Duration</p>
                        <p style="margin: 0.25rem 0 0 0; font-weight: 600;">${feedback.orderItem.rentalDays} day(s)</p>
                    </div>
                    ` : ''}
                    <div>
                        <p style="margin: 0; color: #666; font-size: 0.875rem;">${isRental ? 'Daily Rate' : 'Unit Price'}</p>
                        <p style="margin: 0.25rem 0 0 0; font-weight: 600;">${formatCurrency(feedback.orderItem.dailyRate)}</p>
                    </div>
                    <div>
                        <p style="margin: 0; color: #666; font-size: 0.875rem;">Deposit Per Unit</p>
                        <p style="margin: 0.25rem 0 0 0; font-weight: 600;">${formatCurrency(feedback.orderItem.depositPerUnit)}</p>
                    </div>
                </div>
                <div style="margin-top: 1rem; padding-top: 1rem; border-top: 2px solid #e0e0e0;">
                    <p style="margin: 0; color: #666; font-size: 0.875rem;">Total Price</p>
                    <p style="margin: 0.25rem 0 0 0; font-size: 1.5rem; font-weight: bold; color: #1976d2;">${formatCurrency(feedback.orderItem.totalPrice)}</p>
                    ${isRental ? 
                        `<p style="margin: 0.25rem 0 0 0; color: #666; font-size: 0.75rem;">
                            (${formatCurrency(feedback.orderItem.dailyRate)} × ${feedback.orderItem.rentalDays} days × ${feedback.orderItem.quantity} item(s))
                        </p>` : 
                        `<p style="margin: 0.25rem 0 0 0; color: #666; font-size: 0.75rem;">
                            (${formatCurrency(feedback.orderItem.dailyRate)} × ${feedback.orderItem.quantity} item(s))
                        </p>`
                    }
                </div>
            </div>
        </div>
        `;
    }
    
    // Build Product pricing info similar to Product Detail page
    let productPricingHtml = '';
    if (feedback.product) {
        const canRent = feedback.product.rentalStatus === 'Available' && feedback.product.pricePerDay > 0 && feedback.product.rentalQuantity > 0;
        const canPurchase = feedback.product.purchaseStatus === 'Available' && feedback.product.purchasePrice > 0 && feedback.product.purchaseQuantity > 0;
        
        productPricingHtml = `
            <div style="margin-top: 0.5rem; padding: 0.75rem; background: #f5f5f5; border-radius: 4px;">
                ${canRent ? `
                    <div style="margin-bottom: 0.5rem;">
                        <span style="color: #666; font-size: 0.875rem;">🔄 Rental:</span>
                        <span style="font-weight: 600; color: #7c3aed; margin-left: 0.5rem;">₫${feedback.product.pricePerDay.toLocaleString('vi-VN')}/day</span>
                        <span style="color: #999; font-size: 0.75rem; margin-left: 0.5rem;">(${feedback.product.rentalQuantity} available)</span>
                    </div>
                ` : ''}
                ${canPurchase ? `
                    <div>
                        <span style="color: #666; font-size: 0.875rem;">🛒 Purchase:</span>
                        <span style="font-weight: 600; color: #16a34a; margin-left: 0.5rem;">₫${feedback.product.purchasePrice.toLocaleString('vi-VN')}</span>
                        <span style="color: #999; font-size: 0.75rem; margin-left: 0.5rem;">(${feedback.product.purchaseQuantity} available)</span>
                    </div>
                ` : ''}
            </div>
        `;
    }
    
    content.innerHTML = `
        ${feedback.product ? `
        <div class="detail-section">
            <h3>
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <rect x="2" y="7" width="20" height="14" rx="2" ry="2"></rect>
                    <path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16"></path>
                </svg>
                Product Information
            </h3>
            <div class="product-detail-card">
                <img src="${feedback.product.imageUrl || '/images/placeholder.png'}" 
                     alt="${feedback.product.productName}" 
                     class="product-detail-image">
                <div style="flex: 1;">
                    <h4 style="margin: 0 0 0.5rem 0;">${feedback.product.productName}</h4>
                    <p style="color: #666; margin: 0 0 0.5rem 0;">${feedback.product.description || ''}</p>
                    ${productPricingHtml}
                    <p style="margin: 0.5rem 0 0 0;"><strong>Provider:</strong> ${feedback.product.providerName}</p>
                    <p style="margin: 0;"><strong>Rating:</strong> ${feedback.product.averageRating.toFixed(1)} ⭐ (${feedback.product.totalReviews} reviews)</p>
                </div>
            </div>
        </div>
        ` : ''}
        
        ${orderItemHtml}
        
        <div class="detail-section">
            <h3>
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
                    <circle cx="12" cy="7" r="4"></circle>
                </svg>
                Customer Information
            </h3>
            <div class="customer-detail-card">
                <img src="${feedback.customer.profilePicture || '/images/default-avatar.png'}" 
                     alt="${feedback.customer.customerName}" 
                     class="customer-detail-avatar">
                <div>
                    <h4 style="margin: 0 0 0.25rem 0;">${feedback.customer.customerName}</h4>
                    <p style="color: #666; margin: 0;">${feedback.customer.email || ''}</p>
                    <p style="color: #999; font-size: 0.875rem; margin: 0.25rem 0 0 0;">
                        Submitted: ${formatDate(feedback.customer.submittedAt)}
                    </p>
                </div>
            </div>
        </div>
        
        <div class="detail-section">
            <h3>
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"></polygon>
                </svg>
                Rating & Review
            </h3>
            <div class="feedback-content-box">
                <div class="feedback-rating-large">${'⭐'.repeat(feedback.rating)} (${feedback.rating}/5)</div>
                <p class="feedback-comment-text">${feedback.comment || 'No comment provided'}</p>
                ${feedback.status.isFlagged ? `
                    <div class="flagged-warning">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="width: 20px; height: 20px;">
                            <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path>
                            <line x1="12" y1="9" x2="12" y2="13"></line>
                            <line x1="12" y1="17" x2="12.01" y2="17"></line>
                        </svg>
                        Contains flagged content
                    </div>
                ` : ''}
            </div>
        </div>
        
        ${feedback.providerResponse ? `
        <div class="detail-section">
            <h3>
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z"></path>
                </svg>
                Provider Response
            </h3>
            <div class="provider-response-box">
                <p class="provider-response-text">${feedback.providerResponse.responseText}</p>
                <p class="provider-response-meta">
                    Responded by ${feedback.providerResponse.responderName} • ${formatDate(feedback.providerResponse.respondedAt)}
                </p>
            </div>
        </div>
        ` : ''}
        
        <div class="detail-section">
            <h3>
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <circle cx="12" cy="12" r="10"></circle>
                    <polyline points="12 6 12 12 16 14"></polyline>
                </svg>
                Status Information
            </h3>
            <div class="status-info-grid">
                <div class="status-info-card ${feedback.status.visibility.includes('Visible') ? 'visible' : 'hidden'}">
                    <div class="status-info-label">Visibility</div>
                    <div class="status-info-value">${feedback.status.visibility}</div>
                </div>
                <div class="status-info-card ${feedback.status.contentStatus.includes('Clear') ? 'clear' : 'flagged'}">
                    <div class="status-info-label">Content Status</div>
                    <div class="status-info-value">${feedback.status.contentStatus}</div>
                </div>
                <div class="status-info-card ${feedback.status.responseStatus.includes('Responded') ? 'responded' : 'no-response'}">
                    <div class="status-info-label">Response Status</div>
                    <div class="status-info-value">${feedback.status.responseStatus}</div>
                </div>
            </div>
            ${feedback.status.isBlocked ? `
                <p style="color: #d32f2f; margin-top: 1rem;">
                    <strong>Blocked:</strong> ${formatDate(feedback.status.blockedAt)} by ${feedback.status.blockedByName}
                </p>
            ` : ''}
        </div>
        
        <div class="modal-actions">
            <button class="btn btn-secondary" onclick="closeModal()">Close</button>
            ${feedback.status.isBlocked ? `
                <button class="btn btn-success" onclick="unblockFeedback('${feedback.feedbackId}', true)">
                    ✅ Unblock
                </button>
            ` : `
                <button class="btn btn-danger" onclick="blockFeedback('${feedback.feedbackId}', true)">
                    🚫 Block
                </button>
            `}
        </div>
    `;
}

async function blockFeedback(feedbackId, fromModal = false) {
    console.log('blockFeedback called with feedbackId:', feedbackId);
    console.log('Full URL:', `${API_BASE_URL}/${feedbackId}/block`);
    
    if (!confirm('Are you sure you want to block this feedback and add it to the blacklist?')) {
        return;
    }
    
    try {
        // Try to get token from multiple sources
        const token = localStorage.getItem('AccessToken')
                   || localStorage.getItem('token')
                   || sessionStorage.getItem('AccessToken')
                   || getCookie('AccessToken');
        
        if (!token) {
            showNotification('Authentication token not found. Please login again.', 'error');
            return;
        }
        
        const url = `${API_BASE_URL}/${feedbackId}/block`;
        console.log('Fetching URL:', url);
        console.log('Token:', token ? 'Found' : 'Not found');
        
        const response = await fetch(url, {
            method: 'PUT',
            headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        });
        
        if (!response.ok) throw new Error('Failed to block feedback');
        
        showNotification('Feedback blocked successfully', 'success');
        
        if (fromModal) {
            closeModal();
        }
        
        loadStatistics();
        loadFeedbacks();
    } catch (error) {
        console.error('Error blocking feedback:', error);
        showNotification('Failed to block feedback', 'error');
    }
}

async function unblockFeedback(feedbackId, fromModal = false) {
    if (!confirm('Are you sure you want to unblock this feedback?')) {
        return;
    }
    
    try {
        // Try to get token from multiple sources
        const token = localStorage.getItem('AccessToken')
                   || localStorage.getItem('token')
                   || sessionStorage.getItem('AccessToken')
                   || getCookie('AccessToken');
        
        if (!token) {
            showNotification('Authentication token not found. Please login again.', 'error');
            return;
        }
        
        const response = await fetch(`${API_BASE_URL}/${feedbackId}/unblock`, {
            method: 'PUT',
            headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        });
        
        if (!response.ok) throw new Error('Failed to unblock feedback');
        
        showNotification('Feedback unblocked successfully', 'success');
        
        if (fromModal) {
            closeModal();
        }
        
        loadStatistics();
        loadFeedbacks();
    } catch (error) {
        console.error('Error unblocking feedback:', error);
        showNotification('Failed to unblock feedback', 'error');
    }
}

function renderPagination(page, pageSize, totalItems) {
    const totalPages = Math.ceil(totalItems / pageSize);
    const container = document.getElementById('paginationContainer');
    
    if (totalPages <= 1) {
        container.innerHTML = '';
        return;
    }
    
    let html = `
        <button class="pagination-btn" onclick="changePage(${page - 1})" ${page === 1 ? 'disabled' : ''}>
            Previous
        </button>
    `;
    
    // Show page numbers
    for (let i = 1; i <= totalPages; i++) {
        if (i === 1 || i === totalPages || (i >= page - 2 && i <= page + 2)) {
            html += `
                <button class="pagination-btn ${i === page ? 'active' : ''}" 
                        onclick="changePage(${i})">
                    ${i}
                </button>
            `;
        } else if (i === page - 3 || i === page + 3) {
            html += '<span class="pagination-info">...</span>';
        }
    }
    
    html += `
        <button class="pagination-btn" onclick="changePage(${page + 1})" ${page === totalPages ? 'disabled' : ''}>
            Next
        </button>
        <span class="pagination-info">
            Showing ${(page - 1) * pageSize + 1}-${Math.min(page * pageSize, totalItems)} of ${totalItems}
        </span>
    `;
    
    container.innerHTML = html;
}

function changePage(page) {
    currentPage = page;
    loadFeedbacks();
    window.scrollTo({ top: 0, behavior: 'smooth' });
}

function closeModal() {
    document.getElementById('feedbackDetailModal').style.display = 'none';
}

function formatDate(dateString) {
    const date = new Date(dateString);
    const options = { 
        year: 'numeric', 
        month: 'short', 
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    };
    return date.toLocaleDateString('en-US', options);
}

function formatCurrency(amount) {
    return new Intl.NumberFormat('vi-VN', {
        style: 'currency',
        currency: 'VND',
        minimumFractionDigits: 0,
        maximumFractionDigits: 0
    }).format(amount);
}

function showNotification(message, type = 'info') {
    // Create toast container if not exists
    let container = document.getElementById('toast-container');
    if (!container) {
        container = document.createElement('div');
        container.id = 'toast-container';
        container.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            z-index: 9999;
            display: flex;
            flex-direction: column;
            gap: 10px;
            max-width: 400px;
        `;
        document.body.appendChild(container);
    }

    // Create toast element
    const toast = document.createElement('div');
    toast.style.cssText = `
        background: ${type === 'error' ? '#fee2e2' : type === 'success' ? '#d1fae5' : '#dbeafe'};
        color: ${type === 'error' ? '#991b1b' : type === 'success' ? '#065f46' : '#1e40af'};
        padding: 16px 20px;
        border-radius: 8px;
        box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        border-left: 4px solid ${type === 'error' ? '#dc2626' : type === 'success' ? '#10b981' : '#3b82f6'};
        display: flex;
        align-items: flex-start;
        gap: 12px;
        animation: slideIn 0.3s ease-out;
        max-width: 100%;
        word-wrap: break-word;
        font-size: 14px;
        line-height: 1.5;
    `;

    // Add icon
    const icon = document.createElement('span');
    icon.style.cssText = 'flex-shrink: 0; font-size: 20px;';
    icon.innerHTML = type === 'error' ? '⚠️' : type === 'success' ? '✅' : 'ℹ️';

    // Add message
    const messageEl = document.createElement('div');
    messageEl.style.cssText = 'flex: 1; word-break: break-word;';
    messageEl.textContent = message;

    // Add close button
    const closeBtn = document.createElement('button');
    closeBtn.innerHTML = '×';
    closeBtn.style.cssText = `
        background: none;
        border: none;
        font-size: 24px;
        line-height: 1;
        cursor: pointer;
        color: inherit;
        opacity: 0.6;
        padding: 0;
        margin-left: 8px;
        flex-shrink: 0;
    `;
    closeBtn.onmouseover = () => closeBtn.style.opacity = '1';
    closeBtn.onmouseout = () => closeBtn.style.opacity = '0.6';
    closeBtn.onclick = () => removeToast(toast);

    toast.appendChild(icon);
    toast.appendChild(messageEl);
    toast.appendChild(closeBtn);
    container.appendChild(toast);

    // Auto remove after 6 seconds (increased from typical 3s)
    setTimeout(() => removeToast(toast), 6000);
}

function removeToast(toast) {
    toast.style.animation = 'slideOut 0.3s ease-in';
    setTimeout(() => {
        if (toast.parentElement) {
            toast.parentElement.removeChild(toast);
        }
    }, 300);
}

// Add CSS animations
if (!document.getElementById('toast-animations')) {
    const style = document.createElement('style');
    style.id = 'toast-animations';
    style.textContent = `
        @keyframes slideIn {
            from {
                transform: translateX(400px);
                opacity: 0;
            }
            to {
                transform: translateX(0);
                opacity: 1;
            }
        }
        @keyframes slideOut {
            from {
                transform: translateX(0);
                opacity: 1;
            }
            to {
                transform: translateX(400px);
                opacity: 0;
            }
        }
    `;
    document.head.appendChild(style);
}

function getCookie(name) {
    const value = `; ${document.cookie}`;
    const parts = value.split(`; ${name}=`);
    console.log('getCookie called for:', name);
    console.log('All cookies:', document.cookie);
    console.log('Parts length:', parts.length);
    if (parts.length === 2) {
        const token = parts.pop().split(';').shift();
        console.log('Token found:', token ? 'Yes' : 'No');
        return token;
    }
    console.log('Token not found in cookies');
    return null;
}


// Image Lightbox Functions
function openImageLightbox(imageSrc, caption) {
    const lightbox = document.getElementById('imageLightbox');
    const lightboxImg = document.getElementById('lightboxImage');
    const lightboxCaption = document.getElementById('lightboxCaption');
    
    lightbox.style.display = 'block';
    lightboxImg.src = imageSrc;
    lightboxCaption.textContent = caption || '';
    
    // Prevent body scroll when lightbox is open
    document.body.style.overflow = 'hidden';
}

function closeImageLightbox() {
    const lightbox = document.getElementById('imageLightbox');
    lightbox.style.display = 'none';
    document.body.style.overflow = 'auto';
}

// Setup lightbox event listeners
document.addEventListener('DOMContentLoaded', function() {
    // Close lightbox when clicking the X button
    const closeBtn = document.querySelector('.lightbox-close');
    if (closeBtn) {
        closeBtn.addEventListener('click', closeImageLightbox);
    }
    
    // Close lightbox when clicking outside the image
    const lightbox = document.getElementById('imageLightbox');
    if (lightbox) {
        lightbox.addEventListener('click', function(event) {
            if (event.target === lightbox) {
                closeImageLightbox();
            }
        });
    }
    
    // Close lightbox with Escape key
    document.addEventListener('keydown', function(event) {
        if (event.key === 'Escape') {
            closeImageLightbox();
        }
    });
});

// Add click handlers to images (using event delegation)
document.addEventListener('click', function(event) {
    // Handle product images in table
    if (event.target.classList.contains('product-image')) {
        const productName = event.target.alt || 'Product Image';
        openImageLightbox(event.target.src, productName);
    }
    
    // Handle customer avatars in table
    if (event.target.classList.contains('customer-avatar')) {
        const customerName = event.target.alt || 'Customer Avatar';
        openImageLightbox(event.target.src, customerName);
    }
    
    // Handle product images in detail modal
    if (event.target.classList.contains('product-detail-image')) {
        const productName = event.target.alt || 'Product Image';
        openImageLightbox(event.target.src, productName);
    }
    
    // Handle customer avatars in detail modal
    if (event.target.classList.contains('customer-detail-avatar')) {
        const customerName = event.target.alt || 'Customer Avatar';
        openImageLightbox(event.target.src, customerName);
    }
});
