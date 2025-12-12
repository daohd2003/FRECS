// API Base URL - Change this to your API endpoint
const API_BASE_URL =
  'https://8000-dep-01kbvnk3zkehawpajvz7tf97k3-d.cloudspaces.litng.ai'

// Elements
const personInput = document.getElementById('personInput')
const garmentInput = document.getElementById('garmentInput')
const personDropzone = document.getElementById('personDropzone')
const garmentDropzone = document.getElementById('garmentDropzone')
const generateButton = document.getElementById('generateButton')
const regenerateFeedbackButton = document.getElementById(
    'regenerateFeedbackButton'
)
const generatedImageContainer = document.getElementById(
  'generatedImageContainer'
)
const analysisBox = document.getElementById('analysisBox')
const garmentThumbnailsDiv = document.getElementById('garmentThumbnails');

// Hidden inputs for database URLs
const personImageUrlInput = document.getElementById('personImageUrl')
const garmentImageUrlInput = document.getElementById('garmentImageUrl')

// State
let personImage = null
let garmentImage = null
let personFile = null
let garmentFile = null
let tryOnResultId = null
let isGenerating = false
let isFetchingFeedback = false

// Initialize with database URLs if available
document.addEventListener('DOMContentLoaded', function() {
  // Check if we have URLs from database
  const dbPersonUrl = personImageUrlInput ? personImageUrlInput.value : null
  const dbGarmentUrl = garmentImageUrlInput ? garmentImageUrlInput.value : null
  
  if (dbPersonUrl && dbPersonUrl.trim() !== '') {
    personImage = dbPersonUrl
    displayUploadedImage(personDropzone, personInput, dbPersonUrl)
    console.log('Loaded person image from database:', dbPersonUrl)
  }
  
  if (dbGarmentUrl && dbGarmentUrl.trim() !== '') {
    garmentImage = dbGarmentUrl
    displayUploadedImage(garmentDropzone, garmentInput, dbGarmentUrl)
    console.log('Loaded garment image from database:', dbGarmentUrl)
  }
  
  // Check if both images are available to enable generate button
  if (personImage && garmentImage) {
    checkEnableGenerate()
  }
})

// Lấy danh sách URL ảnh từ query string (nếu có)
const params = new URLSearchParams(window.location.search);
let garmentImageUrls = [];
if (params.has('GarmentImageUrls')) {
    try {
        garmentImageUrls = JSON.parse(decodeURIComponent(params.get('GarmentImageUrls')));
        // Hiển thị select dropdown với các URL garment
        if (Array.isArray(garmentImageUrls) && garmentImageUrls.length > 0) {
            const garmentSelectContainer = document.getElementById('garmentSelectContainer');
            const garmentSelect = document.getElementById('garmentSelect');
            
            // Hiển thị container select
            garmentSelectContainer.style.display = 'block';
            
            // Thêm options cho từng URL
            garmentImageUrls.forEach((url, index) => {
                const option = document.createElement('option');
                option.value = url;
                option.textContent = `Garment Image ${index + 1}`;
                garmentSelect.appendChild(option);
            });
            
            // Xử lý sự kiện chọn ảnh
            garmentSelect.addEventListener('change', function() {
                const selectedUrl = this.value;
                if (selectedUrl) {
                    garmentImage = selectedUrl;
                    garmentFile = null; // Ưu tiên URL khi chọn từ select
                    displayUploadedImage(garmentDropzone, garmentInput, selectedUrl);
                    checkEnableGenerate();
                }
            });
            
            // Tự động chọn ảnh đầu tiên
            if (garmentImageUrls.length > 0) {
                garmentSelect.value = garmentImageUrls[0];
                garmentImage = garmentImageUrls[0];
                displayUploadedImage(garmentDropzone, garmentInput, garmentImageUrls[0]);
                checkEnableGenerate();
            }
        }
    } catch (e) {
        console.error('Invalid GarmentImageUrls:', e);
    }
}

// Event listeners
personInput.addEventListener('change', handlePersonImageUpload)

// REMOVED: garmentInput upload - only allow selection from products
// garmentInput.addEventListener('change', handleGarmentImageUpload)

generateButton.addEventListener('click', (event) => {
  event.preventDefault()
  handleGenerate()
})

regenerateFeedbackButton.addEventListener('click', async () => {
    if (isFetchingFeedback) return

    if (!tryOnResultId) {
        showToast('No image ID available to regenerate analysis.', 'error')
        return
    }

    showToast('Forcing regeneration of new analysis...', 'info')
    await regenerateFashionFeedback(tryOnResultId)
})

// Clear person image button
const clearPersonBtn = document.getElementById('clearPersonBtn')
if (clearPersonBtn) {
    clearPersonBtn.addEventListener('click', () => {
        // Clear person image
        personImage = null
        personFile = null
        localStorage.removeItem('personImageUrl')
        
        // Reset dropzone
        personDropzone.classList.remove('has-image')
        personDropzone.querySelectorAll('.uploaded-image, .badge').forEach((el) => el.remove())
        
        // Remove selected state from example images
        document.querySelectorAll('.example-image').forEach(img => img.classList.remove('selected'))
        
        // Hide clear button
        clearPersonBtn.style.display = 'none'
        
        // Update generate button
        checkEnableGenerate()
        
        showToast('Person image cleared', 'info')
    })
}

// Add drag and drop functionality ONLY for person image
setupDragAndDrop(personDropzone, personInput)
// REMOVED: Garment dropzone drag & drop - only allow selection from products
// setupDragAndDrop(garmentDropzone, garmentInput)

function fileToDataUrl(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(reader.result)
    reader.onerror = reject
    reader.readAsDataURL(file)
  })
}

function checkFiles() {
  const generateButton = document.getElementById('generateButton')
  generateButton.disabled = !(personFile && garmentFile)
}

function displayUploadedImage(dropzone, inputElement, imageUrl) {
  dropzone.classList.add('has-image')
  
  // If this is garment dropzone, enable pointer events to show the image
  if (dropzone.id === 'garmentDropzone') {
    dropzone.style.pointerEvents = 'auto'
    dropzone.style.cursor = 'default'
  }
  
  // Clear only the previous image and badge
  dropzone
    .querySelectorAll('.uploaded-image, .badge')
    .forEach((el) => el.remove())

  // Create image element
  const img = document.createElement('img')
  img.src = imageUrl
  img.className = 'uploaded-image'
  
  // Only allow re-upload for person image (not garment)
  if (dropzone.id === 'personDropzone' && inputElement) {
    img.style.cursor = 'pointer'
    img.addEventListener('click', () => inputElement.click())
  } else {
    img.style.cursor = 'default'
  }
  
  dropzone.appendChild(img)

  // Add checkmark badge
  const badge = document.createElement('div')
  badge.className = 'badge'
  badge.style.position = 'absolute'
  badge.style.top = '10px'
  badge.style.right = '10px'
  badge.style.background = '#FF6B98'
  badge.style.color = 'white'
  badge.style.borderRadius = '50%'
  badge.style.width = '30px'
  badge.style.height = '30px'
  badge.style.display = 'flex'
  badge.style.alignItems = 'center'
  badge.style.justifyContent = 'center'
  badge.innerHTML = '<i class="fas fa-check"></i>'
  dropzone.appendChild(badge)
}

// Toast notification function
function showToast(message, type = 'info') {
  const toast = document.createElement('div')
  toast.className = `toast toast-${type}`
  toast.textContent = message
  document.body.appendChild(toast)

  // Animate in
  setTimeout(() => {
    toast.style.transform = 'translateY(0)'
    toast.style.opacity = '1'
  }, 50)

  // Remove after 3 seconds
  setTimeout(() => {
    toast.style.opacity = '0'
    toast.style.transform = 'translateY(-20px)'
    setTimeout(() => {
      document.body.removeChild(toast)
    }, 300)
  }, 3000)
}

// Functions
function setupDragAndDrop(dropzone, input) {
  ;['dragenter', 'dragover', 'dragleave', 'drop'].forEach((eventName) => {
    dropzone.addEventListener(
      eventName,
      (e) => {
        e.preventDefault()
        e.stopPropagation()
      },
      false
    )
  })
  ;['dragenter', 'dragover'].forEach((eventName) => {
    dropzone.addEventListener(
      eventName,
      () => dropzone.classList.add('highlight'),
      false
    )
  })
  ;['dragleave', 'drop'].forEach((eventName) => {
    dropzone.addEventListener(
      eventName,
      () => dropzone.classList.remove('highlight'),
      false
    )
  })
  dropzone.addEventListener(
    'drop',
    (e) => {
      const dt = e.dataTransfer
      const files = dt.files
      if (files && files.length) {
        input.files = files
        input.dispatchEvent(new Event('change'))
      }
    },
    false
  )
}

async function handlePersonImageUpload(e) {
  if (e.target.files && e.target.files[0]) {
    const file = e.target.files[0]
    personFile = file

    // Convert to Data URL for storage
    const imageUrl = await fileToDataUrl(file)
    localStorage.setItem('personImageUrl', imageUrl)
    personImage = imageUrl

    displayUploadedImage(personDropzone, personInput, imageUrl)
    
    // Remove selected state from example images when uploading from computer
    document.querySelectorAll('.example-image').forEach(img => img.classList.remove('selected'))
    
    // Show clear button for person image
    const clearPersonBtn = document.getElementById('clearPersonBtn')
    if (clearPersonBtn) {
        clearPersonBtn.style.display = 'block'
    }
    
    checkEnableGenerate()
  }
}

async function handleGarmentImageUpload(e) {
  if (e.target.files && e.target.files[0]) {
    const file = e.target.files[0]
    garmentFile = file

    // Convert to Data URL for storage
    const imageUrl = await fileToDataUrl(file)
    localStorage.setItem('garmentImageUrl', imageUrl)
    garmentImage = imageUrl

    displayUploadedImage(garmentDropzone, garmentInput, imageUrl)
    checkEnableGenerate()
  }
}

function checkEnableGenerate() {
  // Check if we have both person and garment images (either as files or URLs)
  const hasPersonImage = personFile || personImage
  const hasGarmentImage = garmentFile || garmentImage
  
  if (hasPersonImage && hasGarmentImage) {
    generateButton.disabled = false
    generateButton.innerHTML = '<i class="fas fa-wand-magic-sparkles"></i> Create Magic!'
    generateButton.classList.add('ready')
    generateButton.style.animation = 'pulse 1.5s infinite'

    // Define pulse animation
    if (!document.getElementById('pulse-animation')) {
      const style = document.createElement('style')
      style.id = 'pulse-animation'
      style.textContent = `
                @keyframes pulse {
                    0% { transform: scale(1); }
                    50% { transform: scale(1.05); }
                    100% { transform: scale(1); }
                }
            `
      document.head.appendChild(style)
    }
  } else {
    generateButton.disabled = true
    generateButton.innerHTML = '<i class="fas fa-wand-magic-sparkles"></i> Create Magic!'
    generateButton.classList.remove('ready')
    generateButton.style.animation = 'none'
  }
}

async function handleGenerate() {
  if (isGenerating) return

  if (!personFile && !personImage) {
    console.log('Missing person image!')
  }
  if (!garmentFile && !garmentImage) {
    console.log('Missing garment image!')
  }

  if (!personFile && !personImage || !garmentFile && !garmentImage) {
    showToast('Please upload both person and garment images', 'error')
    return
  }

  isGenerating = true
  generateButton.disabled = true
  generateButton.innerHTML =
    '<i class="fas fa-spinner fa-spin"></i> Processing...'

    regenerateFeedbackButton.style.display = 'none'
  // Show loading state with animation
  generatedImageContainer.innerHTML = `
        <div class="image-placeholder">
            <div style="position: relative; width: 60px; height: 60px;">
                <div style="position: absolute; border: 4px solid #FFE6F2; border-top: 4px solid #FF6B98; border-radius: 50%; width: 60px; height: 60px; animation: spin 1s linear infinite;"></div>
                <div style="position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); font-size: 1.5rem;"><i class="fas fa-magic"></i></div>
            </div>
            <p style="margin-top: 20px; font-weight: 500; color: #FF6B98;">Creating your fashion magic...</p>
        </div>
    `
    analysisBox.querySelector('.analysis-content').innerHTML = `
        <div style="display: flex; justify-content: center; align-items: center; flex-direction: column; padding: 20px;">
            <div style="position: relative; width: 40px; height: 40px; margin-bottom: 15px;">
                <div style="position: absolute; border: 3px solid #FFE6F2; border-top: 3px solid #FF6B98; border-radius: 50%; width: 40px; height: 40px; animation: spin 1s linear infinite;"></div>
            </div>
            <p>Waiting for image to analyze...</p>
        </div>
    `
  // Define animation
  if (!document.getElementById('spin-animation')) {
    const style = document.createElement('style')
    style.id = 'spin-animation'
    style.textContent = `
            @keyframes spin {
                0% { transform: rotate(0deg); }
                100% { transform: rotate(360deg); }
            }
        `
    document.head.appendChild(style)
  }

  try {
    const formData = new FormData()
    
    // Handle person image
    if (personFile) {
      formData.append('person_image', personFile)
    } else if (personImage) {
      // If only URL is available, fetch and convert to file
      console.log('[DEBUG] Fetching person image from URL:', personImage)
      const personResponse = await fetch(personImage)
      const personBlob = await personResponse.blob()
      const personFileName = personImage.split('/').pop().split('?')[0] || 'person.jpg'
      const personFileFromUrl = new File([personBlob], personFileName, { type: personBlob.type })
      formData.append('person_image', personFileFromUrl)
    }
    
    // Handle garment image
    if (garmentFile) {
      formData.append('clothing_image', garmentFile)
    } else if (garmentImage) {
      // If only URL is available, fetch and convert to file
      console.log('[DEBUG] Fetching garment image from URL:', garmentImage)
      const garmentResponse = await fetch(garmentImage)
      const garmentBlob = await garmentResponse.blob()
      const garmentFileName = garmentImage.split('/').pop().split('?')[0] || 'garment.jpg'
      const garmentFileFromUrl = new File([garmentBlob], garmentFileName, { type: garmentBlob.type })
      formData.append('clothing_image', garmentFileFromUrl)
    }
    
    formData.append('user_id', 0)
    clothType = document.getElementById('clothTypeSelector').value
    formData.append('cloth_type', clothType)

    console.log('[DEBUG] Selected clothType:', clothType)
    console.log('[DEBUG] FormData contents:')
    for (let [key, value] of formData.entries()) {
      console.log(`[DEBUG] ${key}:`, value instanceof File ? `File: ${value.name} (${value.size} bytes)` : value)
    }

    const response = await fetch(`${API_BASE_URL}/api/try-on/full-process`, {
      method: 'POST',
      body: formData,
    })

    if (!response.ok) {
      throw new Error(
        `Process failed: ${response.status} ${response.statusText}`
      )
    }

    const result = await response.json()
    localStorage.setItem('generatedImageUrl', result.result_url)

    tryOnResultId = result.result_id
    localStorage.setItem('lastTryOnResultId', tryOnResultId)

    // Lưu ảnh Try-On vào database (tự động xóa sau 7 ngày)
    // Sử dụng URL và ID từ AI response
    const selectedClothType = document.getElementById('clothTypeSelector')?.value || 'upper';
    
    await saveTryOnImageToGalleryV2({
        resultUrl: result.result_url,
        resultPublicId: result.result_id,
        personUrl: result.person_url,
        personPublicId: result.person_id,
        garmentUrl: result.clothing_url,
        garmentPublicId: result.clothing_id,
        clothingType: selectedClothType
    });

      if (!tryOnResultId) {
          console.warn('Backend did not return a result_id. Cannot fetch feedback.')
          showToast(
              'Try-on successful, but could not get feedback ID for analysis.',
              'warning'
          )
          analysisBox.querySelector(
              '.analysis-content'
          ).innerHTML = `Generate a try-on image to see AI style and fit analysis`
      } else {
          console.log(
              `Generated try-on with ID: ${tryOnResultId}. Fetching feedback...`
          )
          await getFashionFeedback(tryOnResultId)
      }

    const generatedImg = document.createElement('img')
    generatedImg.src = result.result_url
    generatedImg.className = 'generated-image'

    generatedImageContainer.innerHTML = ''
    generatedImageContainer.appendChild(generatedImg)
  } catch (error) {
    console.error('Error:', error)
    showToast(error.message, 'error')
    generatedImageContainer.innerHTML = `<div class="image-placeholder"><p>An error occurred. Please try again.</p></div>`
  } finally {
    isGenerating = false
    checkEnableGenerate()
    generateButton.innerHTML =
      '<i class="fas fa-wand-magic-sparkles"></i> Create Magic!'
  }
}

async function regenerateFashionFeedback(resultId) {
    if (isFetchingFeedback) return
    isFetchingFeedback = true // Set trạng thái đang fetch feedback

    // Hiển thị loading trong analysis box
    analysisBox.querySelector('.analysis-content').innerHTML = `
        <div style="display: flex; justify-content: center; align-items: center; flex-direction: column; padding: 20px;">
            <div style="position: relative; width: 40px; height: 40px; margin-bottom: 15px;">
                <div style="position: absolute; border: 3px solid #FFE6F2; border-top: 3px solid #FF6B98; border-radius: 50%; width: 40px; height: 40px; animation: spin 1s linear infinite;"></div>
            </div>
            <p>Analyzing your fashion fit again...</p>
        </div>
    `
    regenerateFeedbackButton.disabled = true // Vô hiệu hóa nút khi đang xử lý

    try {
        // Gửi yêu cầu POST trực tiếp đến API feedback
        const response = await fetch(`${API_BASE_URL}/api/feedback/${resultId}`, {
            method: 'POST', // Đây là cuộc gọi POST
        })

        if (!response.ok) {
            const errorData = await response.json()
            throw new Error(
                `API Error: ${response.status}. Details: ${errorData.detail || 'No additional details.'
                }`
            )
        }

        const data = await response.json()
        const feedback = data.data.feedback
        const formattedText = data.data.formatted_text

        if (formattedText) {
            const sections = formattedText.split('\n\n')
            let analysisContent = ''
            sections.forEach((section) => {
                const lines = section.split('\n')
                const title = lines[0]
                const details = lines.slice(1)
                analysisContent += `<div class="feedback-section"><h3 style="color: #FF6B98; margin-bottom: 10px;">${title}</h3>`
                details.forEach(
                    (detail) =>
                        (analysisContent += `<p style="margin-bottom: 5px;">${detail}</p>`)
                )
                analysisContent += `</div>`
            })
            analysisBox.querySelector('.analysis-content').innerHTML = analysisContent
        } else {
            // Fallback nếu không có formattedText
            analysisBox.querySelector(
                '.analysis-content'
            ).innerHTML = `<pre>${JSON.stringify(feedback, null, 2)}</pre>`
        }
        showToast('Analysis regenerated successfully!', 'success')
    } catch (error) {
        console.error('Error regenerating feedback:', error)
        showToast(`Error regenerating analysis: ${error.message}`, 'error')
        analysisBox.querySelector('.analysis-content').innerHTML = `
            <div style="padding: 15px; color: #666;">
                <p><i class="fas fa-exclamation-circle"></i> Could not regenerate fashion analysis: ${error.message}</p>
            </div>
        `
    } finally {
        isFetchingFeedback = false
        regenerateFeedbackButton.disabled = false
    }
}
async function getFashionFeedback(resultId) {
  try {
    // Show loading in the analysis box
    document.querySelector('.analysis-content').innerHTML = `
            <div style="display: flex; justify-content: center; align-items: center; flex-direction: column; padding: 20px;">
                <div style="position: relative; width: 40px; height: 40px; margin-bottom: 15px;">
                    <div style="position: absolute; border: 3px solid #FFE6F2; border-top: 3px solid #FF6B98; border-radius: 50%; width: 40px; height: 40px; animation: spin 1s linear infinite;"></div>
                </div>
                <p>Analyzing your fashion fit...</p>
            </div>
        `

      regenerateFeedbackButton.disabled = true
    // Get feedback
    const feedbackResponse = await fetch(
      `${API_BASE_URL}/api/feedback/${resultId}`,
      {
        method: 'GET',
      }
    )

    if (!feedbackResponse.ok) {
      throw new Error(`API Error: ${feedbackResponse.status}`)
    }

    const data = await feedbackResponse.json()
    const feedback = data.data.feedback
    const formattedText = data.data.formatted_text

    // If we have formatted text from the server, use it
    if (formattedText) {
      // Split by double newlines to get sections
      const sections = formattedText.split('\n\n')
      let analysisContent = ''

      sections.forEach((section) => {
        const lines = section.split('\n')
        const title = lines[0]
        const details = lines.slice(1)

        analysisContent += `<div class="feedback-section">
                    <h3 style="color: #FF6B98; margin-bottom: 10px;">${title}</h3>`

        if (details.length > 0) {
          details.forEach((detail) => {
            analysisContent += `<p style="margin-bottom: 5px;">${detail}</p>`
          })
        }

        analysisContent += `</div>`
      })

      document.querySelector('.analysis-content').innerHTML = analysisContent
    } else {
      // Fallback to manual formatting if formatted text is not available
      let analysisContent = ''

      if (feedback.feedback) {
        analysisContent += `<div class="feedback-section">
                    <h3><span style="color: #FF6B98; font-size: 16px;">💬</span> Nhận xét chi tiết</h3>
                    <p>${feedback.feedback}</p>
                </div>`
      }

      if (
        feedback.recommendations &&
        Array.isArray(feedback.recommendations) &&
        feedback.recommendations.length > 0
      ) {
        analysisContent += `<div class="feedback-section">
                    <h3><span style="color: #FF6B98; font-size: 16px;">✨</span> Đề xuất</h3>
                    <ul>`

        feedback.recommendations.forEach((recommendation) => {
          analysisContent += `<li>${recommendation}</li>`
        })

        analysisContent += `</ul></div>`
      }

      if (feedback.overall_score !== undefined) {
        const score = feedback.overall_score
        const stars = '★'.repeat(Math.min(score, 10))
        const emptyStars = '☆'.repeat(10 - Math.min(score, 10))

        analysisContent += `<div class="feedback-section">
                    <h3><span style="color: #FF6B98; font-size: 16px;">💯</span> Điểm đánh giá</h3>
                    <p style="font-size: 18px;">${stars}${emptyStars} (${score}/10)</p>
                </div>`
      }

      // If we have no structured content but have error information
      if (analysisContent === '' && feedback.error) {
        analysisContent = `<div class="feedback-section">
                    <h3><span style="color: #FF6B98; font-size: 16px;">⚠️</span> Lỗi</h3>
                    <p>${feedback.error}</p>
                    ${
                      feedback.details
                        ? `<p>Chi tiết: ${feedback.details}</p>`
                        : ''
                    }
                </div>`
      }

      // If we still have no content, show the raw JSON
      if (analysisContent === '') {
        analysisContent = `<div class="feedback-section">
                    <pre>${JSON.stringify(feedback, null, 2)}</pre>
                </div>`
      }

      document.querySelector('.analysis-content').innerHTML = analysisContent
    }
  } catch (error) {
    console.error('Error getting fashion feedback:', error)
    document.querySelector('.analysis-content').innerHTML = `
            <div style="padding: 15px; color: #666;">
                <p><i class="fas fa-exclamation-circle"></i> Could not retrieve fashion analysis: ${error.message}</p>
                <p>Enjoy your virtual try-on image!</p>
            </div>
        `
  } finally {
    isGenerating = false
    generateButton.disabled = false
    generateButton.innerHTML =
      '<i class="fas fa-wand-magic-sparkles"></i> Create Magic!'
  }
}

// Add toast styles if not already present
if (!document.getElementById('toast-styles')) {
  const toastStyles = document.createElement('style')
  toastStyles.id = 'toast-styles'
  toastStyles.textContent = `
        .toast {
            position: fixed;
            top: 20px;
            right: 20px;
            padding: 12px 20px;
            background-color: rgba(255, 107, 152, 0.9);
            color: white;
            border-radius: 8px;
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
            z-index: 1000;
            transform: translateY(-20px);
            opacity: 0;
            transition: transform 0.3s ease, opacity 0.3s ease;
        }
        
        .toast-error {
            background-color: rgba(255, 70, 70, 0.9);
        }
        
        .toast-success {
            background-color: rgba(70, 200, 120, 0.9);
        }
        
        .feedback-section {
            margin-bottom: 15px;
            padding-bottom: 15px;
            border-bottom: 1px solid #FFD6E6;
        }
        
        .feedback-section:last-child {
            border-bottom: none;
            margin-bottom: 0;
            padding-bottom: 0;
        }
    `
  document.head.appendChild(toastStyles)
}

window.addEventListener('load', () => {
    // CLEAR old person image when entering the page (as requested)
    localStorage.removeItem('personImageUrl')
    personImage = null
    personFile = null
    
    const savedGarment = localStorage.getItem('garmentImageUrl')
    const savedGenerated = localStorage.getItem('generatedImageUrl')
    const savedResultId = localStorage.getItem('lastTryOnResultId')

    // Person image is NOT restored from localStorage anymore (cleared on page load)
    // User must upload new person image each time they visit the page
    
    // Keep garment image if it exists (from product page)
    if (savedGarment) {
        garmentImage = savedGarment
        garmentFile = null
        displayUploadedImage(garmentDropzone, garmentInput, savedGarment)
    }

    // Keep generated image if exists
    if (savedGenerated) {
        const img = document.createElement('img')
        img.src = savedGenerated
        img.className = 'generated-image'
        generatedImageContainer.innerHTML = ''
        generatedImageContainer.appendChild(img)

        if (savedResultId) {
            tryOnResultId = parseInt(savedResultId)
            getFashionFeedback(tryOnResultId)
            regenerateFeedbackButton.style.display = 'block'
        } else {
            document.querySelector(
                '.analysis-content'
            ).innerHTML = `Generate a try-on image to see AI style and fit analysis`
            regenerateFeedbackButton.style.display = 'none'
        }
    } else {
        regenerateFeedbackButton.style.display = 'none'
    }

    // Update button state on load
    checkEnableGenerate()
    
    // Handle example image selection (setup after DOM is loaded)
    const exampleImages = document.querySelectorAll('.example-image')
    exampleImages.forEach(img => {
        img.addEventListener('click', async function() {
            const imageUrl = this.src
            
            // Remove selected class from all examples
            exampleImages.forEach(ex => ex.classList.remove('selected'))
            // Add selected class to clicked image
            this.classList.add('selected')
            
            // Fetch the image and convert to File object
            try {
                const response = await fetch(imageUrl, { mode: 'cors' })
                if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`)
                
                const blob = await response.blob()
                const fileName = `example-${this.dataset.example}.jpg`
                const file = new File([blob], fileName, { type: blob.type })
                
                // Set as person file and image
                personFile = file
                personImage = imageUrl
                
                // Save to localStorage
                localStorage.setItem('personImageUrl', imageUrl)
                
                // Display the image
                displayUploadedImage(personDropzone, personInput, imageUrl)
                
                // Show clear button
                const clearPersonBtn = document.getElementById('clearPersonBtn')
                if (clearPersonBtn) {
                    clearPersonBtn.style.display = 'block'
                }
                
                // Update generate button
                checkEnableGenerate()
                
                showToast('Example image selected', 'success')
            } catch (error) {
                console.error('Error loading example image:', error)
                showToast('Failed to load example image: ' + error.message, 'error')
            }
        })
    })
})

// Extract Cloudinary public_id from URL
function extractCloudinaryPublicId(url) {
    if (!url) return null;
    try {
        // URL format: https://res.cloudinary.com/xxx/image/upload/v123/folder/filename.ext
        const match = url.match(/\/upload\/(?:v\d+\/)?(.+)\.[^.]+$/);
        if (match && match[1]) {
            return match[1]; // Returns folder/filename without extension
        }
        // Fallback: just get filename without extension
        return url.split('/').pop()?.split('.')[0] || null;
    } catch {
        return null;
    }
}

// Function để lưu ảnh Try-On vào database (Gallery) - V2 với đúng public_id từ AI
async function saveTryOnImageToGalleryV2(data) {
    try {
        const backendApiUrl = window.apiSettings?.baseUrl || 'https://localhost:7256/api';
        const token = window.apiSettings?.accessToken;
        const productId = window.apiSettings?.productId;
        
        if (!token) return;

        // Extract Cloudinary public_id từ URL
        const resultPublicId = extractCloudinaryPublicId(data.resultUrl);
        const personPublicId = extractCloudinaryPublicId(data.personUrl);
        const garmentPublicId = extractCloudinaryPublicId(data.garmentUrl);

        const requestBody = {
            productId: productId && productId !== '' ? productId : null,
            imageUrl: data.resultUrl,
            cloudinaryPublicId: resultPublicId,
            personImageUrl: data.personUrl,
            personPublicId: personPublicId,
            garmentImageUrl: data.garmentUrl,
            garmentPublicId: garmentPublicId,
            clothingType: data.clothingType || null
        };

        const response = await fetch(`${backendApiUrl}/try-on-images`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify(requestBody)
        });

        if (response.ok) {
            showToast('Image saved to your gallery!', 'success');
        }
    } catch (error) {
        // Silent fail
    }
}
