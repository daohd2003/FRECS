document.addEventListener('DOMContentLoaded', () => {
    const openModalBtn = document.getElementById('openModalBtn'); // Keep this for the global button
    const modalOverlay = document.getElementById('productFeedbackModal');
    const closeModalBtn = document.getElementById('closeModalBtn');
    const feedbackForm = document.getElementById('feedbackForm');
    const overallRatingContainer = document.querySelector('.star-rating-overall');
    const overallRatingText = document.getElementById('overallRatingText');
    const commentTextArea = document.getElementById('comment');
    const commentCharCount = document.getElementById('commentCharCount');
    const photoPreviewContainer = document.getElementById('photoPreviewContainer');
    const recommendToggle = document.getElementById('recommendToggle');
    const submitBtn = document.getElementById('submitBtn');
    const cancelBtn = document.getElementById('cancelBtn');
    const successState = document.getElementById('successState');

    // Get the hidden input for Rating that ASP.NET Core MVC creates
    // IMPORTANT: This ID needs to be consistent. asp-for="FeedbackInput.Rating" typically creates "FeedbackInput_Rating"
    const hiddenRatingInput = document.getElementById('FeedbackInput_Rating');

    let clientFeedbackState = {
        // Initialize rating from the hidden input if it exists, otherwise 0
        rating: parseInt(hiddenRatingInput ? hiddenRatingInput.value : 0),
        comment: commentTextArea.value || '',
        photos: [], // Still manage client-side for display
        wouldRecommend: false,
    };

    const getRatingText = (rating) => {
        const texts = ['', 'Poor', 'Fair', 'Good', 'Very Good', 'Excellent'];
        return texts[rating] || '';
    };

    const updateStarDisplay = (container, currentRating, hoveredRating = 0) => {
        const stars = container.querySelectorAll('.star-button svg');
        stars.forEach((svg, index) => {
            const starValue = index + 1;
            const parentButton = svg.closest('.star-button');

            if (starValue <= hoveredRating || (hoveredRating === 0 && starValue <= currentRating)) {
                parentButton.classList.add('filled');
            } else {
                parentButton.classList.remove('filled');
            }
        });
    };

    const updateOverallRatingDisplay = (currentRating, hoveredRating = 0) => {
        updateStarDisplay(overallRatingContainer, currentRating, hoveredRating);
        overallRatingText.textContent = hoveredRating > 0 ? getRatingText(hoveredRating) : getRatingText(currentRating);
        overallRatingText.style.display = (currentRating > 0 || hoveredRating > 0) ? 'block' : 'none';

        submitBtn.disabled = currentRating === 0;
    };

    const renderStars = (container, isOverall = false) => {
        container.innerHTML = '';
        for (let i = 1; i <= 5; i++) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'star-button';
            button.dataset.value = i;
            button.innerHTML = `<svg data-lucide="star"></svg>`;
            container.appendChild(button);
        }

        if (isOverall) {
            container.querySelectorAll('.star-button svg').forEach(svg => {
                svg.classList.add('h-10', 'w-10');
            });
        }
        lucide.createIcons();
    };

    // Initial rendering of overall stars and update display based on current data
    renderStars(overallRatingContainer, true);
    updateOverallRatingDisplay(clientFeedbackState.rating); // Use initial state

    // --- Modal Control Functions (now called from inline script) ---
    // These functions should be accessible globally if called from inline script
    // or you can move the openModalBtn listener back here if you prefer.
    // For now, let's keep the listener in the inline script as it sets up hiddenRatingInput.

    const resetModalState = () => {
        clientFeedbackState = {
            rating: 0,
            comment: '',
            photos: [],
            wouldRecommend: false,
        };
        feedbackForm.reset();
        if (hiddenRatingInput) {
            hiddenRatingInput.value = 0; // Reset hidden rating input
        }
        renderStars(overallRatingContainer, true);
        updateOverallRatingDisplay(0);
        commentCharCount.textContent = '0';
        renderPhotoPreviews();
        recommendToggle.classList.remove('active');
        recommendToggle.setAttribute('aria-pressed', 'false');
        submitBtn.disabled = true;
        submitBtn.innerHTML = `<svg class="lucide-send button-icon" xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m22 2-7 20-4-9-9-4 20-7Z"/><path d="M22 2 11 13"/></svg><span>Submit Review</span>`;
        successState.classList.remove('show');
        lucide.createIcons();
    };

    // Make resetModalState globally accessible if needed by inline script
    window.resetFeedbackModal = resetModalState;

    // The openModalBtn, closeModalBtn, cancelBtn event listeners are now in the .cshtml file.
    // We only keep the core logic here.

    // Overall Rating Logic
    overallRatingContainer.addEventListener('click', (e) => {
        const starBtn = e.target.closest('.star-button');
        if (starBtn) {
            const rating = parseInt(starBtn.dataset.value);
            clientFeedbackState.rating = rating; // Update client-side state
            // The hidden input update is handled in the inline script now.
            updateOverallRatingDisplay(clientFeedbackState.rating);
        }
    });

    overallRatingContainer.addEventListener('mouseover', (e) => {
        const starBtn = e.target.closest('.star-button');
        if (starBtn) {
            const hoveredRating = parseInt(starBtn.dataset.value);
            updateOverallRatingDisplay(clientFeedbackState.rating, hoveredRating);
        }
    });

    overallRatingContainer.addEventListener('mouseout', () => {
        updateOverallRatingDisplay(clientFeedbackState.rating);
    });

    // Comment Textarea Logic
    commentTextArea.addEventListener('input', (e) => {
        clientFeedbackState.comment = e.target.value;
        commentCharCount.textContent = clientFeedbackState.comment.length;
    });

    // Photo Upload Logic (Client-side display only)
    const handlePhotoUpload = (e) => {
        const files = Array.from(e.target.files || []);
        const newPhotos = files.map((file, index) => {
            return URL.createObjectURL(file);
        });
        clientFeedbackState.photos = [...clientFeedbackState.photos, ...newPhotos].slice(0, 5);
        renderPhotoPreviews();
    };

    const renderPhotoPreviews = () => {
        photoPreviewContainer.innerHTML = '';
        clientFeedbackState.photos.forEach((photo, index) => {
            const photoWrapper = document.createElement('div');
            photoWrapper.className = 'photo-preview-wrapper';
            photoWrapper.innerHTML = `
                <img src="${photo}" alt="Feedback Photo ${index + 1}">
                <button type="button" class="remove-photo-button" data-index="${index}">
                    <svg class="lucide-x" xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 6 6 18"/><path d="m6 6 12 12"/></svg>
                </button>
            `;
            photoPreviewContainer.appendChild(photoWrapper);
        });

        if (clientFeedbackState.photos.length < 5) {
            const addPhotoLabel = document.createElement('label');
            addPhotoLabel.className = 'photo-upload-placeholder';
            addPhotoLabel.innerHTML = `
                <input type="file" id="photoUploadInput" multiple accept="image/*" class="hidden-input">
                <div class="text-center">
                    <svg class="lucide-camera photo-upload-icon" xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14.5 4h-5L7 7H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-3l-2.5-3z"/><circle cx="12" cy="13" r="3"/></svg>
                    <span class="text-xs text-gray-500">Add Photo</span>
                </div>
            `;
            photoPreviewContainer.appendChild(addPhotoLabel);
            addPhotoLabel.querySelector('#photoUploadInput').addEventListener('change', handlePhotoUpload);
        }
        lucide.createIcons();
    };

    renderPhotoPreviews();

    photoPreviewContainer.addEventListener('click', (e) => {
        const removeButton = e.target.closest('.remove-photo-button');
        if (removeButton) {
            const indexToRemove = parseInt(removeButton.dataset.index);
            if (clientFeedbackState.photos[indexToRemove]) {
                URL.revokeObjectURL(clientFeedbackState.photos[indexToRemove]);
            }
            clientFeedbackState.photos = clientFeedbackState.photos.filter((_, i) => i !== indexToRemove);
            renderPhotoPreviews();
        }
    });

    // Recommendation Toggle Logic (Client-side only)
    recommendToggle.addEventListener('click', () => {
        clientFeedbackState.wouldRecommend = !clientFeedbackState.wouldRecommend;
        recommendToggle.classList.toggle('active', clientFeedbackState.wouldRecommend);
        recommendToggle.setAttribute('aria-pressed', clientFeedbackState.wouldRecommend);
    });

    // Submit Form Logic
    feedbackForm.addEventListener('submit', async (e) => {
        e.preventDefault();

        // This check uses the clientFeedbackState.rating, ensure it's synced with the hidden input.
        if (clientFeedbackState.rating === 0) {
            alert('Please provide an overall rating.');
            return;
        }

        submitBtn.disabled = true;
        submitBtn.innerHTML = `<div class="spinner"></div><span>Submitting...</span>`;

        feedbackForm.submit();
    });

    // Removed the API message handling from here, it's now in the inline script.
});