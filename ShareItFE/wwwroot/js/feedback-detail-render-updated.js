// Helper function to generate default avatar (same as User Management)
function getDefaultAvatar(name, email) {
    const text = name || email || 'U';
    const hash = hashCode(text);
    const colors = ['#8b5cf6', '#3b82f6', '#10b981', '#f59e0b', '#ef4444'];
    const color = colors[Math.abs(hash) % colors.length];
    const initial = text.charAt(0).toUpperCase();
    
    return `data:image/svg+xml,${encodeURIComponent(`
        <svg width="40" height="40" xmlns="http://www.w3.org/2000/svg">
            <rect width="40" height="40" fill="${color}"/>
            <text x="20" y="26" text-anchor="middle" fill="white" font-family="Arial" font-size="16" font-weight="bold">${initial}</text>
        </svg>
    `)}`;
}

function hashCode(str) {
    let hash = 0;
    for (let i = 0; i < str.length; i++) {
        const char = str.charCodeAt(i);
        hash = ((hash << 5) - hash) + char;
        hash = hash & hash;
    }
    return hash;
}

// Updated renderFeedbackDetail function
function renderFeedbackDetail(feedback) {
    console.log('Feedback Detail Data:', feedback);
    console.log('Customer Data:', feedback.customer);
    console.log('ProfilePicture:', feedback.customer?.profilePicture);
    
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
                <img src="${feedback.customer.profilePicture || getDefaultAvatar(feedback.customer.customerName, feedback.customer.email)}" 
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
