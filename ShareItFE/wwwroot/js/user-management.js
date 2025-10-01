// User Management JavaScript
class UserManagement {
    constructor() {
        this.config = window.userManagementConfig || {};
        this.users = [];
        this.filteredUsers = [];
        this.currentTab = 'active';
        this.searchQuery = '';
        this.roleFilter = 'all';
        this.selectedUser = null;
        
        this.init();
    }

    init() {
        this.bindEvents();
        this.loadUsers();
    }

    bindEvents() {
        // Tab switching
        document.querySelectorAll('.tab-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                this.switchTab(e.target.dataset.tab);
            });
        });

        // Search functionality
        const searchInput = document.getElementById('searchInput');
        if (searchInput) {
            searchInput.addEventListener('input', (e) => {
                this.searchQuery = e.target.value.toLowerCase();
                this.filterUsers();
            });
        }

        // Role filter
        const roleFilter = document.getElementById('roleFilter');
        if (roleFilter) {
            roleFilter.addEventListener('change', (e) => {
                this.roleFilter = e.target.value;
                this.filterUsers();
            });
        }

        // Add user modal
        const addUserBtn = document.getElementById('addUserBtn');
        if (addUserBtn) {
            addUserBtn.addEventListener('click', () => {
                this.showAddUserModal();
            });
        }

        const closeAddUserModal = document.getElementById('closeAddUserModal');
        if (closeAddUserModal) {
            closeAddUserModal.addEventListener('click', () => {
                this.hideAddUserModal();
            });
        }

        const cancelAddUser = document.getElementById('cancelAddUser');
        if (cancelAddUser) {
            cancelAddUser.addEventListener('click', () => {
                this.hideAddUserModal();
            });
        }

        // Add user form
        const addUserForm = document.getElementById('addUserForm');
        if (addUserForm) {
            addUserForm.addEventListener('submit', (e) => {
                e.preventDefault();
                this.handleAddUser();
            });
        }

        // User detail modal
        const closeUserDetailModal = document.getElementById('closeUserDetailModal');
        if (closeUserDetailModal) {
            closeUserDetailModal.addEventListener('click', () => {
                this.hideUserDetailModal();
            });
        }

        // Lock/Unlock user
        const lockUnlockBtn = document.getElementById('lockUnlockBtn');
        if (lockUnlockBtn) {
            lockUnlockBtn.addEventListener('click', () => {
                this.handleLockUnlockUser();
            });
        }

        // Close modals when clicking outside
        document.addEventListener('click', (e) => {
            if (e.target.classList.contains('modal')) {
                this.hideAllModals();
            }
        });
    }

    async loadUsers() {
        try {
            this.showLoading();
            
            // Both admin and staff can access users endpoint
            const endpoint = '/users';
            
                
            const response = await fetch(`${this.config.apiBaseUrl}${endpoint}`, {
                headers: {
                    'Authorization': `Bearer ${this.config.accessToken}`,
                    'Content-Type': 'application/json'
                }
            });


            if (!response.ok) {
                let errorMessage = `HTTP ${response.status} - ${response.statusText}`;
                try {
                    const errorText = await response.text();
                    console.error('Error response:', errorText);
                    if (errorText) {
                        errorMessage += `: ${errorText}`;
                    }
                } catch (e) {
                    console.error('Could not read error response:', e);
                }
                throw new Error(errorMessage);
            }

            const data = await response.json();
            this.users = data.data || [];
            
            this.updateStats();
            this.filterUsers();
            
        } catch (error) {
            console.error('Error loading users:', error);
            this.showError('Failed to load users. Please try again.');
        } finally {
            this.hideLoading();
        }
    }

    updateStats() {
        let usersToCount = this.users;
        
        // For staff users, only count customers and providers
        if (this.config.currentUserRole === 'staff') {
            usersToCount = this.users.filter(u => u.role === 'customer' || u.role === 'provider');
        }
        
        const totalUsers = usersToCount.length;
        const activeUsers = usersToCount.filter(u => u.isActive).length;
        const inactiveUsers = usersToCount.filter(u => !u.isActive).length;
        
        // Calculate new users this month
        const currentMonth = new Date().getMonth();
        const currentYear = new Date().getFullYear();
        const newThisMonth = usersToCount.filter(u => {
            const userDate = new Date(u.createdAt);
            return userDate.getMonth() === currentMonth && userDate.getFullYear() === currentYear;
        }).length;

        document.getElementById('totalUsers').textContent = totalUsers;
        document.getElementById('activeUsers').textContent = activeUsers;
        document.getElementById('inactiveUsers').textContent = inactiveUsers;
        document.getElementById('newThisMonth').textContent = newThisMonth;
    }

    switchTab(tab) {
        this.currentTab = tab;
        
        // Update tab buttons
        document.querySelectorAll('.tab-btn').forEach(btn => {
            btn.classList.remove('active');
        });
        document.querySelector(`[data-tab="${tab}"]`).classList.add('active');
        
        this.filterUsers();
    }

    filterUsers() {
        let filtered = this.users.filter(user => {
            // Filter by status (active/inactive)
            const isActive = user.isActive;
            if (this.currentTab === 'active' && !isActive) return false;
            if (this.currentTab === 'inactive' && isActive) return false;
            
            // For staff users, only show customers and providers
            if (this.config.currentUserRole === 'staff') {
                if (user.role !== 'customer' && user.role !== 'provider') {
                    return false;
                }
            }
            
            // Filter by search query
            if (this.searchQuery) {
                const matchesSearch = 
                    (user.profile?.fullName || '').toLowerCase().includes(this.searchQuery) ||
                    user.email.toLowerCase().includes(this.searchQuery);
                if (!matchesSearch) return false;
            }
            
            // Filter by role
            if (this.roleFilter !== 'all' && user.role !== this.roleFilter) {
                return false;
            }
            
            return true;
        });

        this.filteredUsers = filtered;
        this.renderUsers();
        this.updateTabCounts();
    }

    updateTabCounts() {
        let usersToCount = this.users;
        
        // For staff users, only count customers and providers
        if (this.config.currentUserRole === 'staff') {
            usersToCount = this.users.filter(u => u.role === 'customer' || u.role === 'provider');
        }
        
        const activeCount = usersToCount.filter(u => u.isActive).length;
        const inactiveCount = usersToCount.filter(u => !u.isActive).length;
        
        document.getElementById('activeCount').textContent = activeCount;
        document.getElementById('inactiveCount').textContent = inactiveCount;
    }

    renderUsers() {
        const tbody = document.getElementById('usersTableBody');
        const emptyState = document.getElementById('emptyState');
        
        if (this.filteredUsers.length === 0) {
            tbody.innerHTML = '';
            emptyState.style.display = 'block';
            return;
        }
        
        emptyState.style.display = 'none';
        tbody.innerHTML = this.filteredUsers.map(user => this.renderUserRow(user)).join('');
        
        // Bind row events
        this.bindRowEvents();
    }

    renderUserRow(user) {
        const profile = user.profile || {};
        const avatarUrl = profile.avatarUrl || this.getDefaultAvatar(user.email);
        const fullName = profile.fullName || 'Unknown User';
        const phone = profile.phone || 'N/A';
        
        return `
            <tr>
                <td>
                    <div class="user-info">
                        <img src="${avatarUrl}" alt="${fullName}" class="user-avatar">
                        <div class="user-details">
                            <div class="user-name">${fullName}</div>
                            <div class="user-id">ID: ${user.id}</div>
                        </div>
                    </div>
                </td>
                <td>
                    <div class="contact-info">
                        <div class="contact-item">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"></path>
                                <polyline points="22,6 12,13 2,6"></polyline>
                            </svg>
                            <span class="contact-email">${user.email}</span>
                        </div>
                        <div class="contact-item">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z"></path>
                            </svg>
                            <span class="contact-phone">${phone}</span>
                        </div>
                    </div>
                </td>
                <td>
                    <div class="role-status">
                        <span class="badge badge-${user.role || 'unknown'}">${this.capitalizeFirst(user.role || 'Unknown')}</span>
                        <span class="badge badge-${user.isActive ? 'active' : 'inactive'}">${user.isActive ? 'Active' : 'Inactive'}</span>
                    </div>
                </td>
                <td>
                    <div class="activity-info">
                        <div class="activity-item">
                            <span class="activity-label">Joined:</span> ${this.formatDate(user.createdAt)}
                        </div>
                        <div class="activity-item">
                            <span class="activity-label">Last login:</span> ${this.formatDate(user.lastLoginAt) || 'Never'}
                        </div>
                    </div>
                </td>
                <td>
                    <div class="orders-info">
                        <div class="orders-count">${user.totalOrders || 0} orders</div>
                        <div class="orders-amount">$${user.totalSpent || 0}</div>
                    </div>
                </td>
                <td>
                    <div class="actions">
                        ${this.config.currentUserRole === 'admin' ? `
                            <button class="action-btn action-btn-edit" onclick="userManagement.editUser('${user.id}')" title="Edit User">
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                    <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path>
                                    <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path>
                                </svg>
                            </button>
                        ` : ''}
                        
                        ${this.currentTab === 'active' ? `
                            <button class="action-btn action-btn-lock" 
                                    onclick="userManagement.blockUser('${user.id}')" 
                                    title="Lock User">
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                    <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                                    <circle cx="12" cy="16" r="1"></circle>
                                    <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
                                </svg>
                            </button>
                        ` : `
                            <button class="action-btn action-btn-unlock" 
                                    onclick="userManagement.unblockUser('${user.id}')" 
                                    title="Unlock User">
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                    <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                                    <circle cx="12" cy="16" r="1"></circle>
                                    <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
                                </svg>
                            </button>
                        `}
                        <button class="action-btn action-btn-more" onclick="userManagement.showUserDetail('${user.id}')" title="View Details">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <circle cx="12" cy="12" r="1"></circle>
                                <circle cx="19" cy="12" r="1"></circle>
                                <circle cx="5" cy="12" r="1"></circle>
                            </svg>
                        </button>
                    </div>
                </td>
            </tr>
        `;
    }

    bindRowEvents() {
        // Events are bound via onclick attributes in the rendered HTML
    }

    async blockUser(userId) {
        const user = this.users.find(u => u.id === userId);
        if (!user) return;

        const confirmMessage = 'Are you sure you want to block this user?';
        if (!confirm(confirmMessage)) return;

        try {
            this.showLoading();
            
            const endpoint = `${this.config.apiBaseUrl}/users/${userId}/block`;

            const response = await fetch(endpoint, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${this.config.accessToken}`,
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            // Update user status locally
            user.isActive = false;
            
            this.updateStats();
            this.filterUsers();
            
            this.showSuccess('User blocked successfully.');
            
        } catch (error) {
            console.error('Error blocking user:', error);
            this.showError('Failed to block user. Please try again.');
        } finally {
            this.hideLoading();
        }
    }

    async unblockUser(userId) {
        const user = this.users.find(u => u.id === userId);
        if (!user) return;

        const confirmMessage = 'Are you sure you want to unblock this user?';
        if (!confirm(confirmMessage)) return;

        try {
            this.showLoading();
            
            const endpoint = `${this.config.apiBaseUrl}/users/${userId}/unblock`;

            const response = await fetch(endpoint, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${this.config.accessToken}`,
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            // Update user status locally
            user.isActive = true;
            
            this.updateStats();
            this.filterUsers();
            
            this.showSuccess('User unblocked successfully.');
            
        } catch (error) {
            console.error('Error unblocking user:', error);
            this.showError('Failed to unblock user. Please try again.');
        } finally {
            this.hideLoading();
        }
    }

    showUserDetail(userId) {
        const user = this.users.find(u => u.id === userId);
        if (!user) return;

        this.selectedUser = user;
        this.populateUserDetailModal(user);
        this.showUserDetailModal();
    }

    populateUserDetailModal(user) {
        const profile = user.profile || {};
        const avatarUrl = profile.avatarUrl || this.getDefaultAvatar(user.email);
        
        document.getElementById('userDetailAvatar').src = avatarUrl;
        document.getElementById('userDetailAvatar').alt = profile.fullName || 'Unknown User';
        document.getElementById('userDetailName').textContent = profile.fullName || 'Unknown User';
        document.getElementById('userDetailEmail').textContent = user.email;
        document.getElementById('userDetailEmailInfo').textContent = user.email;
        document.getElementById('userDetailPhoneInfo').textContent = profile.phone || 'N/A';
        document.getElementById('userDetailJoinDate').textContent = this.formatDate(user.createdAt);
        document.getElementById('userDetailLastLogin').textContent = this.formatDate(user.lastLoginAt) || 'Never';
        document.getElementById('userDetailTotalOrders').textContent = user.totalOrders || 0;
        document.getElementById('userDetailTotalSpent').textContent = `$${user.totalSpent || 0}`;

        // Update role and status badges
        const roleBadge = document.getElementById('userDetailRole');
        const statusBadge = document.getElementById('userDetailStatus');
        
        roleBadge.textContent = this.capitalizeFirst(user.role);
        roleBadge.className = `badge badge-${user.role}`;
        
        statusBadge.textContent = user.isActive ? 'Active' : 'Inactive';
        statusBadge.className = `badge badge-${user.isActive ? 'active' : 'inactive'}`;

        // Update lock/unlock button
        const lockUnlockBtn = document.getElementById('lockUnlockBtn');
        const lockUnlockText = document.getElementById('lockUnlockText');
        const lockUnlockIcon = lockUnlockBtn.querySelector('svg');
        
        if (user.isActive) {
            lockUnlockBtn.className = 'btn btn-danger';
            lockUnlockText.textContent = 'Lock User';
            lockUnlockIcon.innerHTML = `
                <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                <circle cx="12" cy="16" r="1"></circle>
                <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
            `;
        } else {
            lockUnlockBtn.className = 'btn btn-success';
            lockUnlockText.textContent = 'Unlock User';
            lockUnlockIcon.innerHTML = `
                <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                <circle cx="12" cy="16" r="1"></circle>
                <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
            `;
        }
    }

    async handleLockUnlockUser() {
        if (!this.selectedUser) return;
        
        if (this.selectedUser.isActive) {
            await this.blockUser(this.selectedUser.id);
        } else {
            await this.unblockUser(this.selectedUser.id);
        }
        
        // Update the modal with new user data
        const updatedUser = this.users.find(u => u.id === this.selectedUser.id);
        if (updatedUser) {
            this.selectedUser = updatedUser;
            this.populateUserDetailModal(updatedUser);
        }
    }

    showAddUserModal() {
        document.getElementById('addUserModal').style.display = 'flex';
        document.getElementById('addUserForm').reset();
    }

    hideAddUserModal() {
        document.getElementById('addUserModal').style.display = 'none';
    }

    showUserDetailModal() {
        document.getElementById('userDetailModal').style.display = 'flex';
    }

    hideUserDetailModal() {
        document.getElementById('userDetailModal').style.display = 'none';
        this.selectedUser = null;
    }

    hideAllModals() {
        this.hideAddUserModal();
        this.hideUserDetailModal();
    }

    async handleAddUser() {
        const form = document.getElementById('addUserForm');
        const formData = new FormData(form);
        
        const userData = {
            email: formData.get('email'),
            fullName: formData.get('fullName'),
            phone: formData.get('phone'),
            role: formData.get('role'),
            password: 'TempPassword123!' // Default password, should be changed on first login
        };

        try {
            this.showLoading();
            
            const response = await fetch(`${this.config.apiBaseUrl}/users`, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${this.config.accessToken}`,
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(userData)
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            this.hideAddUserModal();
            this.loadUsers(); // Reload users to show the new one
            this.showSuccess('User created successfully.');
            
        } catch (error) {
            console.error('Error creating user:', error);
            this.showError('Failed to create user. Please try again.');
        } finally {
            this.hideLoading();
        }
    }

    editUser(userId) {
        // TODO: Implement edit user functionality
        console.log('Edit user:', userId);
        this.showError('Edit functionality not implemented yet.');
    }

    // Utility functions
    getDefaultAvatar(email) {
        // Generate a simple avatar based on email
        const hash = this.hashCode(email);
        const colors = ['#8b5cf6', '#3b82f6', '#10b981', '#f59e0b', '#ef4444'];
        const color = colors[Math.abs(hash) % colors.length];
        const initial = email.charAt(0).toUpperCase();
        
        return `data:image/svg+xml,${encodeURIComponent(`
            <svg width="40" height="40" xmlns="http://www.w3.org/2000/svg">
                <rect width="40" height="40" fill="${color}"/>
                <text x="20" y="26" text-anchor="middle" fill="white" font-family="Arial" font-size="16" font-weight="bold">${initial}</text>
            </svg>
        `)}`;
    }

    hashCode(str) {
        let hash = 0;
        for (let i = 0; i < str.length; i++) {
            const char = str.charCodeAt(i);
            hash = ((hash << 5) - hash) + char;
            hash = hash & hash; // Convert to 32-bit integer
        }
        return hash;
    }

    formatDate(dateString) {
        if (!dateString) return 'Never';
        const date = new Date(dateString);
        return date.toLocaleDateString('en-US', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit'
        });
    }

    capitalizeFirst(str) {
        return str.charAt(0).toUpperCase() + str.slice(1);
    }

    showLoading() {
        document.body.classList.add('loading');
    }

    hideLoading() {
        document.body.classList.remove('loading');
    }

    showSuccess(message) {
        this.showMessage(message, 'success');
    }

    showError(message) {
        this.showMessage(message, 'error');
    }

    showMessage(message, type) {
        // Remove existing messages
        const existingMessages = document.querySelectorAll('.message');
        existingMessages.forEach(msg => msg.remove());

        // Create new message
        const messageDiv = document.createElement('div');
        messageDiv.className = `message message-${type}`;
        messageDiv.textContent = message;

        // Insert at the top of the container
        const container = document.querySelector('.user-management-container');
        container.insertBefore(messageDiv, container.firstChild);

        // Auto-remove after 5 seconds
        setTimeout(() => {
            messageDiv.remove();
        }, 5000);
    }
}

// Initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    window.userManagement = new UserManagement();
});
