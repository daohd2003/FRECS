// Active Users Dashboard - Real-time tracking using SignalR
(function() {
    'use strict';

    let activeUsersConnection = null;
    let activeUsersData = [];

    // Initialize SignalR connection
    async function initializeActiveUsersHub() {
        try {
            const apiBaseUrl = window.adminChatConfig?.signalRRootUrl || window.apiSettings?.rootUrl || 'https://localhost:7256';
            const accessToken = window.adminChatConfig?.accessToken || getCookie('AccessToken');
            
            if (!accessToken) {
                showEmptyState('Authentication required');
                return;
            }

            activeUsersConnection = new signalR.HubConnectionBuilder()
                .withUrl(`${apiBaseUrl}/activeUsersHub`, {
                    accessTokenFactory: () => accessToken
                })
                .withAutomaticReconnect()
                .configureLogging(signalR.LogLevel.Information)
                .build();

            // Event handlers
            activeUsersConnection.on("ActiveUsersUpdate", handleActiveUsersUpdate);
            activeUsersConnection.on("UserConnected", handleUserConnected);
            activeUsersConnection.on("UserDisconnected", handleUserDisconnected);

            // Connection state handlers
            activeUsersConnection.onreconnecting(() => {
                updateConnectionStatus('connecting');
            });

            activeUsersConnection.onreconnected(() => {
                updateConnectionStatus('connected');
                requestActiveUsers();
            });

            activeUsersConnection.onclose(() => {
                updateConnectionStatus('disconnected');
            });

            // Start connection
            await activeUsersConnection.start();
            updateConnectionStatus('connected');

            // Request initial active users list
            await requestActiveUsers();

        } catch (error) {
            console.error('[ActiveUsers] Failed to connect:', error);
            updateConnectionStatus('disconnected');
            showEmptyState('Failed to connect');
        }
    }

    // Request active users from server
    async function requestActiveUsers() {
        if (activeUsersConnection && activeUsersConnection.state === signalR.HubConnectionState.Connected) {
            try {
                const users = await activeUsersConnection.invoke("GetActiveUsers");
                handleActiveUsersUpdate(users);
            } catch (error) {
                console.error('[ActiveUsers] Failed to get active users:', error);
            }
        }
    }

    // Handle full active users update
    function handleActiveUsersUpdate(users) {
        activeUsersData = users || [];
        renderActiveUsers();
        updateActiveUsersCount();
    }

    // Handle new user connected
    function handleUserConnected(userInfo) {
        // Check if user already exists (update)
        const existingIndex = activeUsersData.findIndex(u => u.userId === userInfo.userId);
        if (existingIndex >= 0) {
            activeUsersData[existingIndex] = mapUserInfo(userInfo);
        } else {
            activeUsersData.unshift(mapUserInfo(userInfo));
        }
        
        renderActiveUsers();
        updateActiveUsersCount();
        
        // Show notification animation
        showUserConnectedAnimation(userInfo);
    }

    // Handle user disconnected
    function handleUserDisconnected(userId) {
        activeUsersData = activeUsersData.filter(u => u.userId !== userId);
        renderActiveUsers();
        updateActiveUsersCount();
    }

    // Map UserConnectionInfo to ActiveUserDto format
    function mapUserInfo(userInfo) {
        return {
            userId: userInfo.userId,
            fullName: userInfo.fullName || 'Unknown User',
            email: userInfo.email || '',
            role: userInfo.role || 'customer',
            avatarUrl: userInfo.avatarUrl,
            status: 'online',
            onlineDuration: 'Just now'
        };
    }

    // Render active users list
    function renderActiveUsers() {
        const container = document.getElementById('activeUsersContainer');
        if (!container) return;

        if (activeUsersData.length === 0) {
            showEmptyState('No users online');
            return;
        }

        const html = activeUsersData.map(user => renderUserItem(user)).join('');
        container.innerHTML = html;
    }

    // Render individual user item
    function renderUserItem(user) {
        const avatarUrl = user.avatarUrl || `https://ui-avatars.com/api/?name=${encodeURIComponent(user.fullName)}&background=667eea&color=fff&size=128`;
        const roleClass = (user.role || 'customer').toLowerCase();
        const statusClass = (user.status || 'online').toLowerCase();

        return `
            <div class="active-user-item" data-user-id="${user.userId}">
                <div class="user-avatar-wrapper">
                    <img src="${avatarUrl}" alt="${user.fullName}" class="user-avatar" onerror="this.src='https://ui-avatars.com/api/?name=${encodeURIComponent(user.fullName)}&background=667eea&color=fff&size=128'">
                    <div class="user-status-indicator ${statusClass}"></div>
                </div>
                <div class="user-info-section">
                    <div class="user-name">${escapeHtml(user.fullName)}</div>
                    <div class="user-meta">
                        <span class="user-role-badge ${roleClass}">${user.role}</span>
                        <span class="user-online-time">${user.onlineDuration || 'Just now'}</span>
                    </div>
                </div>
            </div>
        `;
    }

    // Show empty state
    function showEmptyState(message = 'No users online') {
        const container = document.getElementById('activeUsersContainer');
        if (!container) return;

        container.innerHTML = `
            <div class="empty-active-users">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
                    <circle cx="9" cy="7" r="4"></circle>
                    <path d="M23 21v-2a4 4 0 0 0-3-3.87"></path>
                    <path d="M16 3.13a4 4 0 0 1 0 7.75"></path>
                </svg>
                <p>${message}</p>
            </div>
        `;
    }

    // Update active users count badge
    function updateActiveUsersCount() {
        const badge = document.getElementById('activeUsersCount');
        if (badge) {
            badge.textContent = activeUsersData.length;
        }
    }

    // Update connection status indicator
    function updateConnectionStatus(status) {
        const statusEl = document.getElementById('connectionStatus');
        if (!statusEl) return;

        statusEl.className = `connection-status ${status}`;
        
        const statusText = {
            'connecting': 'Connecting...',
            'connected': 'Connected',
            'disconnected': 'Disconnected'
        };

        const textSpan = statusEl.querySelector('span');
        if (textSpan) {
            textSpan.textContent = statusText[status] || 'Unknown';
        }
    }

    // Show user connected animation (subtle notification)
    function showUserConnectedAnimation(userInfo) {
        // Find the user item in DOM and add animation class
        const userItem = document.querySelector(`[data-user-id="${userInfo.userId}"]`);
        if (userItem) {
            userItem.style.animation = 'fadeInSlide 0.5s ease-out';
        }
    }

    // Helper: Get cookie value
    function getCookie(name) {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return null;
    }

    // Helper: Escape HTML
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Periodically update "online duration" (every 30 seconds)
    setInterval(() => {
        if (activeUsersData.length > 0) {
            activeUsersData.forEach(user => {
                if (user.connectedAt) {
                    const now = new Date();
                    const connected = new Date(user.connectedAt);
                    const minutes = Math.floor((now - connected) / 60000);
                    
                    if (minutes < 1) user.onlineDuration = 'Just now';
                    else if (minutes < 60) user.onlineDuration = `${minutes}m ago`;
                    else if (minutes < 1440) user.onlineDuration = `${Math.floor(minutes / 60)}h ago`;
                    else user.onlineDuration = `${Math.floor(minutes / 1440)}d ago`;
                }
            });
            renderActiveUsers();
        }
    }, 30000); // Update every 30 seconds

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeActiveUsersHub);
    } else {
        initializeActiveUsersHub();
    }

    // Cleanup on page unload
    window.addEventListener('beforeunload', () => {
        if (activeUsersConnection) {
            activeUsersConnection.stop();
        }
    });

})();

// CSS animation for fade in slide
const style = document.createElement('style');
style.textContent = `
    @keyframes fadeInSlide {
        from {
            opacity: 0;
            transform: translateX(-20px);
        }
        to {
            opacity: 1;
            transform: translateX(0);
        }
    }
`;
document.head.appendChild(style);

