$(document).ready(function () {
    const imageFileInput = document.getElementById('imageFile');
    const imageUrlInput = document.querySelector('input[name="CategoryInput.ImageUrl"]');
    const imagePreview = document.getElementById('imagePreview');
    const uploadBtn = document.getElementById('uploadBtn');
    const clearImageBtn = document.getElementById('clearImageBtn');
    const categoryForm = document.getElementById('categoryForm');
    const submitBtn = document.getElementById('submitBtn');

    // Function to show alerts
    function showAlert(message, type) {
        const alertDiv = document.createElement('div');
        alertDiv.className = `alert alert-${type} alert-dismissible fade show`;
        alertDiv.setAttribute('role', 'alert');
        alertDiv.innerHTML = `
            <i class="fas fa-${getAlertIcon(type)} me-2"></i>
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        `;
        
        // Insert at the top of card body
        const cardBody = document.querySelector('.card-body');
        if (cardBody) {
            cardBody.insertBefore(alertDiv, cardBody.firstChild);
        }
        
        // Auto dismiss after 5 seconds
        setTimeout(() => {
            if (alertDiv.parentNode) {
                alertDiv.remove();
            }
        }, 5000);
    }

    function getAlertIcon(type) {
        switch (type) {
            case 'success': return 'check-circle';
            case 'danger': return 'exclamation-circle';
            case 'warning': return 'exclamation-triangle';
            default: return 'info-circle';
        }
    }

    function isValidUrl(string) {
        try {
            const url = new URL(string);
            return url.protocol === 'http:' || url.protocol === 'https:';
        } catch (_) {
            return false;
        }
    }

    function getAuthToken() {
        // Get JWT token from cookie
        const cookies = document.cookie.split(';');
        for (let cookie of cookies) {
            const [name, value] = cookie.trim().split('=');
            if (name === 'AccessToken') {
                return value;
            }
        }
        
        // Fallback to localStorage
        return localStorage.getItem('AccessToken') || '';
    }

    function getAntiForgeryToken() {
        // Get anti-forgery token from form
        const tokenElement = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenElement ? tokenElement.value : '';
    }

    // Image URL input validation
    imageUrlInput.addEventListener('blur', function() {
        const url = this.value.trim();
        if (url && !isValidUrl(url) && !url.startsWith('data:image/')) {
            showAlert('Please enter a valid URL or select an image file.', 'warning');
        }
    });

    // Drag and drop functionality
    imagePreview.addEventListener('dragover', function(e) {
        e.preventDefault();
        this.style.borderColor = '#667eea';
        this.style.backgroundColor = '#f0f2ff';
    });

    imagePreview.addEventListener('dragleave', function(e) {
        e.preventDefault();
        this.style.borderColor = '#dee2e6';
        this.style.backgroundColor = '#f8f9fa';
    });

    imagePreview.addEventListener('drop', function(e) {
        e.preventDefault();
        this.style.borderColor = '#dee2e6';
        this.style.backgroundColor = '#f8f9fa';
        const files = e.dataTransfer.files;
        if (files.length > 0) {
            imageFileInput.files = files;
            handleFileSelect();
        }
    });

    imageFileInput.addEventListener('change', handleFileSelect);
    imageUrlInput.addEventListener('input', handleImageUrlInput);
    uploadBtn.addEventListener('click', function() {
        if (imageFileInput.files.length > 0) {
            uploadImageToCloudinary(imageFileInput.files[0]);
        }
    });
    
    if (clearImageBtn) {
        clearImageBtn.addEventListener('click', function() {
            imageFileInput.value = '';
            imageUrlInput.value = '';
            resetImagePreview();
        });
    }

    function handleFileSelect() {
        const file = imageFileInput.files[0];
        if (file) {
            const reader = new FileReader();
            reader.onload = function(e) {
                showImagePreview(e.target.result, file.name);
                imageUrlInput.value = ''; // Clear URL input if file is selected
                uploadBtn.style.display = 'block'; // Show upload button
            };
            reader.readAsDataURL(file);
        } else {
            resetImagePreview();
        }
    }

    function handleImageUrlInput() {
        const url = imageUrlInput.value.trim();
        if (url) {
            showImagePreview(url, 'Image from URL');
            imageFileInput.value = ''; // Clear file input if URL is entered
            uploadBtn.style.display = 'none'; // Hide upload button
        } else {
            resetImagePreview();
        }
    }

    function showImagePreview(src, text) {
        imagePreview.innerHTML = `
            <img src="${src}" alt="${text}" style="max-width: 100%; max-height: 180px; border-radius: 8px; object-fit: cover;">
            <p class="text-muted mt-2 mb-0">${text}</p>
        `;
        imagePreview.classList.add('has-image');
    }

    function resetImagePreview() {
        imagePreview.innerHTML = `
            <i class="fas fa-image fa-3x text-muted"></i>
            <p class="text-muted mt-2">No image selected</p>
        `;
        imagePreview.classList.remove('has-image');
        uploadBtn.style.display = 'none';
    }

    async function uploadImageToCloudinary(file, autoSubmit = false) {
        try {
            showLoading(true);
            
            // Create FormData for file upload
            const formData = new FormData();
            formData.append('image', file);
            
            // Upload directly to API
            const apiBaseUrl = window.apiSettings?.baseUrl || 'https://localhost:7256/api';
            const response = await fetch(`${apiBaseUrl}/CategoryUpload/upload-image`, {
                method: 'POST',
                body: formData,
                headers: {
                    'Authorization': `Bearer ${getAuthToken()}`
                }
            });

            console.log('Upload response status:', response.status);
            console.log('Upload response headers:', response.headers);
            
            if (response.ok) {
                const responseText = await response.text();
                console.log('Upload response text:', responseText);
                
                if (responseText.trim()) {
                    try {
                        const result = JSON.parse(responseText);
                        
                        if (result.success) {
                            // Update the ImageUrl input with uploaded URL
                            imageUrlInput.value = result.data.imageUrl;
                            
                            // Update preview
                            showImagePreview(result.data.imageUrl, 'Image uploaded successfully');
                            
                            // Hide upload button
                            uploadBtn.style.display = 'none';
                            
                            showAlert('Image uploaded successfully!', 'success');
                            
                            if (autoSubmit) {
                                setTimeout(() => submitForm(), 1000);
                            }
                        } else {
                            showAlert(`Upload failed: ${result.message}`, 'danger');
                        }
                    } catch (parseError) {
                        console.error('JSON parse error:', parseError);
                        console.error('Response text:', responseText);
                        showAlert(`Upload response invalid: ${responseText.substring(0, 100)}...`, 'danger');
                    }
                } else {
                    showAlert('Upload response is empty', 'danger');
                }
            } else {
                const errorText = await response.text();
                console.error('Upload error response:', errorText);
                showAlert(`Upload failed (${response.status}): ${errorText.substring(0, 200)}...`, 'danger');
            }
        } catch (error) {
            console.error('Upload error:', error);
            showAlert(`Upload failed: ${error.message}`, 'danger');
        } finally {
            if (!autoSubmit) {
                showLoading(false);
            }
        }
    }

    function convertFileToBase64(file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => resolve(reader.result);
            reader.onerror = error => reject(error);
            reader.readAsDataURL(file);
        });
    }

    function showLoading(isLoading) {
        if (submitBtn) {
            submitBtn.disabled = isLoading;
            if (isLoading) {
                submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Creating...';
            } else {
                submitBtn.innerHTML = '<i class="fas fa-save me-1"></i>Create Category';
            }
        }
    }

    function submitForm() {
        categoryForm.submit();
    }

    // Initialize image preview if an image URL is already present
    if (imageUrlInput.value) {
        showImagePreview(imageUrlInput.value, 'Image from URL');
    }

    // Test database functionality
    const testDatabaseBtn = document.getElementById('testDatabaseBtn');
    if (testDatabaseBtn) {
        testDatabaseBtn.addEventListener('click', async function() {
            try {
                showAlert('Testing database connection...', 'info');
                
                // Test database connection
                const apiBaseUrl = window.apiSettings?.baseUrl || 'https://localhost:7256/api';
                const response = await fetch(`${apiBaseUrl}/test/database`, {
                    method: 'GET',
                    headers: {
                        'Authorization': `Bearer ${getAuthToken()}`
                    }
                });

                if (response.ok) {
                    const result = await response.json();
                    showAlert(`Database OK! Found ${result.data.categoryCount} categories.`, 'success');
                } else {
                    const error = await response.json();
                    showAlert(`Database Error: ${error.message}`, 'danger');
                }
            } catch (error) {
                showAlert(`Test failed: ${error.message}`, 'danger');
            }
        });
    }

    // Test upload API functionality
    const testUploadBtn = document.getElementById('testUploadBtn');
    if (testUploadBtn) {
        testUploadBtn.addEventListener('click', async function() {
            try {
                showAlert('Testing upload API...', 'info');
                
                // Test upload API via PageModel
                const response = await fetch('/Staff/CategoryCreate?handler=TestUploadApi', {
                    method: 'GET',
                    headers: {
                        'RequestVerificationToken': getAntiForgeryToken()
                    }
                });

                console.log('Upload API test response status:', response.status);

                if (response.ok) {
                    const responseText = await response.text();
                    console.log('Upload API test response text:', responseText);
                    console.log('Response text length:', responseText.length);
                    console.log('Response text preview:', responseText.substring(0, 200));
                    
                    if (responseText.trim()) {
                        try {
                            const result = JSON.parse(responseText);
                            console.log('Parsed result:', result);
                            
                            if (result.success) {
                                if (result.data) {
                                    try {
                                        const apiResult = JSON.parse(result.data);
                                        showAlert(`Upload API OK! ${apiResult.message}`, 'success');
                                        console.log('API Debug Info:', apiResult.data);
                                    } catch (dataParseError) {
                                        console.error('Data parse error:', dataParseError);
                                        console.log('Raw data:', result.data);
                                        showAlert(`Upload API OK! ${result.message}`, 'success');
                                    }
                                } else {
                                    showAlert(`Upload API OK! ${result.message}`, 'success');
                                }
                            } else {
                                showAlert(`Upload API Error: ${result.error}`, 'danger');
                            }
                        } catch (parseError) {
                            console.error('JSON parse error:', parseError);
                            console.error('Response text:', responseText);
                            showAlert(`Upload API OK but invalid JSON: ${responseText.substring(0, 100)}...`, 'warning');
                        }
                    } else {
                        showAlert('Upload API OK but empty response', 'warning');
                    }
                } else {
                    const errorText = await response.text();
                    console.error('Upload API test error response:', errorText);
                    showAlert(`Upload API Error (${response.status}): ${errorText}`, 'danger');
                }
            } catch (error) {
                console.error('Upload API test error:', error);
                showAlert(`Upload API test failed: ${error.message}`, 'danger');
            }
        });
    }

    // Test upload file functionality
    const testUploadFileBtn = document.getElementById('testUploadFileBtn');
    if (testUploadFileBtn) {
        testUploadFileBtn.addEventListener('click', async function() {
            try {
                showAlert('Testing upload file...', 'info');
                
                // Create a test file
                const testFile = new File(['test content'], 'test.txt', { type: 'text/plain' });
                const formData = new FormData();
                formData.append('image', testFile);
                
                // Test upload file
                const apiBaseUrl = window.apiSettings?.baseUrl || 'https://localhost:7256/api';
                const response = await fetch(`${apiBaseUrl}/CategoryUpload/test-upload`, {
                    method: 'POST',
                    body: formData,
                    headers: {
                        'Authorization': `Bearer ${getAuthToken()}`
                    }
                });

                console.log('Upload file test response status:', response.status);

                if (response.ok) {
                    const responseText = await response.text();
                    console.log('Upload file test response text:', responseText);
                    
                    if (responseText.trim()) {
                        try {
                            const result = JSON.parse(responseText);
                            showAlert(`Upload file OK! ${result.message}`, 'success');
                        } catch (parseError) {
                            showAlert(`Upload file OK but invalid JSON: ${responseText}`, 'warning');
                        }
                    } else {
                        showAlert('Upload file OK but empty response', 'warning');
                    }
                } else {
                    const errorText = await response.text();
                    console.error('Upload file test error response:', errorText);
                    showAlert(`Upload file Error (${response.status}): ${errorText}`, 'danger');
                }
            } catch (error) {
                console.error('Upload file test error:', error);
                showAlert(`Upload file test failed: ${error.message}`, 'danger');
            }
        });
    }

    // Test simple functionality
    const testSimpleBtn = document.getElementById('testSimpleBtn');
    if (testSimpleBtn) {
        testSimpleBtn.addEventListener('click', async function() {
            try {
                showAlert('Testing simple endpoint...', 'info');
                
                // Test simple endpoint via PageModel
                const response = await fetch('/Staff/CategoryCreate?handler=TestSimple', {
                    method: 'GET',
                    headers: {
                        'RequestVerificationToken': getAntiForgeryToken()
                    }
                });

                console.log('Simple test response status:', response.status);

                if (response.ok) {
                    const responseText = await response.text();
                    console.log('Simple test response text:', responseText);
                    
                    if (responseText.trim()) {
                        try {
                            const result = JSON.parse(responseText);
                            if (result.success) {
                                showAlert(`Simple test OK! ${result.message}`, 'success');
                                console.log('Simple test result:', result);
                            } else {
                                showAlert(`Simple test Error: ${result.error}`, 'danger');
                            }
                        } catch (parseError) {
                            console.error('JSON parse error:', parseError);
                            showAlert(`Simple test OK but invalid JSON: ${responseText.substring(0, 100)}...`, 'warning');
                        }
                    } else {
                        showAlert('Simple test OK but empty response', 'warning');
                    }
                } else {
                    const errorText = await response.text();
                    console.error('Simple test error response:', errorText);
                    showAlert(`Simple test Error (${response.status}): ${errorText}`, 'danger');
                }
            } catch (error) {
                console.error('Simple test error:', error);
                showAlert(`Simple test failed: ${error.message}`, 'danger');
            }
        });
    }
});