document.addEventListener('DOMContentLoaded', function () {
    const notificationMenu = document.querySelector('.notification-menu');
    if (!notificationMenu) return;

    const notificationButton = document.getElementById('notificationButton');
    const notificationDropdown = document.getElementById('notificationDropdown');
    const notificationCountBadge = document.getElementById('notificationCount');
    const markAllReadBtn = document.getElementById('markAllReadBtn');
    const notificationList = document.getElementById('notificationList');
    const userId = notificationMenu.dataset.userid;
    const token = notificationMenu.dataset.token;

    const authHeaders = {
        'Content-Type': 'application/json',
    };
    // Chỉ thêm Authorization header nếu token thực sự tồn tại
    if (token) {
        authHeaders['Authorization'] = `Bearer ${token}`;
    }

    // Toggle dropdown
    notificationButton.addEventListener('click', function (event) {
        event.stopPropagation();
        const isVisible = notificationDropdown.style.display === 'block';
        notificationDropdown.style.display = isVisible ? 'none' : 'block';
        
        // Load fresh notifications when opening dropdown
        if (!isVisible && userId) {
            console.log('Loading fresh notifications on dropdown open...');
            loadNotifications(userId);
        }
    });

    // Close dropdown when clicking outside
    document.addEventListener('click', function (event) {
        if (!notificationMenu.contains(event.target)) {
            notificationDropdown.style.display = 'none';
        }
    });

    function updateCountBadge(newCount) {
        if (newCount > 0) {
            notificationCountBadge.textContent = newCount;
            notificationCountBadge.style.display = 'inline-block';
        } else {
            notificationCountBadge.style.display = 'none';
        }
    }

    // Function to reload notifications - exposed globally
    function loadNotifications(userIdParam = null) {
        const targetUserId = userIdParam || userId;
        if (!targetUserId) return;

        // Reload unread count
        fetch(`${apiRootUrl}/api/notification/unread-count/${targetUserId}`, {
            headers: authHeaders
        })
        .then(response => response.json())
        .then(data => {
            if (data && typeof data.data === 'number') {
                updateCountBadge(data.data);
            }
        })
        .catch(error => {
            console.error('Failed to reload notification count:', error);
        });

        // Always reload notification list to keep it updated
        fetch(`${apiRootUrl}/api/notification/user/${targetUserId}?unreadOnly=false`, {
            headers: authHeaders
        })
        .then(response => response.json())
        .then(data => {
            if (data && data.data) {
                console.log('Notifications reloaded:', data.data.length);
                // Debug: Log first notification's createdAt to see format
                if (data.data.length > 0) {
                    console.log('First notification CreatedAt:', data.data[0].createdAt);
                }
                updateNotificationList(data.data);
            }
        })
        .catch(error => {
            console.error('Failed to reload notifications:', error);
        });
    }

    // Function to update notification list DOM
    function updateNotificationList(notifications) {
        if (!notificationList) return;
        
        if (!notifications || notifications.length === 0) {
            notificationList.innerHTML = '<p class="no-notifications">You have no notifications.</p>';
            return;
        }
        
        let html = '';
        notifications.forEach(notification => {
            const isUnread = !notification.isRead;
            const unreadClass = isUnread ? 'unread' : '';
            const unreadDot = isUnread ? '<div class="unread-dot"></div>' : '';
            
            if (notification.orderId && notification.orderId !== '00000000-0000-0000-0000-000000000000') {
                // Notification with order link
                html += `
                    <a href="/Order/Details/${notification.orderId}" 
                       class="notification-item ${unreadClass}" 
                       data-id="${notification.id}">
                        <div class="notification-content">
                            <p class="notification-message">${notification.message}</p>
                            <span class="notification-time">${formatNotificationTime(notification.createdAt)}</span>
                        </div>
                        ${unreadDot}
                    </a>
                `;
            } else {
                // Notification without order link
                html += `
                    <div class="notification-item ${unreadClass}" 
                         data-id="${notification.id}">
                        <div class="notification-content">
                            <p class="notification-message">${notification.message}</p>
                            <span class="notification-time">${formatNotificationTime(notification.createdAt)}</span>
                        </div>
                        ${unreadDot}
                    </div>
                `;
            }
        });
        
        notificationList.innerHTML = html;
        console.log('Notification list DOM updated with', notifications.length, 'notifications');
    }
    
    // Helper function to format notification time
    function formatNotificationTime(dateString) {
        try {
            // Ensure the datetime string is treated as UTC by adding 'Z' if not present
            let utcDateString = dateString;
            if (!dateString.includes('Z') && !dateString.includes('+')) {
                utcDateString = dateString.replace(/\.\d{3}$/, '') + 'Z';
            }
            
            const date = new Date(utcDateString);
            
            // Format with Vietnam timezone (UTC+7)
            const formatted = date.toLocaleString('vi-VN', {
                timeZone: 'Asia/Ho_Chi_Minh',
                year: 'numeric',
                month: '2-digit',
                day: '2-digit',
                hour: '2-digit',
                minute: '2-digit',
                hour12: false
            });
            
            // Debug: Log conversion
            console.log('DateTime conversion:', { 
                original: dateString, 
                utcString: utcDateString, 
                parsed: date.toISOString(), 
                formatted: formatted 
            });
            
            return formatted;
        } catch (e) {
            console.error('Error formatting notification time:', e, 'Original dateString:', dateString);
            return dateString; // Fallback to original string if parsing fails
        }
    }

    // Expose functions globally so they can be called from other scripts
    window.loadNotifications = loadNotifications;
    window.updateNotificationList = updateNotificationList;

    // Mark all as read
    markAllReadBtn.addEventListener('click', function () {
        if (!userId) return;

        fetch(`${apiRootUrl}/api/notification/mark-all-read/${userId}`, {
            method: 'PUT',
            headers: authHeaders
        })
            .then(response => {
                if (response.ok) {
                    document.querySelectorAll('.notification-item.unread').forEach(item => {
                        item.classList.remove('unread');
                        const dot = item.querySelector('.unread-dot');
                        if (dot) dot.remove();
                    });
                    updateCountBadge(0);
                } else {
                    console.error('Failed to mark all notifications as read. Status:', response.status);
                }
            });
    });

    // Mark one as read when clicking on it
    notificationList.addEventListener('click', function (event) {
        const item = event.target.closest('.notification-item');
        if (item && item.classList.contains('unread')) {
            const notificationId = item.dataset.id;

            fetch(`${apiRootUrl}/api/notification/mark-read/${notificationId}`, {
                method: 'PUT',
                headers: authHeaders
            })
                .then(response => {
                    if (response.ok) {
                        item.classList.remove('unread');
                        const dot = item.querySelector('.unread-dot');
                        if (dot) dot.remove();

                        let currentCount = parseInt(notificationCountBadge.textContent || '0');
                        updateCountBadge(Math.max(0, currentCount - 1));
                    } else {
                        console.error('Failed to mark notification as read. Status:', response.status);
                    }
                });
        }
    });

    // Phần polling cũng cần sửa tương tự nếu bạn sử dụng
    // setInterval(function() {
    //     if (!userId) return;
    //     fetch(`${apiRootUrl}/api/notification/unread-count/${userId}`, { credentials: 'include' }) // Thêm 'credentials'
    //        .then(...)
    // }, 30000);
});