// Pagination functionality for Dispute Resolution Center
let currentPage = 1;
const itemsPerPage = 8;

function renderDisputesWithPagination() {
    const tbody = $('#dispute-table-body');
    tbody.empty();

    if (filteredDisputes.length === 0) {
        tbody.append(`
            <tr>
                <td colspan="7" class="px-6 py-12 text-center">
                    <div class="flex flex-col items-center justify-center">
                        <i data-lucide="alert-circle" class="h-16 w-16 text-gray-400 mb-4"></i>
                        <h3 class="text-xl font-semibold text-gray-700 mb-2">No Pending Cases</h3>
                        <p class="text-gray-600">All disputes have been resolved or no cases match your filters</p>
                    </div>
                </td>
            </tr>
        `);
        $('#pagination-container').empty();
    } else {
        // Calculate pagination
        const totalPages = Math.ceil(filteredDisputes.length / itemsPerPage);
        const startIndex = (currentPage - 1) * itemsPerPage;
        const endIndex = Math.min(startIndex + itemsPerPage, filteredDisputes.length);
        const paginatedDisputes = filteredDisputes.slice(startIndex, endIndex);

        // Render disputes for current page
        paginatedDisputes.forEach(dispute => {
            const row = `
                <tr class="case-row transition-colors">
                    <td class="px-6 py-4">
                        <span class="font-mono text-sm text-gray-900">${dispute.violationId.substring(0, 8)}...</span>
                    </td>
                    <td class="px-6 py-4">
                        <span class="font-medium text-gray-900">${escapeHtml(dispute.productName)}</span>
                    </td>
                    <td class="px-6 py-4">
                        <span class="text-gray-700">${escapeHtml(dispute.providerName)}</span>
                    </td>
                    <td class="px-6 py-4">
                        <span class="text-gray-700">${escapeHtml(dispute.customerName)}</span>
                    </td>
                    <td class="px-6 py-4">
                        <span class="text-gray-700">${formatDate(dispute.complaintDate)}</span>
                    </td>
                    <td class="px-6 py-4">
                        <span class="font-semibold text-red-600">₫${dispute.requestedCompensation.toLocaleString('vi-VN')}</span>
                    </td>
                    <td class="px-6 py-4 text-center">
                        <a href="/Admin/Compensation/Details/${dispute.violationId}" 
                           class="inline-flex items-center gap-2 bg-red-600 hover:bg-red-700 text-white px-4 py-2 rounded-lg font-medium transition-colors">
                            <i data-lucide="eye" class="w-4 h-4"></i>
                            View Details
                        </a>
                    </td>
                </tr>
            `;
            tbody.append(row);
        });

        // Render pagination
        renderPagination(totalPages);
    }

    // Re-initialize Lucide icons
    lucide.createIcons();

    // Update info
    updateInfoWithPagination();
}

function renderPagination(totalPages) {
    const container = $('#pagination-container');
    container.empty();

    if (totalPages <= 1) return;

    // Previous button
    const prevBtn = `
        <button onclick="changePage(${currentPage - 1})" 
                ${currentPage === 1 ? 'disabled' : ''}
                class="px-3 py-1 border rounded-lg ${currentPage === 1 ? 'bg-gray-100 text-gray-400 cursor-not-allowed' : 'bg-white text-gray-700 hover:bg-gray-50'}">
            <i data-lucide="chevron-left" class="w-4 h-4"></i>
        </button>
    `;
    container.append(prevBtn);

    // Page numbers
    const maxVisiblePages = 5;
    let startPage = Math.max(1, currentPage - Math.floor(maxVisiblePages / 2));
    let endPage = Math.min(totalPages, startPage + maxVisiblePages - 1);

    if (endPage - startPage < maxVisiblePages - 1) {
        startPage = Math.max(1, endPage - maxVisiblePages + 1);
    }

    if (startPage > 1) {
        container.append(`
            <button onclick="changePage(1)" class="px-3 py-1 border rounded-lg bg-white text-gray-700 hover:bg-gray-50">1</button>
        `);
        if (startPage > 2) {
            container.append(`<span class="px-2 text-gray-500">...</span>`);
        }
    }

    for (let i = startPage; i <= endPage; i++) {
        const pageBtn = `
            <button onclick="changePage(${i})" 
                    class="px-3 py-1 border rounded-lg ${i === currentPage ? 'bg-red-600 text-white' : 'bg-white text-gray-700 hover:bg-gray-50'}">
                ${i}
            </button>
        `;
        container.append(pageBtn);
    }

    if (endPage < totalPages) {
        if (endPage < totalPages - 1) {
            container.append(`<span class="px-2 text-gray-500">...</span>`);
        }
        container.append(`
            <button onclick="changePage(${totalPages})" class="px-3 py-1 border rounded-lg bg-white text-gray-700 hover:bg-gray-50">${totalPages}</button>
        `);
    }

    // Next button
    const nextBtn = `
        <button onclick="changePage(${currentPage + 1})" 
                ${currentPage === totalPages ? 'disabled' : ''}
                class="px-3 py-1 border rounded-lg ${currentPage === totalPages ? 'bg-gray-100 text-gray-400 cursor-not-allowed' : 'bg-white text-gray-700 hover:bg-gray-50'}">
            <i data-lucide="chevron-right" class="w-4 h-4"></i>
        </button>
    `;
    container.append(nextBtn);

    lucide.createIcons();
}

function changePage(page) {
    const totalPages = Math.ceil(filteredDisputes.length / itemsPerPage);
    if (page < 1 || page > totalPages) return;
    currentPage = page;
    renderDisputesWithPagination();
    window.scrollTo({ top: 0, behavior: 'smooth' });
}

function updateInfoWithPagination() {
    const total = filteredDisputes.length;
    const startIndex = (currentPage - 1) * itemsPerPage + 1;
    const endIndex = Math.min(currentPage * itemsPerPage, total);
    
    let resultsText;
    if (total === 0) {
        resultsText = 'No pending cases';
    } else if (total === 1) {
        resultsText = 'Showing 1 pending case';
    } else {
        resultsText = `Showing ${startIndex}-${endIndex} of ${total} pending cases`;
    }
    
    $('#results-info').text(resultsText);
    $('#footer-info').text(resultsText);
}

// Override the original renderDisputes function
if (typeof window.renderDisputes !== 'undefined') {
    window.renderDisputes = renderDisputesWithPagination;
}

// Override the original updateInfo function
if (typeof window.updateInfo !== 'undefined') {
    window.updateInfo = updateInfoWithPagination;
}

// Reset to page 1 when filters change
const originalApplyFilters = window.applyFilters;
if (originalApplyFilters) {
    window.applyFilters = function() {
        currentPage = 1;
        originalApplyFilters();
    };
}
