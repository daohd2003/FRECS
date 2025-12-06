/**
 * Provider Application Form - localStorage Manager
 * Saves and restores form data, verification status, and uploaded images
 * Data expires after 24 hours
 */

const ProviderApplicationStorage = (function() {
    const STORAGE_KEY = 'providerApplicationDraft';
    const EXPIRY_HOURS = 2; // Reduced from 24 to 2 hours

    // ==================== UTILITY FUNCTIONS ====================
    
    function getFormData() {
        try {
            const data = localStorage.getItem(STORAGE_KEY);
            return data ? JSON.parse(data) : null;
        } catch (e) {
            console.error('[Storage] Error parsing localStorage:', e);
            return null;
        }
    }

    function isExpired(draft) {
        if (!draft || !draft.expiresAt) return false;
        return Date.now() > draft.expiresAt;
    }

    function clearFormData() {
        localStorage.removeItem(STORAGE_KEY);
        console.log('[Storage] Draft cleared');
    }

    // ==================== SAVE FUNCTIONS ====================
    
    function saveFormFields() {
        const draft = getFormData() || {};
        
        // Save basic form fields
        const businessNameEl = document.querySelector('input[name="Input.BusinessName"]');
        const taxIdEl = document.getElementById('taxIdInput');
        const contactPhoneEl = document.querySelector('input[name="Input.ContactPhone"]');
        const businessAddressEl = document.querySelector('input[name="Input.BusinessAddress"]');
        const notesEl = document.querySelector('textarea[name="Input.Notes"]');
        
        draft.businessName = businessNameEl?.value || '';
        draft.taxId = taxIdEl?.value || '';
        draft.contactPhone = contactPhoneEl?.value || '';
        draft.businessAddress = businessAddressEl?.value || '';
        draft.notes = notesEl?.value || '';
        draft.savedAt = Date.now();
        draft.expiresAt = Date.now() + (EXPIRY_HOURS * 60 * 60 * 1000);
        draft.version = '1.0';
        
        // ⭐ SECURITY: Save current user ID
        draft.userId = window.CURRENT_USER_ID || '';
        
        localStorage.setItem(STORAGE_KEY, JSON.stringify(draft));
        console.log('[Storage] Form fields saved:', {
            businessName: draft.businessName,
            taxId: draft.taxId,
            contactPhone: draft.contactPhone,
            businessAddress: draft.businessAddress,
            notes: draft.notes,
            userId: draft.userId
        });
    }

    function saveVerification(type, data) {
        const draft = getFormData() || {};
        if (!draft.verification) draft.verification = {};
        
        if (type === 'front') {
            draft.verification.frontImageUrl = data.imageUrl;
            draft.verification.frontImageBase64 = data.imageBase64; // ⭐ Save base64 for restore
            draft.verification.frontVerified = true;
            draft.verification.frontVerificationData = data.aiData;
            if (data.aiData?.idNumber) {
                draft.verification.cccdId = data.aiData.idNumber;
            }
            console.log('[Storage] Front CCCD verification saved');
        } else if (type === 'back') {
            draft.verification.backImageUrl = data.imageUrl;
            draft.verification.backImageBase64 = data.imageBase64; // ⭐ Save base64 for restore
            draft.verification.backVerified = true;
            draft.verification.backVerificationData = data.aiData;
            console.log('[Storage] Back CCCD verification saved');
        }
        
        draft.savedAt = Date.now();
        localStorage.setItem(STORAGE_KEY, JSON.stringify(draft));
    }

    function saveImage(type, url) {
        const draft = getFormData() || {};
        
        if (type === 'selfie') {
            draft.selfie = { imageUrl: url, savedAt: Date.now() };
            console.log('[Storage] Selfie image saved');
        } else if (type === 'businessLicense') {
            draft.businessLicense = { imageUrl: url, savedAt: Date.now() };
            console.log('[Storage] Business license saved');
        }
        
        draft.savedAt = Date.now();
        localStorage.setItem(STORAGE_KEY, JSON.stringify(draft));
    }

    // ==================== RESTORE FUNCTIONS ====================
    
    function restoreFormFields() {
        const draft = getFormData();
        if (!draft || isExpired(draft)) {
            if (isExpired(draft)) {
                console.log('[Storage] Draft expired, clearing...');
                clearFormData();
            }
            return false;
        }
        
        // ⭐ SECURITY: Check if draft belongs to current user
        const currentUserId = window.CURRENT_USER_ID || '';
        if (draft.userId && draft.userId !== currentUserId) {
            console.log('[Storage] Draft belongs to different user, clearing for security');
            console.log('[Storage] Draft user:', draft.userId, 'Current user:', currentUserId);
            clearFormData();
            return false;
        }
        
        // Restore text fields
        if (draft.businessName) {
            const el = document.querySelector('input[name="Input.BusinessName"]');
            if (el) el.value = draft.businessName;
        }
        if (draft.taxId) {
            const el = document.getElementById('taxIdInput');
            if (el) el.value = draft.taxId;
        }
        if (draft.contactPhone) {
            const el = document.querySelector('input[name="Input.ContactPhone"]');
            if (el) el.value = draft.contactPhone;
        }
        if (draft.businessAddress) {
            const el = document.querySelector('input[name="Input.BusinessAddress"]');
            if (el) el.value = draft.businessAddress;
        }
        if (draft.notes) {
            const el = document.querySelector('textarea[name="Input.Notes"]');
            if (el) el.value = draft.notes;
        }
        
        console.log('[Storage] Form fields restored for user:', currentUserId);
        return true;
    }

    // Helper: Convert base64 to File object
    function base64ToFile(base64, filename) {
        const arr = base64.split(',');
        const mime = arr[0].match(/:(.*?);/)[1];
        const bstr = atob(arr[1]);
        let n = bstr.length;
        const u8arr = new Uint8Array(n);
        while (n--) {
            u8arr[n] = bstr.charCodeAt(n);
        }
        return new File([u8arr], filename, { type: mime });
    }
    
    function restoreVerification(verifiedCccdIdRef) {
        const draft = getFormData();
        if (!draft?.verification) return false;
        
        const v = draft.verification;
        
        // Restore CCCD ID to global variable
        if (v.cccdId && verifiedCccdIdRef) {
            verifiedCccdIdRef.value = v.cccdId;
            
            const cccdIdValueEl = document.getElementById('cccdIdValue');
            const cccdIdHelperEl = document.getElementById('cccdIdHelper');
            if (cccdIdValueEl) cccdIdValueEl.textContent = v.cccdId;
            if (cccdIdHelperEl) {
                cccdIdHelperEl.style.display = 'block';
                cccdIdHelperEl.className = 'text-success fw-bold';
            }
            
            console.log('[Storage] CCCD ID restored:', v.cccdId);
        }
        
        // Restore front image
        if (v.frontImageUrl && v.frontVerified) {
            const previewEl = document.getElementById('idCardFrontPreview');
            const statusEl = document.getElementById('frontVerifyStatus');
            const fileNameEl = document.getElementById('frontFileName');
            const resultDiv = document.getElementById('frontVerificationResult');
            const inputEl = document.getElementById('idCardFront');
            
            if (previewEl) {
                previewEl.src = v.frontImageUrl;
                previewEl.style.display = 'block';
            }
            if (statusEl) {
                statusEl.innerHTML = '<span class="text-success fw-bold">✅ Verified (Restored)</span>';
            }
            if (fileNameEl) {
                fileNameEl.textContent = 'Previously verified';
                fileNameEl.classList.add('text-success');
            }
            if (resultDiv && v.frontVerificationData) {
                const data = v.frontVerificationData;
                let resultHtml = '<div class="alert alert-success p-2"><strong>✓ CCCD Verified (Restored)</strong><br>';
                if (data.idNumber) resultHtml += `<small>ID: ${data.idNumber}</small><br>`;
                if (data.fullName) resultHtml += `<small>Name: ${data.fullName}</small><br>`;
                if (data.dateOfBirth) resultHtml += `<small>DOB: ${data.dateOfBirth}</small><br>`;
                if (data.sex) resultHtml += `<small>Sex: ${data.sex}</small><br>`;
                if (data.confidence) resultHtml += `<small>Confidence: ${(data.confidence * 100).toFixed(1)}%</small>`;
                resultHtml += '</div>';
                resultDiv.innerHTML = resultHtml;
                resultDiv.style.display = 'block';
            }
            
            // ⭐ CRITICAL: Restore file to input element for form submission
            if (inputEl && v.frontImageBase64) {
                try {
                    const file = base64ToFile(v.frontImageBase64, 'cccd-front-restored.jpg');
                    const dataTransfer = new DataTransfer();
                    dataTransfer.items.add(file);
                    inputEl.files = dataTransfer.files;
                    console.log('[Storage] Front CCCD file restored to input');
                } catch (e) {
                    console.error('[Storage] Failed to restore front file:', e);
                }
            }
            
            console.log('[Storage] Front CCCD restored');
        }
        
        // Restore back image
        if (v.backImageUrl && v.backVerified) {
            const previewEl = document.getElementById('idCardBackPreview');
            const statusEl = document.getElementById('backVerifyStatus');
            const fileNameEl = document.getElementById('backFileName');
            const resultDiv = document.getElementById('backVerificationResult');
            const inputEl = document.getElementById('idCardBack');
            
            if (previewEl) {
                previewEl.src = v.backImageUrl;
                previewEl.style.display = 'block';
            }
            if (statusEl) {
                statusEl.innerHTML = '<span class="text-success fw-bold">✅ Verified (Restored)</span>';
            }
            if (fileNameEl) {
                fileNameEl.textContent = 'Previously verified';
                fileNameEl.classList.add('text-success');
            }
            if (resultDiv && v.backVerificationData) {
                const data = v.backVerificationData;
                let resultHtml = '<div class="alert alert-success p-2"><strong>✓ CCCD Verified (Restored)</strong><br>';
                if (data.idNumber) resultHtml += `<small>ID: ${data.idNumber}</small><br>`;
                if (data.fullName) resultHtml += `<small>Name: ${data.fullName}</small><br>`;
                if (data.dateOfBirth) resultHtml += `<small>DOB: ${data.dateOfBirth}</small><br>`;
                if (data.sex) resultHtml += `<small>Sex: ${data.sex}</small><br>`;
                if (data.confidence) resultHtml += `<small>Confidence: ${(data.confidence * 100).toFixed(1)}%</small>`;
                resultHtml += '</div>';
                resultDiv.innerHTML = resultHtml;
                resultDiv.style.display = 'block';
            }
            
            // ⭐ CRITICAL: Restore file to input element for form submission
            if (inputEl && v.backImageBase64) {
                try {
                    const file = base64ToFile(v.backImageBase64, 'cccd-back-restored.jpg');
                    const dataTransfer = new DataTransfer();
                    dataTransfer.items.add(file);
                    inputEl.files = dataTransfer.files;
                    console.log('[Storage] Back CCCD file restored to input');
                } catch (e) {
                    console.error('[Storage] Failed to restore back file:', e);
                }
            }
            
            console.log('[Storage] Back CCCD restored');
        }
        
        return true;
    }

    function restoreImages() {
        const draft = getFormData();
        if (!draft) return false;
        
        // Restore selfie
        if (draft.selfie?.imageUrl) {
            const previewEl = document.getElementById('selfiePreview');
            const previewContainer = document.getElementById('selfiePreviewContainer');
            const startContainer = document.getElementById('startCameraContainer');
            const statusEl = document.getElementById('selfieStatus');
            
            if (previewEl) {
                previewEl.src = draft.selfie.imageUrl;
            }
            if (previewContainer) {
                previewContainer.style.display = 'block';
            }
            if (startContainer) {
                startContainer.style.display = 'none';
            }
            if (statusEl) {
                statusEl.textContent = '✅ Selfie captured (Restored)';
            }
            
            console.log('[Storage] Selfie restored');
        }
        
        // Restore business license
        if (draft.businessLicense?.imageUrl) {
            const previewEl = document.getElementById('businessLicensePreview');
            const fileNameEl = document.getElementById('businessLicenseFileName');
            
            if (previewEl) {
                previewEl.src = draft.businessLicense.imageUrl;
                previewEl.style.display = 'block';
            }
            if (fileNameEl) {
                fileNameEl.textContent = 'Previously uploaded';
                fileNameEl.classList.add('text-success');
            }
            
            console.log('[Storage] Business license restored');
        }
        
        return true;
    }

    function showRestoreNotification() {
        const container = document.querySelector('.container');
        if (!container) return;
        
        const banner = document.createElement('div');
        banner.className = 'alert alert-info alert-dismissible fade show mt-3';
        banner.innerHTML = `
            <strong>📋 Draft Restored!</strong> 
            Your previous application data has been restored. You can continue where you left off.
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        `;
        
        // Insert after h1
        const h1 = container.querySelector('h1');
        if (h1) {
            h1.insertAdjacentElement('afterend', banner);
        } else {
            container.prepend(banner);
        }
        
        // Auto-dismiss after 8 seconds
        setTimeout(() => {
            banner.classList.remove('show');
            setTimeout(() => banner.remove(), 150);
        }, 8000);
    }

    // ==================== AUTO-SAVE SETUP ====================
    
    function setupAutoSave() {
        let saveTimeout;
        
        // List of field selectors (mix of ID and name attribute)
        const fieldSelectors = [
            { type: 'name', value: 'Input.BusinessName' },
            { type: 'id', value: 'taxIdInput' },
            { type: 'name', value: 'Input.ContactPhone' },
            { type: 'name', value: 'Input.BusinessAddress' },
            { type: 'name', value: 'Input.Notes' }
        ];
        
        fieldSelectors.forEach(selector => {
            let el;
            if (selector.type === 'id') {
                el = document.getElementById(selector.value);
            } else {
                // For 'name' type, determine if it's input or textarea
                el = document.querySelector(`input[name="${selector.value}"]`) || 
                     document.querySelector(`textarea[name="${selector.value}"]`);
            }
            
            if (el) {
                el.addEventListener('input', () => {
                    clearTimeout(saveTimeout);
                    saveTimeout = setTimeout(saveFormFields, 500); // Debounce 500ms
                });
            }
        });
        
        console.log('[Storage] Auto-save listeners attached');
    }

    // ==================== PUBLIC API ====================
    
    return {
        save: {
            formFields: saveFormFields,
            verification: saveVerification,
            image: saveImage
        },
        restore: {
            formFields: restoreFormFields,
            verification: restoreVerification,
            images: restoreImages,
            showNotification: showRestoreNotification
        },
        clear: clearFormData,
        setupAutoSave: setupAutoSave,
        getData: getFormData,
        isExpired: isExpired,
        
        // ⭐ SECURITY: Clear draft on logout
        clearOnLogout: function() {
            console.log('[Storage] Clearing draft on logout');
            clearFormData();
        }
    };
})();

// Export for use in Apply.cshtml
window.ProviderApplicationStorage = ProviderApplicationStorage;

// ⭐ SECURITY: Auto-clear draft when user logs out
// Listen for logout events (if your app has custom logout event)
window.addEventListener('beforeunload', function() {
    // Optional: You can add logic here if needed
    // For now, we rely on user ID check on restore
});
