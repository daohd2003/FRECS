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