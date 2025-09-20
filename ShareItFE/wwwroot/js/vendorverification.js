// Helper function to format date for Vietnam timezone
function formatDateForVietnam(dateString) {
    try {
        // API now returns Vietnam time directly, no need to convert from UTC
        const date = new Date(dateString);
        
        // Format as Vietnam locale (already Vietnam time)
        return date.toLocaleDateString('vi-VN', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit'
        });
    } catch (e) {
        console.error('Error formatting date:', e, 'Original dateString:', dateString);
        return dateString; // Fallback to original string if parsing fails
    }
}

document.addEventListener('DOMContentLoaded', () => {
    // --- Variables ---
    let items = [];
    let filteredItems = [];

    // All necessary DOM elements are correctly retrieved once at the top
    const itemsTableBody = document.getElementById('items-table-body');
    const noItemsFoundDiv = document.getElementById('no-items-found');
    const searchInput = document.getElementById('search-input');
    const categoryFilter = document.getElementById('category-filter');
    const statusFilter = document.getElementById('status-filter');
    const promoteFilter = document.getElementById('promote-filter');

    // Item Details Modal elements
    const detailsModal = document.getElementById('item-details-modal');
    const detailsCloseButton = document.getElementById('close-button'); // Renamed for clarity
    const modalContentDiv = document.getElementById('modal-content-details');

    // New Action Confirmation Modal elements (NEW)
    const actionConfirmModal = document.getElementById('action-confirm-modal');
    const actionModalTitle = document.getElementById('action-modal-title');
    const actionCloseButton = document.getElementById('action-close-button');
    const actionTextSpan = document.getElementById('action-text');
    const actionProductNameSpan = document.getElementById('action-product-name');
    const reasonGroupDiv = document.getElementById('reason-group');
    const actionReasonTextarea = document.getElementById('action-reason');
    const cancelActionButton = document.getElementById('cancel-action-button');
    const confirmActionButton = document.getElementById('confirm-action-button');

    let currentActionItemId = null; // To store the ID of the item being acted upon
    let currentActionType = null; // To store the type of action (approve, reject, revision)

    // --- Helper Functions ---

    // Function to get status color (e.g., for Tailwind classes)
    const getStatusColor = (status) => {
        switch (status.toLowerCase()) {
            case 'pending':
                return 'status-pending';
            case 'available': // Assuming 'available' from API means it's live/approved
            case 'approved':
                return 'status-approved';
            case 'rejected':
                return 'status-rejected';
            case 'revision':
                return 'status-revision';
            default:
                return 'bg-gray-200 text-gray-800'; // Default gray for unknown statuses
        }
    };

    // Function to get status icon (SVG)
    const getStatusIcon = (status) => {
        switch (status.toLowerCase()) {
            case 'pending':
                return `<svg class="h-3 w-3" fill="currentColor" viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-12a1 1 0 10-2 0v4a1 1 0 00.293.707l3 3a1 1 0 001.414-1.414L11 9.586V6z" clip-rule="evenodd"></path></svg>`;
            case 'available': // Assuming 'available' from API means it's live/approved
            case 'approved':
                return `<svg class="h-3 w-3" fill="currentColor" viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd"></path></svg>`;
            case 'rejected':
                return `<svg class="h-3 w-3" fill="currentColor" viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd"></path></svg>`;
            case 'revision':
                return `<svg class="h-3 w-3" fill="currentColor" viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clip-rule="evenodd"></path></svg>`;
            default:
                return `<svg class="h-3 w-3" fill="currentColor" viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-8.707l-3-3a1 1 0 00-1.414 1.414L10.586 9H7a1 1 0 100 2h3.586l-1.293 1.293a1 1 0 101.414 1.414l3-3a1 1 0 000-1.414z" clip-rule="evenodd"></path></svg>`;
        }
    };

    // --- Render Items Function ---
    const renderItems = () => {
        itemsTableBody.innerHTML = ''; // Clear existing rows
        if (filteredItems.length === 0) {
            noItemsFoundDiv.classList.remove('hidden');
            noItemsFoundDiv.textContent = 'No items found matching your criteria.'; // More specific message
            return;
        } else {
            noItemsFoundDiv.classList.add('hidden');
        }

        filteredItems.forEach(item => {
            const itemSizesDisplay = item.size ? `Sizes: ${item.size}` : 'Sizes: N/A';

            const mainImageUrl = item.primaryImagesUrl && item.primaryImagesUrl !== ""
                ? item.primaryImagesUrl
                : (item.images && item.images.length > 0
                    ? item.images.find(img => img.isPrimary)?.imageUrl || item.images[0].imageUrl
                    : 'https://via.placeholder.com/400x300?text=No+Image');

            // IMPORTANT: The API returns 'available' for products that are live,
            // but your UI logic might treat "pending" as the initial status for
            // actions. Let's make sure the `displayStatus` correctly reflects
            // the state that requires admin action. If 'available' means 'approved',
            // then only 'pending' needs the action buttons.
            const displayStatus = item.availabilityStatus.toLowerCase(); // Use actual status for rendering

            const row = document.createElement('tr');
            row.innerHTML = `
                <td class="px-6 py-4 whitespace-nowrap">
                    <div class="flex items-center">
                        <div class="flex-shrink-0 h-16 w-20">
                            <img class="h-16 w-20 object-cover rounded-md" src="${mainImageUrl}" alt="${item.name}">
                        </div>
                        <div class="ml-4">
                            <div class="text-sm font-medium text-gray-900">${item.name}</div>
                            <div class="text-xs text-gray-500">${item.category || 'N/A'}</div>
                            <div class="text-xs text-purple-600 font-semibold">₫${item.pricePerDay ? item.pricePerDay.toLocaleString('vi-VN') : 'N/A'}/day</div>
                        </div>
                    </div>
                </td>
                <td class="px-6 py-4">
                    <div class="text-sm text-gray-900">
                        <div>${itemSizesDisplay}</div>
                        <div>Color: ${item.color || 'N/A'}</div>
                        <div>Condition: N/A</div> <div class="flex items-center space-x-1 mt-1">
                            <svg class="h-3 w-3 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.828 0L6.343 16.657A8 8 0 1117.657 5.343a8 8 0 010 11.314z"></path>
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"></path>
                            </svg>
                            <span class="text-xs text-gray-500">N/A</span> </div>
                    </div>
                </td>
                <td class="px-6 py-4 whitespace-nowrap">
                    <span class="inline-flex items-center space-x-1 px-2.5 py-0.5 rounded-full text-xs font-medium ${getStatusColor(displayStatus)}">
                        ${getStatusIcon(displayStatus)}
                        <span class="capitalize">${displayStatus.replace('_', ' ')}</span>
                    </span>
                </td>
                <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                    <div class="flex items-center space-x-1">
                        <svg class="h-4 w-4 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"></path>
                        </svg>
                        <span>${item.createdAt ? formatDateForVietnam(item.createdAt) : 'N/A'}</span>
                    </div>
                </td>
                <td class="px-6 py-4 whitespace-nowrap text-sm font-medium">
                    <div class="flex items-center space-x-2">
                        <button data-action="view" data-item-id="${item.id}" class="text-blue-600 hover:text-blue-900" title="View Details">
                            <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"></path>
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"></path>
                            </svg>
                        </button>
                        ${displayStatus === 'pending' ? `
                            <button data-action="approve" data-item-id="${item.id}" class="text-green-600 hover:text-green-900" title="Approve">
                                <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path>
                                </svg>
                            </button>
                            <button data-action="reject" data-item-id="${item.id}" class="text-red-600 hover:text-red-900" title="Reject">
                                <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                                </svg>
                            </button>
                            <button data-action="revision" data-item-id="${item.id}" class="text-orange-600 hover:text-orange-900" title="Request Revision">
                                <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"></path>
                                </svg>
                            </button>
                        ` : ''}
                        <button class="text-gray-400 hover:text-gray-600">
                            <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 5v.01M12 12v.01M12 19v.01M12 6a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2z"></path>
                            </svg>
                        </button>
                    </div>
                </td>
            `;
            itemsTableBody.appendChild(row);
        });

        addEventListenersToButtons(); // Ensure event listeners are re-attached after re-rendering
    };

    // --- API Integration ---
    // Get API base URL from environment or use default
    const apiBaseUrl = window.apiSettings?.baseUrl || 'https://localhost:7256/api';
    const API_BASE_URL = `${apiBaseUrl}/products`;

    const fetchItems = async () => {
        try {
            const response = await fetch(API_BASE_URL);
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            const data = await response.json();
            items = data; // Assign fetched data to the items array
            filteredItems = [...items]; // Populate filteredItems
            renderItems(); // Render the fetched items after fetching
        } catch (error) {
            console.error('Error fetching items:', error);
            noItemsFoundDiv.classList.remove('hidden');
            noItemsFoundDiv.textContent = `Failed to load items. Please ensure the API is running and accessible (${API_BASE_URL}) and that CORS is configured correctly.`;
        }
    };

    // Function to update product status via API (NEW)
    const updateProductStatus = async (itemId, status, reason = null) => {
        try {
            const url = `${API_BASE_URL}/update-status/${itemId}`; // Adjust this endpoint if different
            const response = await fetch(url, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    // Add any authorization headers if required, e.g., 'Authorization': 'Bearer YOUR_TOKEN'
                },
                body: JSON.stringify({
                    productId: itemId, // Ensure your API expects these field names
                    newAvailabilityStatus: status,
                    rejectionReason: reason // This will be null for 'approved' or 'available'
                })
            });

            if (!response.ok) {
                const errorData = await response.json();
                throw new Error(`Failed to update status: ${errorData.message || response.statusText}`);
            }

            console.log(`Item ${itemId} status updated to ${status}`);
            alert(`Product ${status.replace('_', ' ').toLowerCase()} successfully!`);

            // After successful update, re-fetch items to refresh the table
            await fetchItems();
            actionConfirmModal.classList.add('hidden'); // Close the action modal
        } catch (error) {
            console.error('Error updating item status:', error);
            alert(`Error updating product status: ${error.message}`);
        }
    };

    // --- Event Listeners (for filters) ---
    searchInput.addEventListener('input', applyFilters);
    categoryFilter.addEventListener('change', applyFilters);
    statusFilter.addEventListener('change', applyFilters);
    promoteFilter.addEventListener('change', applyFilters);

    function applyFilters() {
        const searchTerm = searchInput.value.toLowerCase();
        const selectedCategory = categoryFilter.value;
        const selectedStatus = statusFilter.value;
        const selectedPromote = promoteFilter.value;

        filteredItems = items.filter(item => {
            const matchesSearch = item.name.toLowerCase().includes(searchTerm) ||
                item.description.toLowerCase().includes(searchTerm) ||
                item.category.toLowerCase().includes(searchTerm);
            const matchesCategory = selectedCategory === 'all' || item.category === selectedCategory;

            // Use the actual availabilityStatus from the item for filtering
            const itemAvailabilityStatus = item.availabilityStatus.toLowerCase();
            const matchesStatus = selectedStatus === 'all' || itemAvailabilityStatus === selectedStatus;

            const matchesPromote = selectedPromote === 'all' || (selectedPromote === 'promoted' && item.isPromoted) || (selectedPromote === 'not-promoted' && !item.isPromoted);

            return matchesSearch && matchesCategory && matchesStatus && matchesPromote;
        });
        renderItems();
    }


    // --- Modals Logic ---

    const addEventListenersToButtons = () => {
        // Event listener for "View Details" button
        document.querySelectorAll('button[data-action="view"]').forEach(button => {
            button.onclick = (event) => {
                const itemId = event.currentTarget.dataset.itemId;
                const item = items.find(i => i.id === itemId);
                if (item) {
                    renderDetailsModalContent(item);
                    detailsModal.classList.remove('hidden');
                }
            };
        });

        // Event listeners for Approve/Reject/Revision buttons (MODIFIED)
        document.querySelectorAll('button[data-action="approve"], button[data-action="reject"], button[data-action="revision"]').forEach(button => {
            button.onclick = (event) => {
                currentActionItemId = event.currentTarget.dataset.itemId;
                currentActionType = event.currentTarget.dataset.action;
                const item = items.find(i => i.id === currentActionItemId);

                if (item) {
                    openActionConfirmModal(item, currentActionType);
                }
            };
        });
    };

    // Event listeners for the **Item Details Modal**
    detailsCloseButton.onclick = () => {
        detailsModal.classList.add('hidden');
    };

    // Close details modal when clicking outside
    window.onclick = (event) => {
        if (event.target == detailsModal) {
            detailsModal.classList.add('hidden');
        }
        // Also close action modal if clicking outside
        if (event.target == actionConfirmModal) {
            actionConfirmModal.classList.add('hidden');
            actionReasonTextarea.value = ''; // Clear reason when closing
        }
    };

    const renderDetailsModalContent = (item) => {
        const additionalImagesHtml = item.images && item.images.length > 0
            ? item.images.map(img => `<img src="${img.imageUrl}" alt="${item.name}" class="w-24 h-24 object-cover rounded-md">`).join('')
            : '<p class="text-gray-500">No additional images available.</p>';

        modalContentDiv.innerHTML = `
            <h2 class="text-2xl font-bold text-gray-900 mb-4">${item.name}</h2>
            <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div>
                    <img src="${item.primaryImagesUrl || (item.images && item.images.length > 0 ? item.images.find(img => img.isPrimary)?.imageUrl || item.images[0].imageUrl : 'https://via.placeholder.com/600x400?text=No+Image')}" alt="${item.name}" class="w-full h-auto object-cover rounded-lg shadow-md mb-4">
                    <h3 class="text-lg font-semibold text-gray-800 mb-2">Additional Images</h3>
                    <div class="flex flex-wrap gap-2">
                        ${additionalImagesHtml}
                    </div>
                </div>
                <div>
                    <p class="text-gray-700 mb-3"><strong class="font-semibold">Description:</strong> ${item.description || 'N/A'}</p>
                    <p class="text-gray-700 mb-3"><strong class="font-semibold">Category:</strong> ${item.category || 'N/A'}</p>
                    <p class="text-gray-700 mb-3"><strong class="font-semibold">Size:</strong> ${item.size || 'N/A'}</p>
                    <p class="text-gray-700 mb-3"><strong class="font-semibold">Color:</strong> ${item.color || 'N/A'}</p>
                    <p class="text-gray-700 mb-3"><strong class="font-semibold">Price Per Day:</strong> ₫${item.pricePerDay ? item.pricePerDay.toLocaleString('vi-VN') : 'N/A'}</p>
                    <p class="text-gray-700 mb-3"><strong class="font-semibold">Availability Status:</strong> <span class="capitalize">${item.availabilityStatus.replace('_', ' ') || 'N/A'}</span></p>
                    <p class="text-gray-700 mb-3"><strong class="font-semibold">Is Promoted:</strong> ${item.isPromoted ? 'Yes' : 'No'}</p>
                    <p class="text-gray-700 mb-3"><strong class="font-semibold">Rent Count:</strong> ${item.rentCount || 0}</p>
                    <p class="text-gray-700 mb-3"><strong class="font-semibold">Average Rating:</strong> ${item.averageRating ? item.averageRating.toFixed(1) : 'N/A'}</p>
                    <p class="text-gray-700 mb-3"><strong class="font-semibold">Provider:</strong> ${item.providerName || 'N/A'}</p>
                    <p class="text-gray-700 mb-3"><strong class="font-semibold">Created At:</strong> ${item.createdAt ? new Date(item.createdAt).toLocaleDateString('en-GB') : 'N/A'}</p>
                    <p class="text-gray-700 mb-3"><strong class="font-semibold">Last Updated:</strong> ${item.updatedAt ? new Date(item.updatedAt).toLocaleDateString('en-GB') : 'N/A'}</p>
                    <p class="text-gray-700 mb-3"><strong class="font-semibold">Condition:</strong> N/A</p> <p class="text-gray-700 mb-3"><strong class="font-semibold">Location:</strong> N/A</p>
                </div>
            </div>
        `;
    };

    // NEW: Function to open the action confirmation modal
    const openActionConfirmModal = (item, action) => {
        actionProductNameSpan.textContent = item.name;
        actionReasonTextarea.value = ''; // Clear any previous reason

        let title = '';
        let actionText = '';
        let confirmButtonClass = 'bg-purple-600 hover:bg-purple-700';

        switch (action) {
            case 'approve':
                title = 'Approve Product';
                actionText = 'approve';
                reasonGroupDiv.classList.add('hidden'); // Hide reason field
                confirmButtonClass = 'bg-green-600 hover:bg-green-700';
                break;
            case 'reject':
                title = 'Reject Product';
                actionText = 'reject';
                reasonGroupDiv.classList.remove('hidden'); // Show reason field
                confirmButtonClass = 'bg-red-600 hover:bg-red-700';
                break;
            case 'revision':
                title = 'Request Revision';
                actionText = 'request a revision for';
                reasonGroupDiv.classList.remove('hidden'); // Show reason field
                confirmButtonClass = 'bg-orange-600 hover:bg-orange-700';
                break;
            default:
                // This case should ideally not be reached
                console.error("Unknown action type:", action);
                return;
        }

        actionModalTitle.textContent = title;
        actionTextSpan.textContent = actionText;

        // Update confirm button styling
        confirmActionButton.className = `inline-flex justify-center py-2 px-4 border border-transparent shadow-sm text-sm font-medium rounded-md text-white ${confirmButtonClass} focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-purple-500`;

        actionConfirmModal.classList.remove('hidden');
    };

    // Event listeners for the NEW Action Confirmation Modal
    actionCloseButton.onclick = () => {
        actionConfirmModal.classList.add('hidden');
        actionReasonTextarea.value = ''; // Clear reason on close
    };

    cancelActionButton.onclick = () => {
        actionConfirmModal.classList.add('hidden');
        actionReasonTextarea.value = ''; // Clear reason on cancel
    };

    confirmActionButton.onclick = async () => {
        const reason = actionReasonTextarea.value.trim();

        // Validate reason for reject/revision
        if ((currentActionType === 'reject' || currentActionType === 'revision') && reason === '') {
            alert('A reason is required for rejection or revision requests.');
            return;
        }

        let newStatus;
        if (currentActionType === 'approve') {
            newStatus = 'Approved'; // API expects 'Approved' or 'Available'
        } else if (currentActionType === 'reject') {
            newStatus = 'Rejected';
        } else if (currentActionType === 'revision') {
            newStatus = 'Revision';
        } else {
            console.error("Unknown action type:", currentActionType);
            return;
        }

        // Call the API function
        await updateProductStatus(currentActionItemId, newStatus, reason);
    };

    // --- Initial Data Fetch ---
    fetchItems();
});