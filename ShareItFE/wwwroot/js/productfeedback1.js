// --- PHẦN 3: LOGIC CHO FEEDBACK MODAL (MỚI THÊM) ---

const openFeedbackModalBtn = document.getElementById('openFeedbackModalBtn');
const closeFeedbackModalBtn = document.getElementById('closeFeedbackModalBtn');
const feedbackModal = document.getElementById('feedbackModal');
const starRating = document.getElementById('starRating');
const feedbackRatingInput = document.getElementById('feedbackRatingInput');

if (openFeedbackModalBtn) {
    openFeedbackModalBtn.addEventListener('click', () => {
        if (!accessToken) { // Assuming accessToken indicates user is logged in
            alert("Please log in to leave a feedback.");
            return;
        }
        feedbackModal.classList.remove('hidden');
        document.body.classList.add('overflow-hidden');
        // Reset form fields if needed
        feedbackRatingInput.value = '0';
        document.getElementById('feedbackComment').value = '';
        updateStarRating(0); // Reset stars
    });
}

if (closeFeedbackModalBtn) {
    closeFeedbackModalBtn.addEventListener('click', () => {
        feedbackModal.classList.add('hidden');
        document.body.classList.remove('overflow-hidden');
    });
}

if (starRating) {
    starRating.addEventListener('click', (e) => {
        const clickedStar = e.target.closest('i[data-rating]');
        if (clickedStar) {
            const rating = parseInt(clickedStar.dataset.rating);
            updateStarRating(rating);
            feedbackRatingInput.value = rating;
        }
    });
}

function updateStarRating(rating) {
    // 1. Ensure 'rating' is a valid number. If it's NaN, default to 0.
    const validRating = isNaN(parseInt(rating, 10)) ? 0 : parseInt(rating, 10);

    starRating.querySelectorAll('i[data-lucide="star"]').forEach(star => {
        // 2. Parse 'starValue' with radix 10 and handle potential NaN.
        const starValue = parseInt(star.dataset.rating, 10);

        // Defensive check: If starValue is NaN, skip processing this star
        if (isNaN(starValue)) {
            console.warn("Skipping star due to invalid data-rating:", star);
            return; // Exit current iteration and move to the next star
        }

        // Now, both starValue and validRating are guaranteed to be numbers
        if (starValue <= validRating) {
            star.classList.remove('text-gray-300', 'hover:text-yellow-400');
            star.classList.add('fill-yellow-400', 'text-yellow-400');
        } else {
            star.classList.remove('fill-yellow-400', 'text-yellow-400');
            star.classList.add('text-gray-300', 'hover:text-yellow-400');
        }
    });
}