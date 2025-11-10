// ============================================
// CREATE VIOLATION - JAVASCRIPT
// Tính toán tiền phạt tự động và UX interactions
// ============================================

// Store files for each item (key: itemId, value: Array of File objects)
const filesByItem = {};

// Toggle checkbox khi click vào card header
function toggleCheckbox(headerElement, itemId) {
    const checkbox = document.getElementById('item_' + itemId);
    checkbox.checked = !checkbox.checked;
    toggleViolationForm(checkbox, itemId);
}

// Hiển thị/ẩn form vi phạm khi tick checkbox
function toggleViolationForm(checkbox, itemId) {
    const form = document.getElementById('form_' + itemId);
    
    if (checkbox.checked) {
        form.style.display = 'block';
        form.classList.add('active');
        
        // Scroll to form
        setTimeout(() => {
            form.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }, 100);
        
        // Tính tiền ngay
        calculatePenalty(itemId);
    } else {
        form.style.display = 'none';
        form.classList.remove('active');
    }
    
    updateSummary();
    updateSubmitButton();
}

// Hiện/ẩn field DamagePercentage khi chọn loại vi phạm
function handleViolationTypeChange(itemId, type) {
    const damageField = document.getElementById('damage_' + itemId);
    
    if (type === 'DAMAGED') {
        damageField.style.display = 'block';
    } else {
        damageField.style.display = 'none';
        // Clear value nếu không phải DAMAGED
        const input = damageField.querySelector('input');
        if (input) input.value = '';
    }

    // Auto-set penalty percentage based on type
    const penaltyInput = document.getElementById('penalty_pct_' + itemId);
    if (type === 'NOT_RETURNED') {
        setPenalty(itemId, 100);
    } else if (type === 'LATE_RETURN') {
        setPenalty(itemId, 25);
    } else if (type === 'DAMAGED') {
        setPenalty(itemId, 50);
    }
}

// Tính toán tiền phạt cho từng sản phẩm
function calculatePenalty(itemId) {
    const depositElement = document.getElementById('deposit_' + itemId);
    if (!depositElement) return;
    
    const depositAmount = parseFloat(depositElement.dataset.value) || 0;
    const penaltyPctInput = document.getElementById('penalty_pct_' + itemId);
    const penaltyPct = parseFloat(penaltyPctInput?.value) || 0;
    
    // Validate percentage
    if (penaltyPct < 0 || penaltyPct > 100) {
        penaltyPctInput.value = Math.max(0, Math.min(100, penaltyPct));
        return;
    }
    
    const penaltyAmount = depositAmount * (penaltyPct / 100);
    const refundAmount = depositAmount - penaltyAmount;
    
    // Update display
    const penaltyAmtElement = document.getElementById('penalty_amt_' + itemId);
    const refundElement = document.getElementById('refund_' + itemId);
    const penaltyAmtValueElement = document.getElementById('penalty_amt_val_' + itemId);
    
    if (penaltyAmtElement) {
        penaltyAmtElement.textContent = formatCurrency(penaltyAmount);
    }
    
    if (refundElement) {
        refundElement.textContent = formatCurrency(refundAmount);
    }
    
    if (penaltyAmtValueElement) {
        penaltyAmtValueElement.value = penaltyAmount;
    }
    
    updateSummary();
}

// Set penalty percentage nhanh
function setPenalty(itemId, percentage) {
    const input = document.getElementById('penalty_pct_' + itemId);
    if (input) {
        input.value = percentage;
        calculatePenalty(itemId);
    }
}

// Cập nhật tổng kết
function updateSummary() {
    let totalDeposit = 0;
    let totalPenalty = 0;
    
    const checkedBoxes = document.querySelectorAll('.violation-checkbox:checked');
    
    checkedBoxes.forEach(checkbox => {
        const itemId = checkbox.id.replace('item_', '');
        const depositElement = document.getElementById('deposit_' + itemId);
        const penaltyValueElement = document.getElementById('penalty_amt_val_' + itemId);
        
        if (depositElement && penaltyValueElement) {
            const deposit = parseFloat(depositElement.dataset.value) || 0;
            const penalty = parseFloat(penaltyValueElement.value) || 0;
            
            totalDeposit += deposit;
            totalPenalty += penalty;
        }
    });
    
    const totalRefund = totalDeposit - totalPenalty;
    
    const summaryBox = document.getElementById('summaryBox');
    
    if (totalDeposit > 0) {
        summaryBox.style.display = 'block';
        
        const totalDepositElement = document.getElementById('totalDeposit');
        const totalPenaltyElement = document.getElementById('totalPenalty');
        const totalRefundElement = document.getElementById('totalRefund');
        
        if (totalDepositElement) totalDepositElement.textContent = formatCurrency(totalDeposit);
        if (totalPenaltyElement) totalPenaltyElement.textContent = formatCurrency(totalPenalty);
        if (totalRefundElement) totalRefundElement.textContent = formatCurrency(totalRefund);
    } else {
        summaryBox.style.display = 'none';
    }
}

// Enable/disable submit button
function updateSubmitButton() {
    const checkedCount = document.querySelectorAll('.violation-checkbox:checked').length;
    const submitBtn = document.getElementById('submitBtn');
    
    if (submitBtn) {
        submitBtn.disabled = (checkedCount === 0);
        
        if (checkedCount > 0) {
            submitBtn.innerHTML = `<i class="fas fa-paper-plane"></i> Submit ${checkedCount} violation${checkedCount > 1 ? 's' : ''}`;
        } else {
            submitBtn.innerHTML = '<i class="fas fa-paper-plane"></i> Submit violations';
        }
    }
}

// Preview files (images/videos)
function previewFiles(input, itemId) {
    if (!input.files || input.files.length === 0) return;
    
    // Initialize file array for this item if not exists
    if (!filesByItem[itemId]) {
        filesByItem[itemId] = [];
    }
    
    // Validate total file count
    const currentCount = filesByItem[itemId].length;
    const newCount = input.files.length;
    
    if (currentCount + newCount > 10) {
        alert(`Maximum 10 files allowed. You currently have ${currentCount} file(s), can only add ${10 - currentCount} more.`);
        input.value = '';
        return;
    }
    
    // Add new files to the array
    Array.from(input.files).forEach((file) => {
        // Validate file size (10MB for images, 100MB for videos)
        const maxSize = file.type.startsWith('image/') ? 10 * 1024 * 1024 : 100 * 1024 * 1024;
        if (file.size > maxSize) {
            alert(`File "${file.name}" is too large. Maximum ${file.type.startsWith('image/') ? '10MB' : '100MB'} allowed.`);
            return;
        }
        
        filesByItem[itemId].push(file);
    });
    
    // Render preview
    renderFilePreview(itemId);
}

// Render file preview with remove buttons
function renderFilePreview(itemId) {
    const previewContainer = document.getElementById('preview_' + itemId);
    if (!previewContainer) {
        return;
    }
    
    const files = filesByItem[itemId] || [];
    
    if (files.length === 0) {
        // Show empty state
        previewContainer.classList.remove('has-files');
        previewContainer.innerHTML = `
            <div class="empty-state">
                <i class="fas fa-images" style="font-size: 48px; color: #94a3b8; margin-bottom: 12px;"></i>
                <p style="margin: 0; color: #64748b; font-size: 14px;">No files selected yet</p>
                <p class="small-text" style="margin-top: 8px;">Supported: JPG, PNG, MP4, MOV (Maximum 10 files)</p>
            </div>
        `;
        return;
    }
    
    // Has files - clear empty state and show grid
    previewContainer.classList.add('has-files');
    previewContainer.innerHTML = '';
    
    files.forEach((file, index) => {
        const div = document.createElement('div');
        div.className = 'preview-item';
        div.style.position = 'relative';
        
        // Add remove button
        const removeBtn = document.createElement('button');
        removeBtn.type = 'button';
        removeBtn.className = 'remove-file-btn';
        removeBtn.innerHTML = '×';
        removeBtn.title = 'Remove this file';
        removeBtn.setAttribute('data-item-id', itemId);
        removeBtn.setAttribute('data-file-index', index);
        
        removeBtn.addEventListener('click', function(e) {
            e.preventDefault();
            e.stopPropagation();
            removeFile(itemId, index);
        });
        
        div.appendChild(removeBtn);
        
        if (file.type.startsWith('image/')) {
            // Preview image
            const img = document.createElement('img');
            img.src = URL.createObjectURL(file);
            img.onload = function() {
                URL.revokeObjectURL(this.src); // Free memory
            };
            div.appendChild(img);
        } else if (file.type.startsWith('video/')) {
            // Preview video
            const video = document.createElement('video');
            video.src = URL.createObjectURL(file);
            video.controls = false;
            video.muted = true;
            
            const badge = document.createElement('div');
            badge.className = 'video-badge';
            badge.innerHTML = '<i class="fas fa-play"></i> VIDEO';
            div.appendChild(video);
            div.appendChild(badge);
        } else {
            // Unknown file type
            const content = document.createElement('div');
            content.style.padding = '20px';
            content.style.textAlign = 'center';
            content.style.background = '#f7fafc';
            content.innerHTML = `
                <i class="fas fa-file" style="font-size: 32px; color: #a0aec0;"></i>
                <p style="margin: 8px 0 0 0; font-size: 11px; color: #718096;">${file.name}</p>
            `;
            div.appendChild(content);
        }
        
        // Add file name at bottom
        const fileName = document.createElement('div');
        fileName.className = 'file-name';
        fileName.textContent = file.name;
        fileName.title = file.name;
        div.appendChild(fileName);
        
        previewContainer.appendChild(div);
    });
}

// Remove a specific file
function removeFile(itemId, fileIndex) {
    if (!filesByItem[itemId]) {
        return;
    }
    
    // Remove file from array
    filesByItem[itemId].splice(fileIndex, 1);
    
    // Clear the file input to reset it
    const fileInput = document.getElementById('files_' + itemId);
    if (fileInput) {
        fileInput.value = '';
    }
    
    // Re-render preview
    renderFilePreview(itemId);
}

// Format currency (VND)
function formatCurrency(amount) {
    return new Intl.NumberFormat('vi-VN', {
        style: 'decimal',
        minimumFractionDigits: 0,
        maximumFractionDigits: 0
    }).format(amount) + ' VND';
}

// Drag & Drop support
document.addEventListener('DOMContentLoaded', function() {
    const uploadAreas = document.querySelectorAll('.file-upload-area');
    
    uploadAreas.forEach(area => {
        area.addEventListener('dragover', function(e) {
            e.preventDefault();
            e.stopPropagation();
            this.classList.add('dragover');
        });
        
        area.addEventListener('dragleave', function(e) {
            e.preventDefault();
            e.stopPropagation();
            this.classList.remove('dragover');
        });
        
        area.addEventListener('drop', function(e) {
            e.preventDefault();
            e.stopPropagation();
            this.classList.remove('dragover');
            
            // Get item ID from parent structure
            const formId = this.closest('.violation-form')?.id;
            if (!formId) return;
            
            const itemId = formId.replace('form_', '');
            const fileInput = document.getElementById('files_' + itemId);
            
            if (fileInput && e.dataTransfer.files.length > 0) {
                // Create a temporary input with dropped files
                const dt = new DataTransfer();
                Array.from(e.dataTransfer.files).forEach(file => dt.items.add(file));
                fileInput.files = dt.files;
                
                // Process files
                previewFiles(fileInput, itemId);
            }
        });
    });
    
    // Initialize calculations for already checked items (if any)
    const checkedBoxes = document.querySelectorAll('.violation-checkbox:checked');
    checkedBoxes.forEach(checkbox => {
        const itemId = checkbox.id.replace('item_', '');
        calculatePenalty(itemId);
    });
    
    updateSummary();
    updateSubmitButton();
});

// Form validation before submit
document.getElementById('violationForm')?.addEventListener('submit', function(e) {
    e.preventDefault(); // Prevent default first, we'll submit manually
    
    const checkedCount = document.querySelectorAll('.violation-checkbox:checked').length;
    
    if (checkedCount === 0) {
        alert('Please select at least 1 item with violation');
        return false;
    }
    
    // Validate each checked violation
    let isValid = true;
    const checkedItems = [];
    
    document.querySelectorAll('.violation-checkbox:checked').forEach(checkbox => {
        const itemId = checkbox.id.replace('item_', '');
        const form = document.getElementById('form_' + itemId);
        
        // Check if violation type is selected
        const violationType = form.querySelector(`select[name="violations[${itemId}].ViolationType"]`);
        if (!violationType || !violationType.value) {
            alert('Please select violation type for all items');
            isValid = false;
            return;
        }
        
        // Check if description is filled
        const description = form.querySelector(`textarea[name="violations[${itemId}].Description"]`);
        if (!description || !description.value.trim() || description.value.trim().length < 10) {
            alert('Please enter detailed description (at least 10 characters) for all items');
            isValid = false;
            return;
        }
        
        // Check if files are uploaded (using filesByItem array)
        const files = filesByItem[itemId] || [];
        if (files.length === 0) {
            alert('Please upload at least 1 image/video evidence for all items');
            isValid = false;
            return;
        }
        
        checkedItems.push(itemId);
    });
    
    if (!isValid) {
        return false;
    }
    
    // Transfer files from filesByItem to input elements before submit
    checkedItems.forEach(itemId => {
        const fileInput = document.getElementById('files_' + itemId);
        const files = filesByItem[itemId] || [];
        
        if (fileInput && files.length > 0) {
            try {
                const dt = new DataTransfer();
                files.forEach(file => {
                    dt.items.add(file);
                });
                fileInput.files = dt.files;
            } catch (err) {
                console.error('Error setting files:', err);
            }
        }
    });
    
    // Show loading state
    const submitBtn = document.getElementById('submitBtn');
    if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Submitting...';
    }
    
    // Now submit the form
    this.submit();
    
    return true;
});


