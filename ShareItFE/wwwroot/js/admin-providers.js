// Admin Providers Tab JavaScript

// Global variables
window.providersData = [];
window.providersLoaded = false;

// Helper to get cookie
function getCookie(name) {
    const value = `; ${document.cookie}`;
    const parts = value.split(`; ${name}=`);
    if (parts.length === 2) return parts.pop().split(';').shift();
    return '';
}

// Load providers data from API
async function loadProvidersData() {
    try {
        const token = window.adminChatConfig?.accessToken || getCookie('AccessToken');
        
        if (!token) {
            throw new Error('Authentication token not found. Please login again.');
        }
        
        // Get date filters from admin dashboard
        const startDate = document.getElementById('startDate')?.value;
        const endDate = document.getElementById('endDate')?.value;
        
        // Build API URL with date filters
        let apiUrl = `${window.apiSettings.baseUrl}/ProviderFinance/all/payments`;
        if (startDate && endDate) {
            apiUrl += `?startDate=${startDate}&endDate=${endDate}`;
        }
        
        const response = await fetch(apiUrl, {
            headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        });

        if (!response.ok) {
            if (response.status === 401) {
                throw new Error('Authentication failed. Please login again.');
            } else if (response.status === 403) {
                throw new Error('Access denied. You do not have permission to view this data.');
            } else if (response.status === 500) {
                throw new Error('Server error. Please try again later.');
            } else {
                throw new Error(`Failed to load provider data (Error ${response.status})`);
            }
        }

        const result = await response.json();
        
        window.providersData = result.data || [];
        
        updateProvidersSummary(window.providersData);
        renderProvidersTable(window.providersData);
        
    } catch (error) {
        const tbody = document.getElementById('providersTableBody');
        const errorMessage = error.message || 'An unexpected error occurred';
        
        tbody.innerHTML = `
            <tr>
                <td colspan="5" style="text-align: center; padding: 3rem;">
                    <div style="display: flex; flex-direction: column; align-items: center; color: #ef4444;">
                        <svg style="width: 3rem; height: 3rem; margin-bottom: 1rem;" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <circle cx="12" cy="12" r="10"></circle>
                            <line x1="12" y1="8" x2="12" y2="12"></line>
                            <line x1="12" y1="16" x2="12.01" y2="16"></line>
                        </svg>
                        <p style="font-weight: 600; font-size: 1rem; margin-bottom: 0.5rem;">Unable to Load Provider Data</p>
                        <p style="font-size: 0.875rem; color: #6b7280;">${errorMessage}</p>
                    </div>
                </td>
            </tr>
        `;
        
        // Reset summary to zeros
        document.getElementById('providerTotalRevenue').textContent = '0₫';
        document.getElementById('activeProvidersCount').textContent = '0';
        document.getElementById('providerTotalOrders').textContent = '0';
    }
}

// Update providers summary statistics
function updateProvidersSummary(providers) {
    // Handle both camelCase and PascalCase properties
    const totalRevenue = providers.reduce((sum, p) => sum + (p.totalEarned || p.TotalEarned || 0), 0);
    const activeProviders = providers.filter(p => (p.totalEarned || p.TotalEarned || 0) > 0).length;
    const totalOrders = providers.reduce((sum, p) => sum + (p.completedOrders || p.CompletedOrders || 0), 0);
    
    document.getElementById('providerTotalRevenue').textContent = totalRevenue.toLocaleString('vi-VN') + '₫';
    document.getElementById('activeProvidersCount').textContent = activeProviders;
    document.getElementById('providerTotalOrders').textContent = totalOrders;
}

// Render providers table
function renderProvidersTable(providers) {
    const tbody = document.getElementById('providersTableBody');
    
    if (!providers || providers.length === 0) {
        tbody.innerHTML = `
            <tr>
                <td colspan="5" style="text-align: center; padding: 3rem;">
                    <div style="display: flex; flex-direction: column; align-items: center; color: #9ca3af;">
                        <svg style="width: 3rem; height: 3rem; margin-bottom: 1rem; opacity: 0.5;" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
                            <circle cx="9" cy="7" r="4"></circle>
                            <path d="M23 21v-2a4 4 0 0 0-3-3.87"></path>
                            <path d="M16 3.13a4 4 0 0 1 0 7.75"></path>
                        </svg>
                        <p>No providers found for the selected period</p>
                        <p style="font-size: 0.875rem; margin-top: 0.5rem;">Try adjusting the date range</p>
                    </div>
                </td>
            </tr>
        `;
        return;
    }

    tbody.innerHTML = providers.map(provider => {
        // Handle both camelCase and PascalCase properties
        const totalEarned = provider.totalEarned || provider.TotalEarned || 0;
        const completedOrders = provider.completedOrders || provider.CompletedOrders || 0;
        const avgRevenue = completedOrders > 0 ? (totalEarned / completedOrders) : 0;
        
        // Get provider name and email
        const providerName = provider.providerName || provider.ProviderName || provider.fullName || 'Unknown Provider';
        const providerEmail = provider.providerEmail || provider.ProviderEmail || provider.email || '';
        const providerId = provider.providerId || provider.ProviderId;
        
        return `
            <tr data-provider-name="${providerName.toLowerCase()} ${providerEmail.toLowerCase()}">
                <td>
                    <div class="provider-info">
                        <div class="provider-avatar">
                            ${providerName.charAt(0).toUpperCase()}
                        </div>
                        <div class="provider-details">
                            <div class="provider-name">${providerName}</div>
                            <div class="provider-email">${providerEmail}</div>
                        </div>
                    </div>
                </td>
                <td data-value="${totalEarned}">
                    <span style="font-weight: 600; color: #10b981;">
                        ${totalEarned.toLocaleString('vi-VN')}₫
                    </span>
                </td>
                <td data-value="${completedOrders}">
                    ${completedOrders}
                </td>
                <td data-value="${avgRevenue}">
                    ${avgRevenue.toLocaleString('vi-VN')}₫
                </td>
                <td>
                    <button onclick="viewProviderDetails('${providerId}', '${providerName.replace(/'/g, "\\'")}')" 
                            class="view-details-btn">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path>
                            <circle cx="12" cy="12" r="3"></circle>
                        </svg>
                        <span>View Details</span>
                    </button>
                </td>
            </tr>
        `;
    }).join('');
}

// Filter providers by search query
function filterProviders() {
    const searchInput = document.getElementById('providerSearchInput');
    const filter = searchInput.value.toLowerCase();
    const table = document.getElementById('providersTable');
    const rows = table.getElementsByTagName('tr');

    // Track visible providers for summary update
    const visibleProviders = [];

    for (let i = 1; i < rows.length; i++) {
        const row = rows[i];
        const providerName = row.getAttribute('data-provider-name');
        if (providerName && providerName.includes(filter)) {
            row.style.display = '';
            
            // Extract provider data from visible row
            const cells = row.getElementsByTagName('td');
            if (cells.length >= 3) {
                const totalEarned = parseFloat(cells[1].getAttribute('data-value')) || 0;
                const completedOrders = parseFloat(cells[2].getAttribute('data-value')) || 0;
                visibleProviders.push({ totalEarned, completedOrders });
            }
        } else {
            row.style.display = 'none';
        }
    }

    // Update summary stats for visible providers only
    const totalRevenue = visibleProviders.reduce((sum, p) => sum + p.totalEarned, 0);
    const activeProviders = visibleProviders.filter(p => p.totalEarned > 0).length;
    const totalOrders = visibleProviders.reduce((sum, p) => sum + p.completedOrders, 0);
    
    document.getElementById('providerTotalRevenue').textContent = totalRevenue.toLocaleString('vi-VN') + '₫';
    document.getElementById('activeProvidersCount').textContent = activeProviders;
    document.getElementById('providerTotalOrders').textContent = totalOrders;
}

// Sort providers table
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

// Global variables for provider modal
window.currentProviderId = null;
window.currentProviderName = null;
window.currentProviderPeriod = 'month';

// View provider details in modal
async function viewProviderDetails(providerId, providerName, period = 'month') {
    // Store current provider info for reloading
    window.currentProviderId = providerId;
    window.currentProviderName = providerName;
    window.currentProviderPeriod = period;
    
    const modal = document.getElementById('providerDetailModal');
    const modalTitle = document.getElementById('providerModalTitle');
    const modalBody = document.getElementById('providerModalBody');
    
    // Update modal title with period selector
    modalTitle.innerHTML = `
        <div style="display: flex; justify-content: space-between; align-items: center; width: 100%;">
            <span>${providerName} - Performance Details</span>
            <div style="display: flex; gap: 0.5rem; margin-left: auto; margin-right: 2rem;">
                <button onclick="changeProviderPeriod('week')" 
                        style="padding: 0.375rem 0.75rem; border: 1px solid ${period === 'week' ? '#7c3aed' : '#d1d5db'}; background: ${period === 'week' ? '#7c3aed' : 'white'}; color: ${period === 'week' ? 'white' : '#6b7280'}; border-radius: 0.375rem; font-size: 0.875rem; cursor: pointer; transition: all 0.2s;">
                    Week
                </button>
                <button onclick="changeProviderPeriod('month')" 
                        style="padding: 0.375rem 0.75rem; border: 1px solid ${period === 'month' ? '#7c3aed' : '#d1d5db'}; background: ${period === 'month' ? '#7c3aed' : 'white'}; color: ${period === 'month' ? 'white' : '#6b7280'}; border-radius: 0.375rem; font-size: 0.875rem; cursor: pointer; transition: all 0.2s;">
                    Month
                </button>
                <button onclick="changeProviderPeriod('year')" 
                        style="padding: 0.375rem 0.75rem; border: 1px solid ${period === 'year' ? '#7c3aed' : '#d1d5db'}; background: ${period === 'year' ? '#7c3aed' : 'white'}; color: ${period === 'year' ? 'white' : '#6b7280'}; border-radius: 0.375rem; font-size: 0.875rem; cursor: pointer; transition: all 0.2s;">
                    Year
                </button>
            </div>
        </div>
    `;
    modalBody.innerHTML = '<div class="modal-loading">Loading provider details...</div>';
    
    // Show modal
    modal.style.display = 'flex';
    document.body.style.overflow = 'hidden';
    
    try {
        const token = window.adminChatConfig?.accessToken || getCookie('AccessToken');
        
        if (!token) {
            throw new Error('Authentication required. Please login again.');
        }
        
        const headers = { 
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json'
        };
        
        const revenueStatsUrl = `${window.apiSettings.baseUrl}/revenue/provider/${providerId}/stats?period=${period}`;
        
        const [summaryResponse, transactionsResponse, revenueStatsResponse] = await Promise.all([
            fetch(`${window.apiSettings.baseUrl}/ProviderFinance/${providerId}/summary`, { headers }),
            fetch(`${window.apiSettings.baseUrl}/ProviderFinance/${providerId}/transactions`, { headers }),
            fetch(revenueStatsUrl, { headers })
        ]);
        
        // Handle errors with user-friendly messages
        if (!summaryResponse.ok) {
            if (summaryResponse.status === 401) {
                throw new Error('Authentication expired. Please refresh the page and login again.');
            } else if (summaryResponse.status === 403) {
                throw new Error('Access denied. You do not have permission to view this provider.');
            } else {
                throw new Error('Failed to load provider financial summary.');
            }
        }
        
        if (!transactionsResponse.ok) {
            throw new Error('Failed to load transaction history.');
        }
        
        const summaryData = await summaryResponse.json();
        const transactionsData = await transactionsResponse.json();
        
        let revenueStats = null;
        if (revenueStatsResponse && revenueStatsResponse.ok) {
            const revenueData = await revenueStatsResponse.json();
            revenueStats = revenueData.data || revenueData;
        }
        
        const summary = summaryData.data;
        const transactions = transactionsData.data || [];
        
        renderProviderDetails(providerId, providerName, summary, transactions, revenueStats);
        
    } catch (error) {
        const errorMessage = error.message || 'An unexpected error occurred. Please try again.';
        
        modalBody.innerHTML = `
            <div style="display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 3rem; color: #ef4444; text-align: center;">
                <svg style="width: 4rem; height: 4rem; margin-bottom: 1rem; opacity: 0.8;" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <circle cx="12" cy="12" r="10"></circle>
                    <line x1="12" y1="8" x2="12" y2="12"></line>
                    <line x1="12" y1="16" x2="12.01" y2="16"></line>
                </svg>
                <p style="font-weight: 600; font-size: 1.125rem; color: #1f2937; margin-bottom: 0.5rem;">Unable to Load Provider Details</p>
                <p style="font-size: 0.875rem; color: #6b7280; max-width: 400px;">${errorMessage}</p>
                <button onclick="viewProviderDetails('${providerId}', '${providerName.replace(/'/g, "\\'")}', '${period}')" 
                        style="margin-top: 1.5rem; padding: 0.625rem 1.5rem; background: #7c3aed; color: white; border: none; border-radius: 0.5rem; cursor: pointer; font-size: 0.875rem; font-weight: 500;">
                    Try Again
                </button>
            </div>
        `;
    }
}

// Global variables for transaction pagination and filtering
window.currentTransactionPage = 1;
window.transactionsPerPage = 10;
window.allTransactions = [];
window.filteredTransactions = [];

// Render provider details
function renderProviderDetails(providerId, providerName, summary, transactions, revenueStats) {
    const modalBody = document.getElementById('providerModalBody');
    
    // Store transactions globally for pagination and filtering
    window.allTransactions = transactions || [];
    window.filteredTransactions = transactions || [];
    window.currentTransactionPage = 1;
    
    const bankAccount = summary?.bankAccount || summary?.BankAccount;
    
    // Use revenue stats if available, otherwise fallback to basic calculation
    let statsHtml = '';
    
    if (revenueStats) {
        // Full stats from Revenue API (same as Provider Dashboard)
        const revenue = revenueStats.currentPeriodRevenue || 0;
        const orders = revenueStats.currentPeriodOrders || 0;
        const netRevenue = revenueStats.netRevenue || 0;
        const platformFee = revenueStats.platformFee || 0;
        const avgOrderValue = revenueStats.averageOrderValue || 0;
        const revenueGrowth = revenueStats.revenueGrowthPercentage || 0;
        const ordersGrowth = revenueStats.orderGrowthPercentage || 0;
        
        statsHtml = `
            <!-- Revenue Stats (from Provider Dashboard) -->
            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; margin-bottom: 1.5rem;">
                <!-- Gross Revenue -->
                <div style="background: linear-gradient(135deg, #dbeafe 0%, #bfdbfe 100%); padding: 1.25rem; border-radius: 0.75rem; box-shadow: 0 1px 3px rgba(0,0,0,0.1);">
                    <div style="display: flex; flex-direction: column;">
                        <p style="font-size: 0.875rem; color: #1e40af; font-weight: 600; margin-bottom: 0.5rem;">Revenue</p>
                        <p style="font-size: 1.5rem; font-weight: 700; color: #1e3a8a; margin-bottom: 0.25rem;">${revenue.toLocaleString('vi-VN')}₫</p>
                        <p style="font-size: 0.75rem; color: ${revenueGrowth >= 0 ? '#059669' : '#dc2626'};">
                            ${revenueGrowth >= 0 ? '↑' : '↓'} ${Math.abs(revenueGrowth).toFixed(1)}% vs prev
                        </p>
                    </div>
                </div>
                
                <!-- Orders -->
                <div style="background: linear-gradient(135deg, #ddd6fe 0%, #c4b5fd 100%); padding: 1.25rem; border-radius: 0.75rem; box-shadow: 0 1px 3px rgba(0,0,0,0.1);">
                    <div style="display: flex; flex-direction: column;">
                        <p style="font-size: 0.875rem; color: #6b21a8; font-weight: 600; margin-bottom: 0.5rem;">Orders</p>
                        <p style="font-size: 1.5rem; font-weight: 700; color: #581c87; margin-bottom: 0.25rem;">${orders}</p>
                        <p style="font-size: 0.75rem; color: ${ordersGrowth >= 0 ? '#059669' : '#dc2626'};">
                            ${ordersGrowth >= 0 ? '↑' : '↓'} ${Math.abs(ordersGrowth).toFixed(1)}% vs prev
                        </p>
                    </div>
                </div>
                
                <!-- Net Revenue -->
                <div style="background: linear-gradient(135deg, #d1fae5 0%, #a7f3d0 100%); padding: 1.25rem; border-radius: 0.75rem; box-shadow: 0 1px 3px rgba(0,0,0,0.1);">
                    <div style="display: flex; flex-direction: column;">
                        <p style="font-size: 0.875rem; color: #065f46; font-weight: 600; margin-bottom: 0.5rem;">Net Revenue</p>
                        <p style="font-size: 1.5rem; font-weight: 700; color: #064e3b; margin-bottom: 0.5rem;">${netRevenue.toLocaleString('vi-VN')}₫</p>
                        <div style="padding-top: 0.5rem; border-top: 1px solid rgba(6, 95, 70, 0.2);">
                            <p style="font-size: 0.7rem; color: #059669; margin-bottom: 0.25rem;">After platform fees</p>
                            <div style="font-size: 0.7rem; color: #047857; display: flex; flex-direction: column; gap: 0.125rem;">
                                <div style="display: flex; justify-content: space-between;">
                                    <span>From orders:</span>
                                    <span style="font-weight: 600;">${(revenueStats.netRevenueFromOrders || 0).toLocaleString('vi-VN')}₫</span>
                                </div>
                                <div style="display: flex; justify-content: space-between;">
                                    <span>From penalties:</span>
                                    <span style="font-weight: 600;">${(revenueStats.netRevenueFromPenalties || 0).toLocaleString('vi-VN')}₫</span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
                
                <!-- Platform Fee -->
                <div style="background: linear-gradient(135deg, #fed7aa 0%, #fdba74 100%); padding: 1.25rem; border-radius: 0.75rem; box-shadow: 0 1px 3px rgba(0,0,0,0.1);">
                    <div style="display: flex; flex-direction: column;">
                        <p style="font-size: 0.875rem; color: #9a3412; font-weight: 600; margin-bottom: 0.5rem;">Platform Fee</p>
                        <p style="font-size: 1.5rem; font-weight: 700; color: #7c2d12; margin-bottom: 0.5rem;">${platformFee.toLocaleString('vi-VN')}₫</p>
                        <div style="padding-top: 0.5rem; border-top: 1px solid rgba(154, 52, 18, 0.2);">
                            <div style="font-size: 0.7rem; color: #c2410c; display: flex; flex-direction: column; gap: 0.125rem;">
                                <div style="display: flex; justify-content: space-between;">
                                    <span>Rental fee (20%):</span>
                                    <span style="font-weight: 600;">${(revenueStats.rentalFee || 0).toLocaleString('vi-VN')}₫</span>
                                </div>
                                <div style="display: flex; justify-content: space-between;">
                                    <span>Sales fee (10%):</span>
                                    <span style="font-weight: 600;">${(revenueStats.purchaseFee || 0).toLocaleString('vi-VN')}₫</span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
            
            <!-- Revenue Trend Chart -->
            ${revenueStats.chartData && revenueStats.chartData.length > 0 ? `
                <div style="background: white; padding: 1.25rem; border-radius: 0.75rem; margin-bottom: 1.5rem; border: 1px solid #e5e7eb;">
                    <h3 style="font-size: 1.125rem; font-weight: 600; color: #1f2937; margin-bottom: 1rem;">Revenue Trend</h3>
                    <div style="position: relative; height: 250px;">
                        <canvas id="providerRevenueTrendChart"></canvas>
                    </div>
                </div>
            ` : ''}
            
            <!-- Order Status Breakdown Chart -->
            ${renderOrderStatusChart(revenueStats.statusBreakdown)}
        `;
    } else {
        // Fallback: Basic stats from transactions
        const totalOrders = transactions.length;
        const totalRevenue = summary?.totalReceived || summary?.TotalReceived || 0;
        const avgOrderValue = totalOrders > 0 ? (totalRevenue / totalOrders) : 0;
        
        statsHtml = `
            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; margin-bottom: 1.5rem;">
                <div style="background: linear-gradient(135deg, #d1fae5 0%, #a7f3d0 100%); padding: 1.25rem; border-radius: 0.75rem; box-shadow: 0 1px 3px rgba(0,0,0,0.1);">
                    <div style="display: flex; flex-direction: column;">
                        <p style="font-size: 0.875rem; color: #065f46; font-weight: 600; margin-bottom: 0.5rem;">Total Revenue</p>
                        <p style="font-size: 1.5rem; font-weight: 700; color: #064e3b;">${totalRevenue.toLocaleString('vi-VN')}₫</p>
                    </div>
                </div>
                <div style="background: linear-gradient(135deg, #dbeafe 0%, #bfdbfe 100%); padding: 1.25rem; border-radius: 0.75rem; box-shadow: 0 1px 3px rgba(0,0,0,0.1);">
                    <div style="display: flex; flex-direction: column;">
                        <p style="font-size: 0.875rem; color: #1e40af; font-weight: 600; margin-bottom: 0.5rem;">Total Transactions</p>
                        <p style="font-size: 1.5rem; font-weight: 700; color: #1e3a8a;">${totalOrders}</p>
                    </div>
                </div>
                <div style="background: linear-gradient(135deg, #e9d5ff 0%, #d8b4fe 100%); padding: 1.25rem; border-radius: 0.75rem; box-shadow: 0 1px 3px rgba(0,0,0,0.1);">
                    <div style="display: flex; flex-direction: column;">
                        <p style="font-size: 0.875rem; color: #6b21a8; font-weight: 600; margin-bottom: 0.5rem;">Avg Transaction Value</p>
                        <p style="font-size: 1.5rem; font-weight: 700; color: #581c87;">${avgOrderValue.toLocaleString('vi-VN')}₫</p>
                    </div>
                </div>
            </div>
        `;
    }
    
    modalBody.innerHTML = `
        <div style="padding: 1.5rem;">
            ${statsHtml}

            <!-- Bank Account Info -->
            ${bankAccount ? `
                <div style="background: #f9fafb; padding: 1.25rem; border-radius: 0.75rem; margin-bottom: 1.5rem; border: 1px solid #e5e7eb;">
                    <h3 style="font-size: 1.125rem; font-weight: 600; color: #1f2937; margin-bottom: 1rem; display: flex; align-items: center;">
                        <svg style="width: 1.25rem; height: 1.25rem; margin-right: 0.5rem;" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <rect x="1" y="4" width="22" height="16" rx="2" ry="2"></rect>
                            <line x1="1" y1="10" x2="23" y2="10"></line>
                        </svg>
                        Bank Account Information
                    </h3>
                    <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 1rem; font-size: 0.875rem;">
                        <div>
                            <p style="color: #6b7280; margin-bottom: 0.25rem;">Bank Name</p>
                            <p style="font-weight: 600; color: #1f2937;">${bankAccount.bankName || bankAccount.BankName || 'N/A'}</p>
                        </div>
                        <div>
                            <p style="color: #6b7280; margin-bottom: 0.25rem;">Account Number</p>
                            <p style="font-weight: 600; color: #1f2937;">${bankAccount.accountNumber || bankAccount.AccountNumber || 'N/A'}</p>
                        </div>
                        <div>
                            <p style="color: #6b7280; margin-bottom: 0.25rem;">Account Holder</p>
                            <p style="font-weight: 600; color: #1f2937;">${bankAccount.accountHolderName || bankAccount.AccountHolderName || 'N/A'}</p>
                        </div>
                    </div>
                </div>
            ` : ''}
            
            <!-- Recent Transactions -->
            <div>
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem;">
                    <h3 style="font-size: 1.125rem; font-weight: 600; color: #1f2937; display: flex; align-items: center; margin: 0;">
                        <svg style="width: 1.25rem; height: 1.25rem; margin-right: 0.5rem;" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <line x1="8" y1="6" x2="21" y2="6"></line>
                            <line x1="8" y1="12" x2="21" y2="12"></line>
                            <line x1="8" y1="18" x2="21" y2="18"></line>
                            <line x1="3" y1="6" x2="3.01" y2="6"></line>
                            <line x1="3" y1="12" x2="3.01" y2="12"></line>
                            <line x1="3" y1="18" x2="3.01" y2="18"></line>
                        </svg>
                        Recent Transactions
                    </h3>
                    <input type="text" id="transactionSearch" placeholder="Search by order..." 
                           style="padding: 0.5rem 0.75rem; border: 1px solid #d1d5db; border-radius: 0.375rem; font-size: 0.875rem; width: 200px;"
                           oninput="filterTransactions()" />
                </div>
                <div id="transactionsContainer"></div>
            </div>
        </div>
    `;
    
    // Render initial transactions
    renderTransactionsTable();
    
    // Initialize charts if revenue stats available
    if (revenueStats) {
        setTimeout(() => initializeProviderCharts(revenueStats), 100);
    }
}

// Helper function: Render Order Status Breakdown Chart (not grid)
function renderOrderStatusChart(statusBreakdown) {
    if (!statusBreakdown || statusBreakdown.length === 0) {
        return '';
    }
    
    return `
        <div style="background: white; padding: 1.25rem; border-radius: 0.75rem; margin-bottom: 1.5rem; border: 1px solid #e5e7eb;">
            <h3 style="font-size: 1.125rem; font-weight: 600; color: #1f2937; margin-bottom: 1rem;">Order Status Breakdown</h3>
            <div style="position: relative; height: 250px;">
                <canvas id="providerOrderStatusChart"></canvas>
            </div>
        </div>
    `;
}

// Initialize provider charts
function initializeProviderCharts(revenueStats) {
    // Destroy existing charts if any
    if (window.providerRevenueTrendChart && typeof window.providerRevenueTrendChart.destroy === 'function') {
        window.providerRevenueTrendChart.destroy();
    }
    if (window.providerOrderStatusChart && typeof window.providerOrderStatusChart.destroy === 'function') {
        window.providerOrderStatusChart.destroy();
    }
    
    // Revenue Trend Chart
    if (revenueStats.chartData && revenueStats.chartData.length > 0) {
        const trendCtx = document.getElementById('providerRevenueTrendChart');
        if (trendCtx) {
            window.providerRevenueTrendChart = new Chart(trendCtx, {
                type: 'line',
                data: {
                    labels: revenueStats.chartData.map(d => d.period),
                    datasets: [{
                        label: 'Revenue',
                        data: revenueStats.chartData.map(d => d.revenue),
                        borderColor: '#3b82f6',
                        backgroundColor: 'rgba(59, 130, 246, 0.1)',
                        tension: 0.4,
                        fill: true
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            display: false
                        },
                        tooltip: {
                            callbacks: {
                                label: function(context) {
                                    return context.parsed.y.toLocaleString('vi-VN') + '₫';
                                }
                            }
                        }
                    },
                    scales: {
                        y: {
                            beginAtZero: true,
                            ticks: {
                                callback: function(value) {
                                    return (value / 1000).toLocaleString('vi-VN') + 'k₫';
                                }
                            }
                        }
                    }
                }
            });
        }
    }
    
    // Order Status Chart
    if (revenueStats.statusBreakdown && revenueStats.statusBreakdown.length > 0) {
        const statusCtx = document.getElementById('providerOrderStatusChart');
        if (statusCtx) {
            const statusColors = {
                'pending': '#f59e0b',
                'approved': '#3b82f6',
                'in_use': '#8b5cf6',
                'returned': '#10b981',
                'cancelled': '#ef4444',
                'in_transit': '#06b6d4',
                'returning': '#f97316',
                'returned_with_issue': '#eab308'
            };
            
            window.providerOrderStatusChart = new Chart(statusCtx, {
                type: 'doughnut',
                data: {
                    labels: revenueStats.statusBreakdown.map(s => s.status.replace('_', ' ').toUpperCase()),
                    datasets: [{
                        data: revenueStats.statusBreakdown.map(s => s.count),
                        backgroundColor: revenueStats.statusBreakdown.map(s => 
                            statusColors[s.status] || '#9ca3af'
                        ),
                        borderWidth: 2,
                        borderColor: '#ffffff'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            position: 'right',
                            labels: {
                                generateLabels: function(chart) {
                                    const data = chart.data;
                                    if (data.labels.length && data.datasets.length) {
                                        return data.labels.map((label, i) => {
                                            const dataset = data.datasets[0];
                                            const value = dataset.data[i];
                                            const total = dataset.data.reduce((a, b) => a + b, 0);
                                            const percentage = ((value / total) * 100).toFixed(1);
                                            return {
                                                text: `${label}: ${value} (${percentage}%)`,
                                                fillStyle: dataset.backgroundColor[i],
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
                            callbacks: {
                                label: function(context) {
                                    const label = context.label || '';
                                    const value = context.parsed;
                                    const total = context.dataset.data.reduce((a, b) => a + b, 0);
                                    const percentage = ((value / total) * 100).toFixed(1);
                                    return `${label}: ${value} orders (${percentage}%)`;
                                }
                            }
                        }
                    }
                }
            });
        }
    }
}

// Filter transactions based on search
function filterTransactions() {
    const searchTerm = document.getElementById('transactionSearch')?.value.toLowerCase() || '';
    const allTransactions = window.allTransactions || [];
    
    if (searchTerm) {
        window.filteredTransactions = allTransactions.filter(tx => {
            const orders = tx.orders || tx.Orders || [];
            return orders.some(order => {
                const orderId = order.orderId || order.OrderId || '';
                const orderCode = 'ORD' + orderId.toString().substring(0, 3).toUpperCase();
                return orderCode.toLowerCase().includes(searchTerm);
            });
        });
    } else {
        window.filteredTransactions = [...allTransactions]; // Create new array
    }
    
    window.currentTransactionPage = 1;
    renderTransactionsTable();
}

// Render transactions table with pagination
function renderTransactionsTable() {
    const container = document.getElementById('transactionsContainer');
    if (!container) return;
    
    const transactions = window.filteredTransactions || [];
    const page = window.currentTransactionPage || 1;
    const perPage = window.transactionsPerPage || 10;
    
    const startIdx = (page - 1) * perPage;
    const endIdx = startIdx + perPage;
    const paginatedTransactions = transactions.slice(startIdx, endIdx);
    
    const totalPages = Math.ceil(transactions.length / perPage);
    
    if (transactions.length === 0) {
        container.innerHTML = `
            <div style="text-align: center; padding: 2rem; color: #9ca3af;">
                <svg style="width: 3rem; height: 3rem; margin: 0 auto 0.5rem; opacity: 0.5;" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <polyline points="22 12 18 12 15 21 9 3 6 12 2 12"></polyline>
                </svg>
                <p>No transactions found</p>
            </div>
        `;
        return;
    }
    
    container.innerHTML = `
        <div style="overflow-x: auto; border: 1px solid #e5e7eb; border-radius: 0.5rem; margin-bottom: 1rem;">
            <table style="width: 100%; border-collapse: collapse;">
                <thead style="background: #f9fafb;">
                    <tr>
                        <th style="padding: 0.75rem 1rem; text-align: left; font-size: 0.75rem; font-weight: 600; color: #6b7280; text-transform: uppercase; border-bottom: 1px solid #e5e7eb;">Order</th>
                        <th style="padding: 0.75rem 1rem; text-align: left; font-size: 0.75rem; font-weight: 600; color: #6b7280; text-transform: uppercase; border-bottom: 1px solid #e5e7eb;">Date</th>
                        <th style="padding: 0.75rem 1rem; text-align: left; font-size: 0.75rem; font-weight: 600; color: #6b7280; text-transform: uppercase; border-bottom: 1px solid #e5e7eb;">Amount</th>
                        <th style="padding: 0.75rem 1rem; text-align: left; font-size: 0.75rem; font-weight: 600; color: #6b7280; text-transform: uppercase; border-bottom: 1px solid #e5e7eb;">Status</th>
                    </tr>
                </thead>
                <tbody style="background: white;">
                    ${paginatedTransactions.map(tx => {
                        // Generate OrderCode from Orders array
                        let orderDisplay = 'N/A';
                        
                        const orders = tx.orders || tx.Orders || [];
                        
                        if (orders && orders.length > 0) {
                            const firstOrder = orders[0];
                            const orderId = firstOrder.orderId || firstOrder.OrderId;
                            
                            if (orderId) {
                                const orderCode = 'ORD' + orderId.toString().substring(0, 3).toUpperCase();
                                
                                if (orders.length === 1) {
                                    orderDisplay = orderCode;
                                } else {
                                    orderDisplay = `${orderCode} (+${orders.length - 1})`;
                                }
                            }
                        }
                        
                        const transactionDate = tx.transactionDate || tx.TransactionDate;
                        const amount = tx.amount || tx.Amount || 0;
                        const status = tx.status || tx.Status || 'N/A';
                        
                        let statusStyle = 'background: #f3f4f6; color: #374151;';
                        if (status.toLowerCase() === 'completed') {
                            statusStyle = 'background: #d1fae5; color: #065f46;';
                        } else if (status.toLowerCase() === 'pending') {
                            statusStyle = 'background: #fef3c7; color: #92400e;';
                        }
                        
                        return `
                            <tr style="border-bottom: 1px solid #f3f4f6;">
                                <td style="padding: 0.75rem 1rem; font-size: 0.875rem; color: #1f2937; font-weight: 600; font-family: 'Courier New', monospace;">
                                    ${orderDisplay}
                                </td>
                                <td style="padding: 0.75rem 1rem; font-size: 0.875rem; color: #6b7280;">
                                    ${transactionDate ? new Date(transactionDate).toLocaleDateString('vi-VN') : 'N/A'}
                                </td>
                                <td style="padding: 0.75rem 1rem; font-size: 0.875rem; font-weight: 600; color: #10b981;">
                                    ${amount.toLocaleString('vi-VN')}₫
                                </td>
                                <td style="padding: 0.75rem 1rem;">
                                    <span style="display: inline-block; padding: 0.25rem 0.625rem; font-size: 0.75rem; font-weight: 600; border-radius: 9999px; ${statusStyle}">
                                        ${status}
                                    </span>
                                </td>
                            </tr>
                        `;
                    }).join('')}
                </tbody>
            </table>
        </div>
        
        <!-- Pagination - Always visible for consistent layout -->
        <div style="display: flex; justify-content: space-between; align-items: center; padding: 0.75rem 0; min-height: 3rem;">
            <div style="font-size: 0.875rem; color: #6b7280;">
                ${transactions.length > 0 ? `Showing ${startIdx + 1}-${Math.min(endIdx, transactions.length)} of ${transactions.length} transactions` : 'No transactions'}
            </div>
            <div style="display: flex; gap: 0.5rem;">
                <button onclick="changeProviderTransactionPage(${page - 1})" 
                        ${page === 1 || totalPages === 0 ? 'disabled' : ''}
                        style="padding: 0.5rem 0.75rem; border: 1px solid #d1d5db; border-radius: 0.375rem; background: white; cursor: ${page === 1 || totalPages === 0 ? 'not-allowed' : 'pointer'}; font-size: 0.875rem; opacity: ${page === 1 || totalPages === 0 ? '0.5' : '1'};">
                    Previous
                </button>
                <span style="padding: 0.5rem 0.75rem; font-size: 0.875rem; color: #6b7280;">
                    Page ${totalPages > 0 ? page : 0} of ${totalPages}
                </span>
                <button onclick="changeProviderTransactionPage(${page + 1})" 
                        ${page === totalPages || totalPages === 0 ? 'disabled' : ''}
                        style="padding: 0.5rem 0.75rem; border: 1px solid #d1d5db; border-radius: 0.375rem; background: white; cursor: ${page === totalPages || totalPages === 0 ? 'not-allowed' : 'pointer'}; font-size: 0.875rem; opacity: ${page === totalPages || totalPages === 0 ? '0.5' : '1'};">
                    Next
                </button>
            </div>
        </div>
    `;
}

// Change transaction page for provider modal
function changeProviderTransactionPage(newPage) {
    const totalPages = Math.ceil((window.filteredTransactions || []).length / (window.transactionsPerPage || 10));
    if (newPage >= 1 && newPage <= totalPages) {
        window.currentTransactionPage = newPage;
        renderTransactionsTable();
    }
}

// Change provider period and reload data
function changeProviderPeriod(newPeriod) {
    if (!window.currentProviderId || !window.currentProviderName) {
        return;
    }
    
    viewProviderDetails(window.currentProviderId, window.currentProviderName, newPeriod);
}

// Close provider modal
function closeProviderModal() {
    const modal = document.getElementById('providerDetailModal');
    modal.style.display = 'none';
    document.body.style.overflow = '';
    
    // Clear current provider info
    window.currentProviderId = null;
    window.currentProviderName = null;
    window.currentProviderPeriod = 'month';
}

// Close modal on ESC key
document.addEventListener('keydown', function(e) {
    if (e.key === 'Escape') {
        closeProviderModal();
    }
});

// Prevent Enter key from submitting form when searching providers
document.addEventListener('DOMContentLoaded', function() {
    const searchInput = document.getElementById('providerSearchInput');
    if (searchInput) {
        searchInput.addEventListener('keydown', function(e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                return false;
            }
        });
    }
});

