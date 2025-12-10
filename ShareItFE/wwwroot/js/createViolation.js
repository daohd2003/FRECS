// ============================================
// CREATE VIOLATION - JAVASCRIPT
// Tính toán tiền phạt tự động và UX interactions
// ============================================


// Store files for each item (key: itemId, value: Array of File objects)
const filesByItem = {};

// Pre-populate form with existing violation data
function prePopulateViolations(existingViolations) {
    if (!existingViolations || existingViolations.length === 0) {
        return;
    }
    
    existingViolations.forEach((violation, index) => {
        const itemId = violation.orderItemId;
        
        // Check the checkbox and show the form
        const checkbox = document.getElementById('item_' + itemId);
        
        if (checkbox) {
            checkbox.checked = true;
            toggleViolationForm(checkbox, itemId);
            
            // Wait a bit for form to show, then populate
            setTimeout(() => {
                // Populate violation type and make readonly
                const violationTypeSelect = document.querySelector(`select[name="violations[${itemId}].ViolationType"]`);
                if (violationTypeSelect && violation.violationType !== null && violation.violationType !== undefined) {
                    // Convert enum number to string value
                    let violationTypeValue = '';
                    switch(violation.violationType) {
                        case 0: violationTypeValue = 'DAMAGED'; break;
                        case 1: violationTypeValue = 'LATE_RETURN'; break;
                        case 2: violationTypeValue = 'NOT_RETURNED'; break;
                        default: violationTypeValue = violation.violationType.toString(); break;
                    }
                    
                    violationTypeSelect.value = violationTypeValue;
                    violationTypeSelect.disabled = true; // Make readonly in edit mode
                    violationTypeSelect.style.backgroundColor = '#f3f4f6';
                    violationTypeSelect.style.cursor = 'not-allowed';
                    handleViolationTypeChange(itemId, violationTypeValue);
                }
                
                // Populate description and make readonly
                const descriptionTextarea = document.querySelector(`textarea[name="violations[${itemId}].Description"]`);
                if (descriptionTextarea && violation.description) {
                    descriptionTextarea.value = violation.description;
                    descriptionTextarea.readOnly = true; // Make readonly in edit mode
                    descriptionTextarea.style.backgroundColor = '#f3f4f6';
                    descriptionTextarea.style.cursor = 'not-allowed';
                }
                
                // Populate damage percentage and make readonly
                if (violation.damagePercentage !== null && violation.damagePercentage !== undefined) {
                    const damageInput = document.querySelector(`input[name="violations[${itemId}].DamagePercentage"]`);
                    if (damageInput) {
                        damageInput.value = violation.damagePercentage;
                        damageInput.readOnly = true; // Make readonly in edit mode
                        damageInput.style.backgroundColor = '#f3f4f6';
                        damageInput.style.cursor = 'not-allowed';
                    }
                }
                
                // Populate penalty percentage (KEEP EDITABLE)
                if (violation.penaltyPercentage !== null && violation.penaltyPercentage !== undefined) {
                    const penaltyPctInput = document.querySelector(`input[name="violations[${itemId}].PenaltyPercentage"]`);
                    if (penaltyPctInput) {
                        penaltyPctInput.value = violation.penaltyPercentage;
                        // Keep penalty percentage editable - add visual indicator
                        penaltyPctInput.style.backgroundColor = '#fff';
                        penaltyPctInput.style.borderColor = '#3b82f6';
                        penaltyPctInput.style.borderWidth = '2px';
                    }
                }
                
                // Disable file upload in edit mode
                const fileInput = document.querySelector(`input[name="violations[${itemId}].EvidenceFiles"]`);
                if (fileInput) {
                    fileInput.disabled = true;
                    fileInput.style.display = 'none'; // Hide file input completely
                }
                
                // Hide file upload button
                const uploadButton = document.querySelector(`.btn-select-files[onclick*="${itemId}"]`);
                if (uploadButton) {
                    uploadButton.style.display = 'none';
                }
                
                // Add edit mode indicator to penalty rate field
                const penaltyPctInput = document.querySelector(`input[name="violations[${itemId}].PenaltyPercentage"]`);
                if (penaltyPctInput) {
                    // Add edit indicator next to penalty rate field
                    const penaltyGroup = penaltyPctInput.closest('.form-group');
                    if (penaltyGroup) {
                        const label = penaltyGroup.querySelector('label');
                        if (label && !label.querySelector('.edit-indicator')) {
                            const editIndicator = document.createElement('span');
                            editIndicator.className = 'edit-indicator';
                            editIndicator.innerHTML = ' <i class="fas fa-edit" style="color: #3b82f6; margin-left: 8px;"></i> <small style="color: #3b82f6; font-weight: 600;">Editable</small>';
                            label.appendChild(editIndicator);
                        }
                    }
                }
                
                // Recalculate penalty
                calculatePenalty(itemId);
                
                // Show existing evidence files info (if any)
                if (violation.evidenceCount && violation.evidenceCount > 0) {
                    console.log(`DEBUG: Showing evidence for ${itemId} - count: ${violation.evidenceCount}, URLs:`, violation.evidenceUrls);
                    showExistingEvidenceInfo(itemId, violation.evidenceCount, violation.evidenceUrls);
                } else {
                    console.log(`DEBUG: No evidence for ${itemId} - count: ${violation.evidenceCount}`);
                }
            }, 100);
        }
    });
}

// Show existing evidence files information
function showExistingEvidenceInfo(itemId, evidenceCount, evidenceUrls = []) {
    const previewArea = document.getElementById('preview_' + itemId);
    if (!previewArea || !evidenceCount || evidenceCount === 0) return;
    
    // Clear empty state
    previewArea.innerHTML = '';
    previewArea.classList.add('has-files');
    
    // Add existing files (with actual images if URLs available)
    for (let i = 0; i < evidenceCount; i++) {
        const existingFileDiv = document.createElement('div');
        existingFileDiv.className = 'preview-item existing-evidence';
        
        if (i < evidenceUrls.length && evidenceUrls[i]) {
            // Show actual image
            const imageUrl = evidenceUrls[i];
            existingFileDiv.innerHTML = `
                <div style="position: relative; border-radius: 8px; overflow: hidden; height: 120px;">
                    <img src="${imageUrl}" 
                         alt="Evidence ${i + 1}" 
                         style="width: 100%; height: 100%; object-fit: cover; border: 2px solid #10b981;"
                         onerror="this.style.display='none'; this.nextElementSibling.style.display='flex';">
                    <div style="background: #f3f4f6; border: 2px solid #10b981; border-radius: 8px; padding: 12px; text-align: center; height: 100%; display: none; flex-direction: column; justify-content: center; align-items: center; position: absolute; top: 0; left: 0; width: 100%; box-sizing: border-box;">
                        <i class="fas fa-image" style="font-size: 24px; color: #10b981; margin-bottom: 8px;"></i>
                        <div style="font-size: 11px; color: #374151; font-weight: 600;">Evidence ${i + 1}</div>
                        <div style="font-size: 10px; color: #10b981; margin-top: 4px;">Already uploaded</div>
                    </div>
                </div>
            `;
        } else {
            // Show placeholder for missing URLs
            existingFileDiv.innerHTML = `
                <div style="background: #f3f4f6; border: 2px solid #10b981; border-radius: 8px; padding: 12px; text-align: center; height: 120px; display: flex; flex-direction: column; justify-content: center; align-items: center;">
                    <i class="fas fa-image" style="font-size: 24px; color: #10b981; margin-bottom: 8px;"></i>
                    <div style="font-size: 11px; color: #374151; font-weight: 600;">Evidence ${i + 1}</div>
                    <div style="font-size: 10px; color: #10b981; margin-top: 4px;">Already uploaded</div>
                </div>
            `;
        }
        
        previewArea.appendChild(existingFileDiv);
    }
    
    // No info message needed for edit mode
    
    // Update submit button since we have existing evidence
    updateSubmitButton();
}

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
        // Check if there are existing evidence files (edit mode)
        const hasExistingEvidence = document.querySelector('.existing-evidence') !== null;
        // Also check if any preview area has has-files class
        const hasFilesInPreview = document.querySelector('.file-upload-area .has-files') !== null;
        
        // Allow submit if either new items selected OR existing evidence (edit mode)
        const canSubmit = checkedCount > 0 || hasExistingEvidence || hasFilesInPreview;
        submitBtn.disabled = !canSubmit;
        
        if (checkedCount > 0) {
            submitBtn.innerHTML = `<i class="fas fa-paper-plane"></i> Submit ${checkedCount} violation${checkedCount > 1 ? 's' : ''}`;
        } else if (hasExistingEvidence || hasFilesInPreview) {
            submitBtn.innerHTML = `<i class="fas fa-paper-plane"></i> Update violations`;
        } else {
            submitBtn.innerHTML = `<i class="fas fa-paper-plane"></i> Submit violations`;
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
        // Validate file format (must match backend validation)
        const fileName = file.name.toLowerCase();
        const extension = fileName.substring(fileName.lastIndexOf('.'));
        
        const allowedImageExtensions = ['.jpg', '.jpeg', '.png', '.gif', '.webp', '.bmp'];
        const allowedVideoExtensions = ['.mp4', '.mov', '.avi', '.mkv', '.webm', '.flv', '.wmv'];
        
        const isImage = allowedImageExtensions.includes(extension);
        const isVideo = allowedVideoExtensions.includes(extension);
        
        if (!isImage && !isVideo) {
            alert(`File "${file.name}" has invalid format. Only images (JPG, PNG, GIF, WebP, BMP) or videos (MP4, MOV, AVI, MKV, WebM, FLV, WMV) are accepted.`);
            return;
        }
        
        // Validate file size (10MB for images, 100MB for videos)
        const maxSize = isImage ? 10 * 1024 * 1024 : 100 * 1024 * 1024;
        if (file.size > maxSize) {
            alert(`File "${file.name}" is too large. Maximum ${isImage ? '10MB' : '100MB'} allowed.`);
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
    const hasExistingEvidence = document.querySelector('.existing-evidence') !== null;
    
    // Check if we're in edit mode (has existing violations with data populated)
    const isEditMode = hasExistingEvidence || (checkedCount > 0 && document.querySelector('[name*="].ViolationType"]') && document.querySelector('[name*="].ViolationType"]').value);
    
    // If we have existing evidence (edit mode), skip validation for items with existing evidence
    if (hasExistingEvidence) {
        // Find items with existing evidence and exclude them from validation
        const itemsWithEvidence = [];
        document.querySelectorAll('.existing-evidence').forEach(evidenceEl => {
            const previewArea = evidenceEl.closest('[id^="preview_"]');
            if (previewArea) {
                const itemId = previewArea.id.replace('preview_', '');
                itemsWithEvidence.push(itemId);
            }
        });
        
        // If all checked items have existing evidence, skip all validation
        const checkedItems = Array.from(document.querySelectorAll('.violation-checkbox:checked')).map(cb => cb.id.replace('item_', ''));
        const allItemsHaveEvidence = checkedItems.length > 0 && checkedItems.every(itemId => itemsWithEvidence.includes(itemId));
        
        if (allItemsHaveEvidence) {
            
            // Show loading state
            const submitBtn = document.getElementById('submitBtn');
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Updating...';
            }
            
            // Submit directly without validation
            this.submit();
            return true;
        }
    }
    
    // Normal validation for create mode
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
        
        // Check if files are uploaded (using filesByItem array) OR if there are existing evidence files
        const files = filesByItem[itemId] || [];
        
        // Check for existing evidence in the preview area for this item
        const previewArea = document.getElementById('preview_' + itemId);
        const hasExistingEvidence = previewArea && previewArea.querySelector('.existing-evidence') !== null;
        
        // Also check if the preview area has the has-files class (indicates existing evidence)
        const hasExistingFiles = previewArea && previewArea.classList.contains('has-files');
        
        // Debug: check what we're detecting
        if (hasExistingEvidence || hasExistingFiles) {
            // Item has existing evidence, skip file validation
        } else if (files.length === 0) {
            alert(`Please upload at least 1 image/video evidence for item: ${itemId}`);
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
                // Handle error silently
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

// Evidence display is read-only in edit mode - no delete functionality needed


