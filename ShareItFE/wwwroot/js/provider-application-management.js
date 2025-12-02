// Provider Application Management JavaScript
class ProviderApplicationManagement {
    constructor() {
        this.config = window.providerApplicationConfig || {};
        this.applications = [];
        this.filteredApplications = [];
        this.currentPage = 1;
        this.itemsPerPage = 10;
        this.searchQuery = '';
        this.statusFilter = 'all';
        this.providerTypeFilter = 'all';
        this.sortBy = 'created-desc';
        this.selectedApplication = null;

        this.init();
    }

    init() {
        this.createToastContainer();
        this.bindEvents();
        this.loadApplications();
    }

    bindEvents() {
        // Search
        const searchInput = document.getElementById('searchInput');
        if (searchInput) {
            searchInput.addEventListener('input', (e) => {
                this.searchQuery = e.target.value.trim().toLowerCase();
                this.filterAndRenderApplications();
            });
        }

        // Filters
        const statusFilter = document.getElementById('statusFilter');
        if (statusFilter) {
            statusFilter.addEventListener('change', (e) => {
                this.statusFilter = e.target.value;
                this.loadApplications();
            });
        }

        const sortFilter = document.getElementById('sortFilter');
        if (sortFilter) {
            sortFilter.addEventListener('change', (e) => {
                this.sortBy = e.target.value;
                this.filterAndRenderApplications();
            });
        }

        const providerTypeFilter = document.getElementById('providerTypeFilter');
        if (providerTypeFilter) {
            providerTypeFilter.addEventListener('change', (e) => {
                this.providerTypeFilter = e.target.value;
                this.filterAndRenderApplications();
            });
        }

        // Pagination
        const prevBtn = document.getElementById('prevPageBtn');
        const nextBtn = document.getElementById('nextPageBtn');

        if (prevBtn) {
            prevBtn.addEventListener('click', () => this.goToPage(this.currentPage - 1));
        }

        if (nextBtn) {
            nextBtn.addEventListener('click', () => this.goToPage(this.currentPage + 1));
        }

        // Modal close buttons
        window.closeApproveModal = () => this.hideModal('approveModal');
        window.closeRejectModal = () => this.hideModal('rejectModal');

        // Confirm buttons
        const confirmApproveBtn = document.getElementById('confirmApproveBtn');
        if (confirmApproveBtn) {
            confirmApproveBtn.addEventListener('click', () => this.handleApprove());
        }

        const confirmRejectBtn = document.getElementById('confirmRejectBtn');
        if (confirmRejectBtn) {
            confirmRejectBtn.addEventListener('click', () => this.handleReject());
        }
    }

    async loadApplications() {
        try {
            this.showLoading(true);

            const statusParam = this.statusFilter !== 'all' ? `?status=${this.statusFilter}` : '';
            const response = await fetch(`${this.config.apiBaseUrl}/provider-applications${statusParam}`, {
                headers: {
                    'Authorization': `Bearer ${this.config.accessToken}`,
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const result = await response.json();
            this.applications = result.data || [];

            this.updateStatistics();
            this.filterAndRenderApplications();

        } catch (error) {
            console.error('Error loading applications:', error);
            this.showToast('error', 'Failed to load applications. Please try again.');
        } finally {
            this.showLoading(false);
        }
    }

    filterAndRenderApplications() {
        // Filter by search
        this.filteredApplications = this.applications.filter(app => {
            if (!this.searchQuery) return true;

            const businessName = (app.businessName || '').toLowerCase();
            const applicantName = (app.user?.profile?.fullName || '').toLowerCase();
            const applicantEmail = (app.user?.email || '').toLowerCase();
            const taxId = (app.taxId || '').toLowerCase();

            return businessName.includes(this.searchQuery) ||
                applicantName.includes(this.searchQuery) ||
                applicantEmail.includes(this.searchQuery) ||
                taxId.includes(this.searchQuery);
        });

        // Filter by provider type (derived)
        if (this.providerTypeFilter !== 'all') {
            this.filteredApplications = this.filteredApplications.filter(app => {
                const type = this.deriveProviderType(app);
                return type === this.providerTypeFilter;
            });
        }

        // Sort
        this.sortApplications();

        // Update UI
        this.currentPage = 1;
        this.renderTable();
        this.renderPagination();
        this.updateShowingCount();
    }

    deriveProviderType(app) {
        // Use providerType from database if available
        if (app.providerType) {
            return app.providerType.toLowerCase();
        }

        // Fallback: derive from Tax ID length
        const taxId = (app.taxId || '').replace(/\D/g, '');
        if (taxId.length === 12) return 'individual';
        if (taxId.length === 10) return 'business';
        return 'unknown';
    }

    sortApplications() {
        const [field, order] = this.sortBy.split('-');

        this.filteredApplications.sort((a, b) => {
            let aValue, bValue;

            switch (field) {
                case 'name':
                    aValue = (a.businessName || '').toLowerCase();
                    bValue = (b.businessName || '').toLowerCase();
                    break;
                case 'created':
                    aValue = new Date(a.createdAt);
                    bValue = new Date(b.createdAt);
                    break;
                default:
                    return 0;
            }

            if (order === 'asc') {
                return aValue > bValue ? 1 : -1;
            } else {
                return aValue < bValue ? 1 : -1;
            }
        });
    }

    renderTable() {
        const tbody = document.getElementById('applicationsTableBody');
        const emptyState = document.getElementById('emptyState');

        if (!tbody) return;

        if (this.filteredApplications.length === 0) {
            tbody.innerHTML = '';
            emptyState.style.display = 'block';
            return;
        }

        emptyState.style.display = 'none';

        // Paginate
        const startIndex = (this.currentPage - 1) * this.itemsPerPage;
        const endIndex = startIndex + this.itemsPerPage;
        const pageApplications = this.filteredApplications.slice(startIndex, endIndex);

        tbody.innerHTML = pageApplications.map(app => this.renderApplicationRow(app)).join('');
    }

    renderApplicationRow(app) {
        const statusClass = app.status === 'pending' ? 'pending' :
            app.status === 'approved' ? 'approved' : 'rejected';

        const statusText = app.status === 'pending' ? 'Pending' :
            app.status === 'approved' ? 'Approved' : 'Rejected';

        const createdDate = new Date(app.createdAt);
        const formattedDate = createdDate.toLocaleDateString('en-US', {
            month: 'short',
            day: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });

        const applicantName = app.user?.profile?.fullName || 'N/A';
        const applicantEmail = app.user?.email || 'N/A';

        const type = this.deriveProviderType(app);
        const taxHtml = app.taxId
            ? this.escapeHtml(app.taxId)
            : '<span class="text-muted">N/A</span>';

        const actions = app.status === 'pending' ? `
            <button class="action-btn view-btn" onclick="viewApplicationDetails('${app.id}')" title="View Details">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path>
                    <circle cx="12" cy="12" r="3"></circle>
                </svg>
            </button>
            ${type === 'business' ? `
                <button class="action-btn" onclick="window.open('https://tracuunnt.gdt.gov.vn/', '_blank')" 
                        title="Check Tax ID" 
                        style="background: #3b82f6; color: white;">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path>
                        <polyline points="15 3 21 3 21 9"></polyline>
                        <line x1="10" y1="14" x2="21" y2="3"></line>
                    </svg>
                </button>
            ` : ''}
            <button class="action-btn approve-btn" onclick="providerAppMgmt.showApproveModal('${app.id}')" title="Approve">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
                    <polyline points="22 4 12 14.01 9 11.01"></polyline>
                </svg>
            </button>
            <button class="action-btn reject-btn" onclick="providerAppMgmt.showRejectModal('${app.id}')" title="Reject">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <circle cx="12" cy="12" r="10"></circle>
                    <line x1="15" y1="9" x2="9" y2="15"></line>
                    <line x1="9" y1="9" x2="15" y2="15"></line>
                </svg>
            </button>
        ` : `
            <button class="action-btn view-btn" onclick="viewApplicationDetails('${app.id}')" title="View Details">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path>
                    <circle cx="12" cy="12" r="3"></circle>
                </svg>
            </button>
            ${type === 'business' ? `
                <button class="action-btn" onclick="window.open('https://tracuunnt.gdt.gov.vn/', '_blank')" 
                        title="Check Tax ID" 
                        style="background: #3b82f6; color: white;">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path>
                        <polyline points="15 3 21 3 21 9"></polyline>
                        <line x1="10" y1="14" x2="21" y2="3"></line>
                    </svg>
                </button>
            ` : ''}
        `;

        return `
            <tr>
                <td>
                    <div class="business-info">
                        <div class="business-name">${this.escapeHtml(app.businessName)}</div>
                        ${app.notes ? `<div class="business-notes">${this.escapeHtml(app.notes)}</div>` : ''}
                    </div>
                </td>
                <td>
                    <div class="applicant-info">
                        <div class="applicant-name">${this.escapeHtml(applicantName)}</div>
                        <div class="applicant-email">${this.escapeHtml(applicantEmail)}</div>
                    </div>
                </td>
                <td>${taxHtml}</td>
                <td>
                    <span class="provider-type-badge provider-type-${type}">
                        ${type === 'individual' ? '👤 Individual' : type === 'business' ? '🏢 Business' : '❓ Unknown'}
                    </span>
                </td>
                <td>
                    <span class="status-badge status-${statusClass}">${statusText}</span>
                </td>
                <td>
                    <div class="date-info">${formattedDate}</div>
                </td>
                <td>
                    <div class="action-buttons">
                        ${actions}
                    </div>
                </td>
            </tr>
        `;
    }

    showApproveModal(applicationId) {
        const app = this.applications.find(a => a.id === applicationId);
        if (!app) return;

        this.selectedApplication = app;

        const message = document.getElementById('approveMessage');
        if (message) {
            message.textContent = `Are you sure you want to approve the application for "${app.businessName}"?`;
        }

        this.showModal('approveModal');
    }

    showRejectModal(applicationId) {
        const app = this.applications.find(a => a.id === applicationId);
        if (!app) return;

        this.selectedApplication = app;

        // Populate modal
        document.getElementById('rejectBusinessName').textContent = app.businessName;
        document.getElementById('rejectApplicantName').textContent = app.user?.profile?.fullName || 'N/A';
        document.getElementById('rejectApplicantEmail').textContent = app.user?.email || 'N/A';
        document.getElementById('rejectionReason').value = '';

        this.showModal('rejectModal');
    }

    async handleApprove() {
        if (!this.selectedApplication) return;

        try {
            const btn = document.getElementById('confirmApproveBtn');
            btn.disabled = true;
            btn.textContent = 'Approving...';

            const response = await fetch(`${this.config.apiBaseUrl}/provider-applications/approve/${this.selectedApplication.id}`, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${this.config.accessToken}`,
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                throw new Error('Failed to approve application');
            }

            this.showToast('success', 'Application approved successfully! Notification email sent.');
            this.hideModal('approveModal');
            this.loadApplications();

        } catch (error) {
            console.error('Error approving application:', error);
            this.showToast('error', 'Failed to approve application. Please try again.');
        } finally {
            const btn = document.getElementById('confirmApproveBtn');
            btn.disabled = false;
            btn.textContent = 'Approve';
        }
    }

    async handleReject() {
        if (!this.selectedApplication) return;

        const reason = document.getElementById('rejectionReason').value.trim();
        if (!reason) {
            this.showToast('warning', 'Please enter a rejection reason.');
            return;
        }

        try {
            const btn = document.getElementById('confirmRejectBtn');
            btn.disabled = true;
            btn.textContent = 'Rejecting...';

            const response = await fetch(`${this.config.apiBaseUrl}/provider-applications/reject/${this.selectedApplication.id}`, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${this.config.accessToken}`,
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ rejectionReason: reason })
            });

            if (!response.ok) {
                throw new Error('Failed to reject application');
            }

            this.showToast('success', 'Application rejected. Notification email sent to applicant.');
            this.hideModal('rejectModal');
            this.loadApplications();

        } catch (error) {
            console.error('Error rejecting application:', error);
            this.showToast('error', 'Failed to reject application. Please try again.');
        } finally {
            const btn = document.getElementById('confirmRejectBtn');
            btn.disabled = false;
            btn.textContent = 'Reject Application';
        }
    }

    updateStatistics() {
        const today = new Date();
        today.setHours(0, 0, 0, 0);

        const pending = this.applications.filter(a => a.status === 'pending').length;
        const approvedToday = this.applications.filter(a => {
            if (a.status !== 'approved' || !a.reviewedAt) return false;
            const reviewDate = new Date(a.reviewedAt);
            return reviewDate >= today;
        }).length;
        const rejectedToday = this.applications.filter(a => {
            if (a.status !== 'rejected' || !a.reviewedAt) return false;
            const reviewDate = new Date(a.reviewedAt);
            return reviewDate >= today;
        }).length;

        document.getElementById('pendingCount').textContent = pending;
        document.getElementById('approvedToday').textContent = approvedToday;
        document.getElementById('rejectedToday').textContent = rejectedToday;
    }

    updateShowingCount() {
        const showingCount = document.getElementById('showingCount');
        if (showingCount) {
            showingCount.textContent = this.filteredApplications.length;
        }
    }

    renderPagination() {
        const paginationContainer = document.getElementById('paginationContainer');
        if (!paginationContainer) return;

        const totalPages = Math.ceil(this.filteredApplications.length / this.itemsPerPage);

        if (totalPages <= 1) {
            paginationContainer.style.display = 'none';
            return;
        }

        paginationContainer.style.display = 'flex';

        // Update info
        const startIndex = (this.currentPage - 1) * this.itemsPerPage + 1;
        const endIndex = Math.min(this.currentPage * this.itemsPerPage, this.filteredApplications.length);

        document.getElementById('paginationStart').textContent = startIndex;
        document.getElementById('paginationEnd').textContent = endIndex;
        document.getElementById('paginationTotal').textContent = this.filteredApplications.length;

        // Update buttons
        const prevBtn = document.getElementById('prevPageBtn');
        const nextBtn = document.getElementById('nextPageBtn');

        prevBtn.disabled = this.currentPage === 1;
        nextBtn.disabled = this.currentPage === totalPages;

        // Render page numbers
        const numbersContainer = document.getElementById('paginationNumbers');
        let html = '';

        for (let i = 1; i <= totalPages; i++) {
            if (i === 1 || i === totalPages || (i >= this.currentPage - 1 && i <= this.currentPage + 1)) {
                html += `
                    <button class="page-number-btn ${i === this.currentPage ? 'active' : ''}" 
                            onclick="providerAppMgmt.goToPage(${i})">
                        ${i}
                    </button>
                `;
            } else if (i === this.currentPage - 2 || i === this.currentPage + 2) {
                html += '<span class="page-dots">...</span>';
            }
        }

        numbersContainer.innerHTML = html;
    }

    goToPage(page) {
        const totalPages = Math.ceil(this.filteredApplications.length / this.itemsPerPage);
        if (page < 1 || page > totalPages) return;

        this.currentPage = page;
        this.renderTable();
        this.renderPagination();
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    showModal(modalId) {
        const modal = document.getElementById(modalId);
        if (modal) {
            modal.style.display = 'flex';
        }
    }

    hideModal(modalId) {
        const modal = document.getElementById(modalId);
        if (modal) {
            modal.style.display = 'none';
        }
        this.selectedApplication = null;
    }

    showLoading(show) {
        const loadingState = document.getElementById('loadingState');
        const tableWrapper = document.querySelector('.table-wrapper');

        if (loadingState) {
            loadingState.style.display = show ? 'block' : 'none';
        }
        if (tableWrapper) {
            tableWrapper.style.display = show ? 'none' : 'block';
        }
    }

    createToastContainer() {
        if (!document.getElementById('toast-container')) {
            const container = document.createElement('div');
            container.id = 'toast-container';
            container.style.cssText = 'position: fixed; top: 20px; right: 20px; z-index: 10000;';
            document.body.appendChild(container);
        }
    }

    showToast(type, message) {
        const container = document.getElementById('toast-container');
        if (!container) return;

        const toast = document.createElement('div');
        toast.className = `toast toast-${type}`;

        const icons = {
            success: '<polyline points="20 6 9 17 4 12"></polyline>',
            error: '<circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line>',
            warning: '<path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line>',
            info: '<circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line>'
        };

        toast.innerHTML = `
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                ${icons[type] || icons.info}
            </svg>
            <span>${message}</span>
        `;

        container.appendChild(toast);

        setTimeout(() => toast.classList.add('show'), 10);

        setTimeout(() => {
            toast.classList.remove('show');
            setTimeout(() => toast.remove(), 300);
        }, 5000);
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}

// Global functions for modals
function closeApproveModal() {
    document.getElementById('approveModal').style.display = 'none';
}

function closeRejectModal() {
    document.getElementById('rejectModal').style.display = 'none';
}

function closeDetailModal() {
    document.getElementById('detailModal').style.display = 'none';
}

function closeImageEnlargeModal() {
    document.getElementById('imageEnlargeModal').style.display = 'none';
}

async function viewApplicationDetails(applicationId) {
    if (!providerAppMgmt) return;

    const app = providerAppMgmt.applications.find(a => a.id === applicationId);
    if (!app) {
        console.error('Application not found:', applicationId);
        return;
    }

    // Populate detail modal
    document.getElementById('detailBusinessName').textContent = app.businessName || 'N/A';
    document.getElementById('detailApplicantName').textContent = app.user?.profile?.fullName || 'N/A';
    document.getElementById('detailApplicantEmail').textContent = app.user?.email || 'N/A';
    document.getElementById('detailTaxId').textContent = app.taxId || 'N/A';
    document.getElementById('detailContactPhone').textContent = app.contactPhone || 'N/A';
    document.getElementById('detailBusinessNameFull').textContent = app.businessName || 'N/A';
    document.getElementById('detailNotes').textContent = app.notes || 'No notes';

    // Fetch signed URLs for private images
    let signedUrls = {};
    let useSignedUrls = false;

    try {
        const response = await fetch(`${providerAppMgmt.config.apiBaseUrl}/provider-applications/${applicationId}/images`, {
            headers: {
                'Authorization': `Bearer ${providerAppMgmt.config.accessToken}`,
                'Content-Type': 'application/json'
            }
        });

        if (response.ok) {
            const result = await response.json();
            signedUrls = result.data || {};
            useSignedUrls = true;
            console.log('Using signed URLs for private images');
        } else {
            console.warn('Failed to fetch signed URLs, falling back to direct URLs');
            useSignedUrls = false;
        }
    } catch (error) {
        console.error('Error fetching signed URLs:', error);
        console.warn('Falling back to direct URLs for old public images');
        useSignedUrls = false;
    }

    // Show ID Card section if images exist
    const idCardSection = document.getElementById('idCardSection');
    const frontUrl = useSignedUrls ? signedUrls.idCardFront : app.idCardFrontImageUrl;
    const backUrl = useSignedUrls ? signedUrls.idCardBack : app.idCardBackImageUrl;

    if (frontUrl && backUrl) {
        idCardSection.style.display = 'block';
        document.getElementById('idCardFrontImage').src = frontUrl;
        document.getElementById('idCardBackImage').src = backUrl;
    } else {
        idCardSection.style.display = 'none';
    }

    // Determine provider type
    const type = providerAppMgmt.deriveProviderType(app);

    // Show Selfie section for Individual
    const selfieSection = document.getElementById('selfieSection');
    if (selfieSection) {
        const selfieUrl = useSignedUrls ? signedUrls.selfie : app.selfieImageUrl;
        if (type === 'individual' && selfieUrl) {
            selfieSection.style.display = 'block';
            document.getElementById('selfieImage').src = selfieUrl;

            const faceMatchInfo = document.getElementById('faceMatchInfo');
            if (app.faceMatched !== undefined && faceMatchInfo) {
                const matchHtml = app.faceMatched ?
                    '<div style="padding:12px;background:#dcfce7;border-left:3px solid #16a34a;border-radius:4px"><strong style="color:#166534">✓ Face Matched</strong><br><span style="color:#15803d;font-size:13px">Score: ' + (app.faceMatchScore * 100).toFixed(1) + '%</span></div>' :
                    '<div style="padding:12px;background:#fee2e2;border-left:3px solid #dc2626;border-radius:4px"><strong style="color:#991b1b">✗ Not Matched</strong></div>';
                faceMatchInfo.innerHTML = matchHtml;
            }
        } else {
            selfieSection.style.display = 'none';
        }
    }

    // Show Business License for Business
    const businessLicenseSection = document.getElementById('businessLicenseSection');
    if (businessLicenseSection) {
        const businessLicenseUrl = useSignedUrls ? signedUrls.businessLicense : app.businessLicenseImageUrl;
        if (type === 'business' && businessLicenseUrl) {
            businessLicenseSection.style.display = 'block';
            document.getElementById('businessLicenseImage').src = businessLicenseUrl;
        } else {
            businessLicenseSection.style.display = 'none';
        }
    }

    document.getElementById('detailModal').style.display = 'flex';
}

function enlargeImage(imgElement) {
    openPAImageLightbox(imgElement.src);
}

// Product Administration-like lightbox for PA
let paScale = 1, paX = 0, paY = 0, paDrag = false, paStartX = 0, paStartY = 0;

function openPAImageLightbox(src) {
    const overlay = document.getElementById('paImageLightbox');
    const img = document.getElementById('paLightboxImage');
    img.src = src;
    img.style.transform = 'translate(0, 0) scale(1)';
    img.style.cursor = 'zoom-in';
    paScale = 1; paX = 0; paY = 0;
    overlay.style.display = 'flex';

    // Wheel zoom
    img.onwheel = (e) => {
        e.preventDefault(); e.stopPropagation();
        const delta = e.deltaY > 0 ? -0.1 : 0.1;
        paScale = Math.min(Math.max(0.5, paScale + delta), 6);
        if (paScale <= 1) { paX = 0; paY = 0; img.style.cursor = 'zoom-in'; paDrag = false; }
        img.style.transform = `translate(${paX}px, ${paY}px) scale(${paScale})`;
    };

    // Drag to pan (when zoomed)
    img.onmousedown = (e) => {
        if (paScale > 1) { paDrag = true; paStartX = e.clientX - paX; paStartY = e.clientY - paY; img.style.cursor = 'grabbing'; }
    };
    document.onmousemove = (e) => {
        if (!paDrag) return; paX = e.clientX - paStartX; paY = e.clientY - paStartY; img.style.transform = `translate(${paX}px, ${paY}px) scale(${paScale})`;
    };
    document.onmouseup = () => { if (paDrag) { paDrag = false; img.style.cursor = paScale > 1 ? 'grab' : 'zoom-in'; } };
}

function togglePAZoom(e) {
    e.stopPropagation();
    const img = e.target;
    if (paScale === 1) { paScale = 2; img.style.cursor = 'grab'; }
    else { paScale = 1; paX = 0; paY = 0; img.style.cursor = 'zoom-in'; }
    img.style.transform = `translate(${paX}px, ${paY}px) scale(${paScale})`;
}

function closePAImageLightbox() {
    const overlay = document.getElementById('paImageLightbox');
    overlay.style.display = 'none';
    paScale = 1; paX = 0; paY = 0; paDrag = false;
}

// Initialize when DOM is ready
let providerAppMgmt;
document.addEventListener('DOMContentLoaded', () => {
    providerAppMgmt = new ProviderApplicationManagement();
    window.providerAppMgmt = providerAppMgmt;
});

