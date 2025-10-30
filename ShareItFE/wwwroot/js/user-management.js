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
        
        // Pagination
        this.currentPage = 1;
        this.itemsPerPage = 10;
        
        // Sorting
        this.sortColumn = null;
        this.sortDirection = 'asc';
        
        // More filters
        this.moreFilters = {
            minOrders: null,
            maxOrders: null
        };
        
        this.init();
    }

    init() {
        this.createToastContainer();
        this.bindEvents();
        this.loadUsers();
        
        // Check if we need to auto-open a user detail modal from URL params
        this.checkUrlParams();
    }
    
    checkUrlParams() {
        const urlParams = new URLSearchParams(window.location.search);
        const userId = urlParams.get('userId');
        const openDetail = urlParams.get('openDetail');
        
        if (userId && openDetail === 'true') {
            // Wait for users to load, then open the detail modal
            const checkUsersLoaded = setInterval(() => {
                if (this.users && this.users.length > 0) {
                    clearInterval(checkUsersLoaded);
                    this.showUserDetail(userId);
                    // Clean up URL without page reload
                    window.history.replaceState({}, document.title, window.location.pathname);
                }
            }, 100);
            
            // Timeout after 5 seconds
            setTimeout(() => clearInterval(checkUsersLoaded), 5000);
        }
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

        // More Filters modal events
        const openMoreFiltersBtn = document.getElementById('openMoreFiltersBtn');
        if (openMoreFiltersBtn) {
            openMoreFiltersBtn.addEventListener('click', () => this.showMoreFiltersModal());
        }
        const closeMoreFiltersModal = document.getElementById('closeMoreFiltersModal');
        if (closeMoreFiltersModal) {
            closeMoreFiltersModal.addEventListener('click', () => this.hideMoreFiltersModal());
        }
        const applyMoreFiltersBtn = document.getElementById('applyMoreFiltersBtn');
        if (applyMoreFiltersBtn) {
            applyMoreFiltersBtn.addEventListener('click', () => this.applyMoreFilters());
        }
        const resetMoreFiltersBtn = document.getElementById('resetMoreFiltersBtn');
        if (resetMoreFiltersBtn) {
            resetMoreFiltersBtn.addEventListener('click', () => this.resetMoreFilters());
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
            
            // Use the new endpoint with order statistics
            const endpoint = '/users/with-order-stats';
                
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
            
            this.loadStatistics();
            this.filterUsers();
            
        } catch (error) {
            console.error('Error loading users:', error);
            this.showError('Failed to load users. Please try again.');
        } finally {
            this.hideLoading();
        }
    }

    async loadStatistics() {
        try {
            const response = await fetch(`${this.config.apiBaseUrl}/users/statistics`, {
                headers: {
                    'Authorization': `Bearer ${this.config.accessToken}`,
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const result = await response.json();
            const stats = result.data;

            // Update basic stats
            document.getElementById('totalUsers').textContent = stats.totalUsers;
            document.getElementById('activeUsers').textContent = stats.activeUsers;
            document.getElementById('inactiveUsers').textContent = stats.inactiveUsers;
            document.getElementById('newThisMonth').textContent = stats.newThisMonth;

            // Update trend information
            this.updateTrendDisplay(stats);
            
            // Store stats for later use
            this.currentStats = stats;

        } catch (error) {
            console.error('Error loading statistics:', error);
            // Fallback to client-side calculation
            this.updateStatsClientSide();
        }
    }

    updateTrendDisplay(stats) {
        // Find the "New This Month" stat value
        const newThisMonthValue = document.getElementById('newThisMonth');
        if (!newThisMonthValue) return;
        
        const statContent = newThisMonthValue.closest('.stat-content');
        if (!statContent) return;

        // Remove existing trend element
        const existingTrend = statContent.querySelector('.stat-trend');
        if (existingTrend) existingTrend.remove();

        // Create trend element
        if (stats.trendPercentage !== undefined && stats.trendDirection) {
            const trendElement = document.createElement('div');
            trendElement.className = `stat-trend trend-${stats.trendDirection}`;
            
            let trendIcon = '→';
            
            if (stats.trendDirection === 'up') {
                trendIcon = '↑';
            } else if (stats.trendDirection === 'down') {
                trendIcon = '↓';
            }
            
            const absPercentage = Math.abs(stats.trendPercentage);
            const sign = stats.trendPercentage >= 0 ? '+' : '';
            
            // Create detailed tooltip
            const tooltipText = `This Month: ${stats.newThisMonth} users (${stats.avgPerDayThisMonth}/day avg)
Last Month: ${stats.newLastMonth} users (${stats.avgPerDayLastMonth}/day avg)
Change: ${sign}${stats.absoluteChange} users (${sign}${absPercentage}%)
Progress: Day ${stats.currentDay} of ${stats.daysInMonth} (${stats.monthProgress}%)`;
            
            trendElement.innerHTML = `
                <span class="trend-icon">${trendIcon}</span>
                <span class="trend-percent">${sign}${absPercentage}%</span>
                <span class="trend-text">vs last month</span>
            `;
            
            trendElement.title = tooltipText;
            trendElement.style.cursor = 'help';
            
            // Append to stat-content
            statContent.appendChild(trendElement);
        }
    }

    updateStatsClientSide() {
        let usersToCount = this.users;
        
        // For staff and admin users, only count customers and providers
        if (this.config.currentUserRole === 'staff' || this.config.currentUserRole === 'admin') {
            usersToCount = this.users.filter(u => u.role === 'customer' || u.role === 'provider');
        }
        
        const totalUsers = usersToCount.length;
        const activeUsers = usersToCount.filter(u => u.isActive).length;
        const inactiveUsers = usersToCount.filter(u => !u.isActive).length;
        
        // Calculate new users this month (client-side)
        const now = new Date();
        const currentMonth = now.getMonth();
        const currentYear = now.getFullYear();
        const newThisMonth = usersToCount.filter(u => {
            const userDate = new Date(u.createdAt);
            return userDate.getMonth() === currentMonth && userDate.getFullYear() === currentYear;
        }).length;

        document.getElementById('totalUsers').textContent = totalUsers;
        document.getElementById('activeUsers').textContent = activeUsers;
        document.getElementById('inactiveUsers').textContent = inactiveUsers;
        document.getElementById('newThisMonth').textContent = newThisMonth;
        
        console.warn('Using client-side statistics calculation (API unavailable)');
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
            
        // For staff and admin users, only show customers and providers
        if (this.config.currentUserRole === 'staff' || this.config.currentUserRole === 'admin') {
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
            
            // More filters: min/max total orders
            const totalOrders = user.totalOrders || 0;
            if (this.moreFilters.minOrders !== null && totalOrders < this.moreFilters.minOrders) return false;
            if (this.moreFilters.maxOrders !== null && totalOrders > this.moreFilters.maxOrders) return false;
            
            return true;
        });

        this.filteredUsers = filtered;
        this.currentPage = 1; // Reset to first page when filtering
        this.renderUsers();
        this.updateTabCounts();
    }

    updateTabCounts() {
        let usersToCount = this.users;
        
        // For staff and admin users, only count customers and providers
        if (this.config.currentUserRole === 'staff' || this.config.currentUserRole === 'admin') {
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
            this.hidePagination();
            return;
        }
        
        emptyState.style.display = 'none';
        
        // Apply sorting
        this.applySorting();
        
        // Paginate
        const startIndex = (this.currentPage - 1) * this.itemsPerPage;
        const endIndex = startIndex + this.itemsPerPage;
        const paginatedUsers = this.filteredUsers.slice(startIndex, endIndex);
        
        tbody.innerHTML = paginatedUsers.map(user => this.renderUserRow(user)).join('');
        
        // Render pagination
        this.renderPagination();
        
        // Bind row events
        this.bindRowEvents();
    }

    applySorting() {
        if (!this.sortColumn) return;
        
        this.filteredUsers.sort((a, b) => {
            let aValue, bValue;
            
            switch(this.sortColumn) {
                case 'name':
                    aValue = (a.profile?.fullName || '').toLowerCase();
                    bValue = (b.profile?.fullName || '').toLowerCase();
                    break;
                case 'email':
                    aValue = a.email.toLowerCase();
                    bValue = b.email.toLowerCase();
                    break;
                case 'role':
                    aValue = a.role;
                    bValue = b.role;
                    break;
                case 'status':
                    aValue = a.isActive ? 1 : 0;
                    bValue = b.isActive ? 1 : 0;
                    break;
                case 'created':
                    aValue = new Date(a.createdAt);
                    bValue = new Date(b.createdAt);
                    break;
                case 'lastLogin':
                    aValue = a.lastLogin ? new Date(a.lastLogin) : new Date(0);
                    bValue = b.lastLogin ? new Date(b.lastLogin) : new Date(0);
                    break;
                case 'orders':
                    aValue = typeof a.totalOrders === 'number' ? a.totalOrders : (parseInt(a.totalOrders, 10) || 0);
                    bValue = typeof b.totalOrders === 'number' ? b.totalOrders : (parseInt(b.totalOrders, 10) || 0);
                    break;
                default:
                    return 0;
            }
            
            if (aValue < bValue) return this.sortDirection === 'asc' ? -1 : 1;
            if (aValue > bValue) return this.sortDirection === 'asc' ? 1 : -1;
            return 0;
        });
    }

    sortBy(column) {
        if (this.sortColumn === column) {
            // Toggle direction
            this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
        } else {
            this.sortColumn = column;
            this.sortDirection = 'asc';
        }
        
        this.updateSortIndicators();
        this.currentPage = 1; // Reset to first page
        this.renderUsers();
    }

    updateSortIndicators() {
        document.querySelectorAll('.users-table th').forEach(th => {
            th.classList.remove('sorted');
            const indicator = th.querySelector('.sort-indicator');
            if (indicator) {
                indicator.innerHTML = '<svg class="sort-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M7 15l5 5 5-5M7 9l5-5 5 5"></path></svg>';
            }
        });
        
        const currentTh = document.querySelector(`[data-sort="${this.sortColumn}"]`);
        if (currentTh) {
            currentTh.classList.add('sorted');
            const indicator = currentTh.querySelector('.sort-indicator');
            if (indicator) {
                const icon = this.sortDirection === 'asc' 
                    ? '<svg class="sort-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="18 15 12 9 6 15"></polyline></svg>'
                    : '<svg class="sort-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 12 15 18 9"></polyline></svg>';
                indicator.innerHTML = icon;
            }
        }
    }

    renderPagination() {
        const totalPages = Math.ceil(this.filteredUsers.length / this.itemsPerPage);
        
        let paginationHTML = `
            <div class="pagination-container">
                <div class="pagination-info">
                    Showing ${((this.currentPage - 1) * this.itemsPerPage) + 1} to ${Math.min(this.currentPage * this.itemsPerPage, this.filteredUsers.length)} of ${this.filteredUsers.length} users
                </div>
                <div style="display: flex; align-items: center; gap: 1rem;">
                    <div style="display: flex; align-items: center; gap: 0.5rem;">
                        <span style="font-size: 0.875rem; color: #6b7280;">Show:</span>
                        <select class="pagination-select" onchange="userManagement.changeItemsPerPage(this.value)">
                            <option value="10" ${this.itemsPerPage === 10 ? 'selected' : ''}>10</option>
                            <option value="20" ${this.itemsPerPage === 20 ? 'selected' : ''}>20</option>
                            <option value="50" ${this.itemsPerPage === 50 ? 'selected' : ''}>50</option>
                        </select>
                    </div>
                    <div class="pagination">
                        <button class="pagination-btn" onclick="userManagement.goToPage(${this.currentPage - 1})" ${this.currentPage === 1 ? 'disabled' : ''}>
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <polyline points="15 18 9 12 15 6"></polyline>
                            </svg>
                        </button>
        `;
        
        // Page numbers
        for (let i = 1; i <= totalPages; i++) {
            if (i === 1 || i === totalPages || (i >= this.currentPage - 1 && i <= this.currentPage + 1)) {
                paginationHTML += `
                    <button class="pagination-btn ${i === this.currentPage ? 'active' : ''}" 
                            onclick="userManagement.goToPage(${i})">
                        ${i}
                    </button>
                `;
            } else if (i === this.currentPage - 2 || i === this.currentPage + 2) {
                paginationHTML += `<span style="color: #9ca3af;">...</span>`;
            }
        }
        
        paginationHTML += `
                        <button class="pagination-btn" onclick="userManagement.goToPage(${this.currentPage + 1})" ${this.currentPage === totalPages ? 'disabled' : ''}>
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <polyline points="9 18 15 12 9 6"></polyline>
                            </svg>
                        </button>
                    </div>
                </div>
            </div>
        `;
        
        // Insert or update pagination
        let paginationContainer = document.querySelector('.pagination-container');
        if (paginationContainer) {
            paginationContainer.outerHTML = paginationHTML;
        } else {
            document.querySelector('.table-container').insertAdjacentHTML('beforeend', paginationHTML);
        }
    }

    hidePagination() {
        const paginationContainer = document.querySelector('.pagination-container');
        if (paginationContainer) {
            paginationContainer.remove();
        }
    }

    goToPage(page) {
        const totalPages = Math.ceil(this.filteredUsers.length / this.itemsPerPage);
        if (page < 1 || page > totalPages) return;
        
        this.currentPage = page;
        this.renderUsers();
        
        // Scroll to top of table
        document.querySelector('.table-container').scrollIntoView({ behavior: 'smooth' });
    }

    changeItemsPerPage(value) {
        this.itemsPerPage = parseInt(value);
        this.currentPage = 1;
        this.renderUsers();
    }

    exportToCSV() {
        if (this.filteredUsers.length === 0) {
            this.showWarning('No users to export');
            return;
        }

        // Prepare CSV data
        const headers = ['Name', 'Email', 'Phone', 'Role', 'Status', 'Join Date', 'Last Login', 'Total Orders'];
        const rows = this.filteredUsers.map(user => {
            const profile = user.profile || {};
            return [
                profile.fullName || 'N/A',
                user.email,
                profile.phone || 'N/A',
                this.capitalizeFirst(user.role),
                user.isActive ? 'Active' : 'Inactive',
                this.formatDate(user.createdAt),
                this.formatDate(user.lastLogin) || 'Never',
                user.totalOrders || 0
            ];
        });

        // Create CSV content
        let csvContent = headers.join(',') + '\n';
        rows.forEach(row => {
            csvContent += row.map(cell => `"${cell}"`).join(',') + '\n';
        });

        // Download CSV
        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement('a');
        const url = URL.createObjectURL(blob);
        
        link.setAttribute('href', url);
        link.setAttribute('download', `users_export_${new Date().toISOString().split('T')[0]}.csv`);
        link.style.visibility = 'hidden';
        
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);

        this.showSuccess(`Exported ${this.filteredUsers.length} users to CSV`);
    }

    formatCurrency(amount) {
        return new Intl.NumberFormat('vi-VN', {
            style: 'currency',
            currency: 'VND'
        }).format(amount);
    }

    renderUserRow(user) {
        const profile = user.profile || {};
        const avatarUrl = profile.avatarUrl || this.getDefaultAvatar(user.email);
        const fullName = profile.fullName || 'Unknown User';
        const phone = profile.phone || 'N/A';
        
        // ✅ CHỈ HIỂN THỊ tổng số orders
        const totalOrders = user.totalOrders || 0;
        
        return `
            <tr>
                <td>
                    <div class="user-info">
                        <img src="${avatarUrl}" alt="${fullName}" class="user-avatar">
                        <div class="user-details">
                            <div class="user-name">${fullName}</div>
                            <div class="user-id">ID: ${user.id.substring(0, 8)}...</div>
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
                            <span class="activity-label">Last login:</span> ${this.formatDate(user.lastLogin) || 'Never'}
                        </div>
                    </div>
                </td>
                <td>
                    <div class="orders-info">
                        <div class="orders-count">${totalOrders} orders</div>
                    </div>
                </td>
                <td>
                    <div class="actions">
                        
                        
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
            
            this.loadStatistics();
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
            
            this.loadStatistics();
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
        if (!user) {
            console.error('User not found:', userId);
            return;
        }

        console.log('User data:', user);
        this.selectedUser = user;
        
        try {
        this.populateUserDetailModal(user);
        this.showUserDetailModal();
        } catch (error) {
            console.error('Error populating user detail modal:', error);
            this.showError('Failed to load user details. Please try again.');
        }
    }

    populateUserDetailModal(user) {
        const profile = user.profile || {};
        const avatarUrl = profile.avatarUrl || this.getDefaultAvatar(user.email);
        
        // Basic info
        document.getElementById('userDetailAvatar').src = avatarUrl;
        document.getElementById('userDetailAvatar').alt = profile.fullName || 'Unknown User';
        document.getElementById('userDetailName').textContent = profile.fullName || 'Unknown User';
        document.getElementById('userDetailEmail').textContent = user.email;

        // Update role and status badges
        const roleBadge = document.getElementById('userDetailRole');
        const statusBadge = document.getElementById('userDetailStatus');
        
        // Handle both 'role' and 'Role' (camelCase and PascalCase)
        const userRole = (user.role || user.Role || '').toString().toLowerCase();
        const isActive = user.isActive !== undefined ? user.isActive : user.IsActive;
        
        roleBadge.textContent = this.capitalizeFirst(userRole);
        roleBadge.className = `badge badge-${userRole}`;
        
        statusBadge.textContent = isActive ? 'Active' : 'Inactive';
        statusBadge.className = `badge badge-${isActive ? 'active' : 'inactive'}`;

        // ✅ Hiển thị Order Statistics và Returned Orders Breakdown
        const ordersByStatus = user.ordersByStatus || user.OrdersByStatus || {};
        const returnedBreakdown = user.returnedOrdersBreakdown || user.ReturnedOrdersBreakdown || {};
        
        console.log('Orders by status:', ordersByStatus);
        console.log('Returned breakdown:', returnedBreakdown);
        
        // Create or update order statistics section in modal body
        const modalBody = document.querySelector('#userDetailModal .modal-body');
        console.log('Modal body found:', modalBody);
        if (!modalBody) {
            console.error('Modal body not found!');
            return;
        }
        
        console.log('Rendering modal content...');
        
        // Determine breakdown title based on role
        const breakdownTitle = userRole === 'provider' 
            ? 'Income Breakdown by Product Type' 
            : 'Spending Breakdown by Product Type';
        
        const totalLabel = userRole === 'provider' ? 'Total Earnings' : 'Total Spent';
        
        // Clear modal body and render Order Statistics + Returned Orders Breakdown
        modalBody.innerHTML = `
            <!-- Order Statistics by Status -->
            <div class="info-section">
                <h4 class="section-title">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="width: 20px; height: 20px; margin-right: 8px; vertical-align: middle;">
                        <path d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01"></path>
                    </svg>
                    Order Statistics
                </h4>
                <div class="stats-grid">
                    <div class="stat-card">
                        <div class="stat-label">Pending</div>
                        <div class="stat-value">${ordersByStatus.Pending || ordersByStatus.pending || 0}</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-label">Approved</div>
                        <div class="stat-value">${ordersByStatus.Approved || ordersByStatus.approved || 0}</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-label">In Transit</div>
                        <div class="stat-value">${ordersByStatus.InTransit || ordersByStatus.inTransit || 0}</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-label">In Use</div>
                        <div class="stat-value">${ordersByStatus.InUse || ordersByStatus.inUse || 0}</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-label">Returning</div>
                        <div class="stat-value">${ordersByStatus.Returning || ordersByStatus.returning || 0}</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-label">Returned</div>
                        <div class="stat-value">${ordersByStatus.Returned || ordersByStatus.returned || 0}</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-label">Cancelled</div>
                        <div class="stat-value">${ordersByStatus.Cancelled || ordersByStatus.cancelled || 0}</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-label">Returned with Issue</div>
                        <div class="stat-value">${ordersByStatus.ReturnedWithIssue || ordersByStatus.returnedWithIssue || 0}</div>
                    </div>
                </div>
            </div>
            
            <!-- Returned Orders Breakdown (Financial) -->
            <div class="info-section">
                <h4 class="section-title">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="width: 20px; height: 20px; margin-right: 8px; vertical-align: middle;">
                        <path d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
                    </svg>
                    ${breakdownTitle}
                </h4>
                <div class="earnings-breakdown">
                    <div class="breakdown-item">
                        <div class="breakdown-type">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="width: 20px; height: 20px;">
                                <rect x="2" y="7" width="20" height="14" rx="2" ry="2"></rect>
                                <path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16"></path>
                            </svg>
                            <span>Rental Products</span>
                        </div>
                        <div class="breakdown-details">
                            <div class="breakdown-count">${returnedBreakdown.RentalProductsCount || returnedBreakdown.rentalProductsCount || returnedBreakdown.RentalOrdersCount || returnedBreakdown.rentalOrdersCount || 0} products</div>
                            <div class="breakdown-amount">${this.formatCurrency(returnedBreakdown.RentalTotalEarnings || returnedBreakdown.rentalTotalEarnings || 0)}</div>
                        </div>
                    </div>
                    
                    <div class="breakdown-item">
                        <div class="breakdown-type">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="width: 20px; height: 20px;">
                                <circle cx="9" cy="21" r="1"></circle>
                                <circle cx="20" cy="21" r="1"></circle>
                                <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"></path>
                            </svg>
                            <span>Purchase Products</span>
                        </div>
                        <div class="breakdown-details">
                            <div class="breakdown-count">${returnedBreakdown.PurchaseProductsCount || returnedBreakdown.purchaseProductsCount || returnedBreakdown.PurchaseOrdersCount || returnedBreakdown.purchaseOrdersCount || 0} products</div>
                            <div class="breakdown-amount">${this.formatCurrency(returnedBreakdown.PurchaseTotalEarnings || returnedBreakdown.purchaseTotalEarnings || 0)}</div>
                        </div>
                    </div>
                    
                    <div class="breakdown-total">
                        <div class="total-label">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="width: 18px; height: 18px; margin-right: 6px; vertical-align: middle;">
                                <polyline points="22 12 18 12 15 21 9 3 6 12 2 12"></polyline>
                            </svg>
                            ${totalLabel}
                        </div>
                        <div class="total-amount">${this.formatCurrency(returnedBreakdown.TotalEarnings || returnedBreakdown.totalEarnings || 0)}</div>
                    </div>
                </div>
            </div>
        `;

        // Update lock/unlock button
        const lockUnlockBtn = document.getElementById('lockUnlockBtn');
        const lockUnlockText = document.getElementById('lockUnlockText');
        
        if (lockUnlockBtn && lockUnlockText) {
        const lockUnlockIcon = lockUnlockBtn.querySelector('svg');
        
            if (isActive) {
            lockUnlockBtn.className = 'btn btn-danger';
            lockUnlockText.textContent = 'Lock User';
                if (lockUnlockIcon) {
            lockUnlockIcon.innerHTML = `
                <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                <circle cx="12" cy="16" r="1"></circle>
                <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
            `;
                }
        } else {
            lockUnlockBtn.className = 'btn btn-success';
            lockUnlockText.textContent = 'Unlock User';
                if (lockUnlockIcon) {
            lockUnlockIcon.innerHTML = `
                <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                <circle cx="12" cy="16" r="1"></circle>
                <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
            `;
                }
            }
        } else {
            console.error('Lock/Unlock button elements not found');
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

    // More Filters modal helpers
    showMoreFiltersModal() {
        const modal = document.getElementById('moreFiltersModal');
        if (modal) modal.style.display = 'flex';
        // Pre-fill inputs with current values
        const minOrdersInput = document.getElementById('minOrdersInput');
        const maxOrdersInput = document.getElementById('maxOrdersInput');
        if (minOrdersInput) minOrdersInput.value = this.moreFilters.minOrders ?? '';
        if (maxOrdersInput) maxOrdersInput.value = this.moreFilters.maxOrders ?? '';
    }

    hideMoreFiltersModal() {
        const modal = document.getElementById('moreFiltersModal');
        if (modal) modal.style.display = 'none';
    }

    applyMoreFilters() {
        const minOrdersInput = document.getElementById('minOrdersInput');
        const maxOrdersInput = document.getElementById('maxOrdersInput');
        const minVal = minOrdersInput && minOrdersInput.value !== '' ? parseInt(minOrdersInput.value, 10) : null;
        const maxVal = maxOrdersInput && maxOrdersInput.value !== '' ? parseInt(maxOrdersInput.value, 10) : null;
        this.moreFilters.minOrders = Number.isFinite(minVal) ? minVal : null;
        this.moreFilters.maxOrders = Number.isFinite(maxVal) ? maxVal : null;
        this.hideMoreFiltersModal();
        this.filterUsers();
        this.showInfo('Filters applied');
    }

    resetMoreFilters() {
        this.moreFilters = { minOrders: null, maxOrders: null };
        const minOrdersInput = document.getElementById('minOrdersInput');
        const maxOrdersInput = document.getElementById('maxOrdersInput');
        if (minOrdersInput) minOrdersInput.value = '';
        if (maxOrdersInput) maxOrdersInput.value = '';
        this.hideMoreFiltersModal();
        this.filterUsers();
        this.showInfo('Filters cleared');
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
        this.showToast(message, 'success');
    }

    showError(message) {
        this.showToast(message, 'error');
    }

    showWarning(message) {
        this.showToast(message, 'warning');
    }

    showInfo(message) {
        this.showToast(message, 'info');
    }

    // Toast Notification System
    createToastContainer() {
        if (!document.getElementById('toastContainer')) {
            const container = document.createElement('div');
            container.id = 'toastContainer';
            container.className = 'toast-container';
            document.body.appendChild(container);
        }
    }

    showToast(message, type = 'info') {
        const container = document.getElementById('toastContainer');
        const toast = document.createElement('div');
        toast.className = `toast ${type}`;
        
        const icons = {
            success: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22,4 12,14.01 9,11.01"></polyline></svg>',
            error: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>',
            warning: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>',
            info: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>'
        };

        toast.innerHTML = `
            <div class="toast-icon">${icons[type]}</div>
            <div class="toast-content">
                <p class="toast-message">${message}</p>
            </div>
            <button class="toast-close" onclick="this.parentElement.remove()">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <line x1="18" y1="6" x2="6" y2="18"></line>
                    <line x1="6" y1="6" x2="18" y2="18"></line>
                </svg>
            </button>
        `;

        container.appendChild(toast);

        // Auto remove after 5 seconds
        setTimeout(() => {
            toast.classList.add('hiding');
            setTimeout(() => toast.remove(), 300);
        }, 5000);
    }

    // Legacy method for backward compatibility
    showMessage(message, type) {
        this.showToast(message, type);
    }
}

// Initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    window.userManagement = new UserManagement();
});
