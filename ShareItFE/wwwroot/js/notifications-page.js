// Notifications Page Management
(function() {
    'use strict';

    // Prevent double initialization
    if (window.notificationPageInitialized) {
        return;
    }
    window.notificationPageInitialized = true;

    const config = window.notificationPageConfig || {};
    const userId = config.userId;
    const userRole = config.userRole;
    // Remove trailing slash from apiBaseUrl if exists
    const apiBaseUrl = (config.apiBaseUrl || '').replace(/\/$/, '');
    const accessToken = config.accessToken;

    // State
    let currentPage = 1;
    let pageSize = 10;
    let totalPages = 0;
    let totalCount = 0;
    let searchTerm = '';
    let filterStatus = '';

    // DOM Elements (will be initialized in init())
    let searchInput;
    let filterStatusSelect;
    let notificationsContainer;
    let paginationContainer;
    let markAllReadBtn;

    // Initialize
    function init() {
        // Get DOM elements
        searchInput = document.getElementById('searchInput');
        filterStatusSelect = document.getElementById('filterStatus');
        notificationsContainer = document.getElementById('notificationsContainer');
        paginationContainer = document.getElementById('paginationContainer');
        markAllReadBtn = document.getElementById('markAllReadBtnPage');

        if (!userId || !apiBaseUrl) {
            console.error('Missing required configuration');
            showError('Configuration error. Please refresh the page.');
            return;
        }

        if (!accessToken) {
            showError('Please log in to view notifications.');
            return;
        }

        setupEventListeners();
        loadNotifications();
    }

    // Setup Event Listeners
    function setupEventListeners() {
        // Search with debounce
        let searchTimeout;
        searchInput.addEventListener('input', (e) => {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(() => {
                searchTerm = e.target.value.trim();
                currentPage = 1;
                loadNotifications();
            }, 500);
        });

        // Status filter
        filterStatusSelect.addEventListener('change', (e) => {
            filterStatus = e.target.value;
            currentPage = 1;
            loadNotifications();
        });

        // Mark all as read
        if (markAllReadBtn) {
            markAllReadBtn.addEventListener('click', (e) => {
                e.preventDefault();
                markAllAsRead();
            });
        }
    }

    // Load Notifications from API
    async function loadNotifications() {
        showLoading();

        try {
            // Build query parameters
            const params = new URLSearchParams({
                page: currentPage,
                pageSize: pageSize
            });

            if (searchTerm) params.append('searchTerm', searchTerm);
            if (filterStatus === 'unread') params.append('isRead', 'false');
            if (filterStatus === 'read') params.append('isRead', 'true');

            const url = `${apiBaseUrl}/notification/paged/${userId}?${params}`;

            const response = await fetch(url, {
                headers: {
                    'Authorization': `Bearer ${accessToken}`,
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const result = await response.json();
            
            // ApiResponse structure: { message: string, data: T }
            if (result.data) {
                const pagedData = result.data;
                totalPages = pagedData.totalPages || 1;
                totalCount = pagedData.totalCount || 0;
                
                renderNotifications(pagedData.items || []);
                renderPagination();
            } else {
                throw new Error(result.message || 'No data returned from server');
            }
        } catch (error) {
            console.error('Error loading notifications:', error);
            showError('Failed to load notifications. Please try again.');
        }
    }

    // Render Notifications
    function renderNotifications(notifications) {
        if (!notifications || notifications.length === 0) {
            notificationsContainer.innerHTML = `
                <div class="empty-state">
                    <div class="empty-state-icon">🔔</div>
                    <h3>No notifications found</h3>
                    <p>You're all caught up!</p>
                </div>
            `;
            return;
        }

        let html = '';
        notifications.forEach(notification => {
            const unreadClass = !notification.isRead ? 'unread' : '';
            const icon = getNotificationIcon(notification.type);
            const typeClass = notification.type.toLowerCase();
            const formattedTime = formatNotificationTime(notification.createdAt);
            
            html += `
                <div class="notification-item-page ${unreadClass}" data-id="${notification.id}" data-order-id="${notification.orderId || ''}">
                    <div class="notification-icon ${typeClass}">
                        ${icon}
                    </div>
                    <div class="notification-content-wrapper">
                        <div class="notification-message">${escapeHtml(notification.message)}</div>
                        <div class="notification-meta">
                            <span class="notification-time">
                                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                    <circle cx="12" cy="12" r="10"></circle>
                                    <polyline points="12 6 12 12 16 14"></polyline>
                                </svg>
                                ${formattedTime}
                            </span>
                        </div>
                    </div>
                    <div class="notification-actions">
                        ${!notification.isRead ? `
                            <button class="btn-notification-action btn-mark-read" title="Mark as read">
                                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                    <polyline points="20 6 9 17 4 12"></polyline>
                                </svg>
                            </button>
                        ` : ''}
                        <button class="btn-notification-action btn-delete" title="Delete">
                            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <polyline points="3 6 5 6 21 6"></polyline>
                                <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
                            </svg>
                        </button>
                    </div>
                </div>
            `;
        });

        notificationsContainer.innerHTML = html;
        attachNotificationEventListeners();
    }

    // Attach Event Listeners to Notification Items
    function attachNotificationEventListeners() {
        // Click notification to navigate (if has orderId)
        document.querySelectorAll('.notification-item-page').forEach(item => {
            const orderId = item.getAttribute('data-order-id');
            const notificationId = item.getAttribute('data-id');

            item.addEventListener('click', async (e) => {
                // Don't navigate if clicking action buttons
                if (e.target.closest('.btn-notification-action')) {
                    return;
                }

                // Mark as read if unread
                if (item.classList.contains('unread')) {
                    await markAsRead(notificationId);
                }

                // Navigate to order details if orderId exists
                if (orderId && orderId !== '00000000-0000-0000-0000-000000000000') {
                    const orderUrl = userRole && userRole.toLowerCase() === 'provider'
                        ? `/provider/order/${orderId}`
                        : `/Order/Details/${orderId}`;
                    window.location.href = orderUrl;
                }
            });
        });

        // Mark as read buttons
        document.querySelectorAll('.btn-mark-read').forEach(btn => {
            btn.addEventListener('click', async (e) => {
                e.stopPropagation();
                const item = e.target.closest('.notification-item-page');
                const notificationId = item.getAttribute('data-id');
                await markAsRead(notificationId);
            });
        });

        // Delete buttons
        document.querySelectorAll('.btn-delete').forEach(btn => {
            btn.addEventListener('click', async (e) => {
                e.stopPropagation();
                const item = e.target.closest('.notification-item-page');
                const notificationId = item.getAttribute('data-id');
                
                if (confirm('Are you sure you want to delete this notification?')) {
                    await deleteNotification(notificationId);
                }
            });
        });
    }

    // Mark Single Notification as Read
    async function markAsRead(notificationId) {
        try {
            const response = await fetch(`${apiBaseUrl}/notification/mark-read/${notificationId}`, {
                method: 'PUT',
                headers: {
                    'Authorization': `Bearer ${accessToken}`,
                    'Content-Type': 'application/json'
                }
            });

            if (response.ok) {
                const item = document.querySelector(`.notification-item-page[data-id="${notificationId}"]`);
                if (item) {
                    item.classList.remove('unread');
                    const markReadBtn = item.querySelector('.btn-mark-read');
                    if (markReadBtn) {
                        markReadBtn.remove();
                    }
                    const message = item.querySelector('.notification-message');
                    if (message) {
                        message.style.fontWeight = 'normal';
                    }
                }
                
                // Update unread count in header
                if (window.loadNotifications) {
                    window.loadNotifications(userId);
                }
            } else {
                throw new Error('Failed to mark as read');
            }
        } catch (error) {
            console.error('Error marking as read:', error);
            showToast('Failed to mark as read', 'error');
        }
    }

    // Mark All as Read
    function markAllAsRead() {
        if (!userId) return;

        fetch(`${apiBaseUrl}/notification/mark-all-read/${userId}`, {
            method: 'PUT',
            headers: {
                'Authorization': `Bearer ${accessToken}`,
                'Content-Type': 'application/json'
            }
        })
        .then(response => {
            if (response.ok) {
                // Update UI immediately
                document.querySelectorAll('.notification-item-page.unread').forEach(item => {
                    item.classList.remove('unread');
                    const markReadBtn = item.querySelector('.btn-mark-read');
                    if (markReadBtn) markReadBtn.remove();
                    const message = item.querySelector('.notification-message');
                    if (message) message.style.fontWeight = 'normal';
                });
                
                showToast('All notifications marked as read', 'success');
                loadNotifications();
                
                // Update unread count in header
                if (window.loadNotifications) {
                    window.loadNotifications(userId);
                }
            } else {
                showToast('Failed to mark all as read', 'error');
            }
        })
        .catch(error => {
            console.error('Error marking all as read:', error);
            showToast('Failed to mark all as read', 'error');
        });
    }

    // Delete Notification
    async function deleteNotification(notificationId) {
        try {
            const response = await fetch(`${apiBaseUrl}/notification/${notificationId}`, {
                method: 'DELETE',
                headers: {
                    'Authorization': `Bearer ${accessToken}`,
                    'Content-Type': 'application/json'
                }
            });

            if (response.ok) {
                showToast('Notification deleted', 'success');
                
                // Remove from DOM with animation
                const item = document.querySelector(`.notification-item-page[data-id="${notificationId}"]`);
                if (item) {
                    item.style.transition = 'all 0.3s ease';
                    item.style.opacity = '0';
                    item.style.transform = 'translateX(100px)';
                    setTimeout(() => {
                        item.remove();
                        
                        // Check if no more notifications
                        const remainingItems = document.querySelectorAll('.notification-item-page');
                        if (remainingItems.length === 0) {
                            loadNotifications();
                        }
                    }, 300);
                }
                
                // Update unread count in header
                if (window.loadNotifications) {
                    window.loadNotifications(userId);
                }
            } else {
                throw new Error('Failed to delete notification');
            }
        } catch (error) {
            console.error('Error deleting notification:', error);
            showToast('Failed to delete notification', 'error');
        }
    }

    // Render Pagination (similar logic to PaginationHelper.cs)
    function renderPagination() {
        if (totalPages <= 1) {
            paginationContainer.style.display = 'none';
            return;
        }

        paginationContainer.style.display = 'flex';

        let html = `
            <button class="pagination-btn" ${currentPage === 1 ? 'disabled' : ''} onclick="window.notificationPage.goToPage(${currentPage - 1})">
                Previous
            </button>
        `;

        // Get pagination items using same logic as PaginationHelper.cs
        const paginationItems = getPaginationItems(currentPage, totalPages, 7);
        
        paginationItems.forEach(item => {
            if (item === '...') {
                html += `<span class="pagination-info">...</span>`;
            } else {
                const pageNum = item;
                html += `
                    <button class="pagination-btn ${pageNum === currentPage ? 'active' : ''}" 
                            onclick="window.notificationPage.goToPage(${pageNum})">
                        ${pageNum}
                    </button>
                `;
            }
        });

        html += `
            <button class="pagination-btn" ${currentPage === totalPages ? 'disabled' : ''} onclick="window.notificationPage.goToPage(${currentPage + 1})">
                Next
            </button>
        `;

        html += `<span class="pagination-info">Page ${currentPage} of ${totalPages} (${totalCount} total)</span>`;

        paginationContainer.innerHTML = html;
    }

    // Generate pagination items (JavaScript version of PaginationHelper.GetPaginationItems)
    // This mirrors the C# PaginationHelper logic for consistency
    function getPaginationItems(currentPage, totalPages, maxVisible = 7) {
        const items = [];

        if (totalPages <= maxVisible) {
            // Show all pages if total is small
            for (let i = 1; i <= totalPages; i++) {
                items.push(i);
            }
        } else {
            // Always show first page
            items.push(1);

            if (currentPage <= 3) {
                // Near beginning: [1] [2] [3] [4] [5] [...] [Last]
                for (let i = 2; i <= 5 && i < totalPages; i++) {
                    items.push(i);
                }
                if (5 < totalPages - 1) {
                    items.push('...');
                }
            } else if (currentPage >= totalPages - 2) {
                // Near end: [1] [...] [Last-4] [Last-3] [Last-2] [Last-1] [Last]
                items.push('...');
                for (let i = totalPages - 4; i <= totalPages - 1 && i > 1; i++) {
                    items.push(i);
                }
            } else {
                // Middle: [1] [...] [Current-1] [Current] [Current+1] [...] [Last]
                items.push('...');
                for (let i = currentPage - 1; i <= currentPage + 1; i++) {
                    if (i > 1 && i < totalPages) {
                        items.push(i);
                    }
                }
                items.push('...');
            }

            // Always show last page
            if (totalPages > 1) {
                items.push(totalPages);
            }
        }

        return items;
    }

    // Go to Page
    function goToPage(page) {
        if (page < 1 || page > totalPages || page === currentPage) return;
        currentPage = page;
        loadNotifications();
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    // Helper Functions
    function getNotificationIcon(type) {
        const icons = {
            order: '📦',
            payment: '💳',
            system: '⚙️',
            message: '💬'
        };
        return icons[type.toLowerCase()] || '🔔';
    }

    function formatNotificationTime(dateString) {
        try {
            const date = new Date(dateString);
            const now = new Date();
            const diffMs = now - date;
            const diffMins = Math.floor(diffMs / 60000);
            const diffHours = Math.floor(diffMs / 3600000);
            const diffDays = Math.floor(diffMs / 86400000);

            if (diffMins < 1) return 'Just now';
            if (diffMins < 60) return `${diffMins} minute${diffMins > 1 ? 's' : ''} ago`;
            if (diffHours < 24) return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`;
            if (diffDays < 7) return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`;

            return date.toLocaleString('vi-VN', {
                year: 'numeric',
                month: '2-digit',
                day: '2-digit',
                hour: '2-digit',
                minute: '2-digit',
                hour12: false
            });
        } catch (e) {
            return dateString;
        }
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function showLoading() {
        notificationsContainer.innerHTML = `
            <div class="loading-container">
                <div class="spinner"></div>
                <p>Loading notifications...</p>
            </div>
        `;
    }

    function showError(message) {
        notificationsContainer.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">⚠️</div>
                <h3>Error</h3>
                <p>${escapeHtml(message)}</p>
            </div>
        `;
    }

    function showToast(message, type = 'success') {
        const toast = document.createElement('div');
        toast.className = `toast-notification ${type}`;
        toast.innerHTML = `
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                ${type === 'success' 
                    ? '<polyline points="20 6 9 17 4 12"></polyline>'
                    : '<circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line>'
                }
            </svg>
            <span>${escapeHtml(message)}</span>
        `;
        document.body.appendChild(toast);

        setTimeout(() => {
            toast.style.animation = 'slideInFromRight 0.3s ease reverse';
            setTimeout(() => toast.remove(), 300);
        }, 3000);
    }

    // Expose public API
    window.notificationPage = {
        goToPage: goToPage,
        refresh: loadNotifications
    };

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

