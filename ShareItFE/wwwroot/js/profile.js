document.addEventListener('DOMContentLoaded', () => {
    // ==== CHUYỂN TAB ====
    const navLinks = document.querySelectorAll('.nav-link');
    const tabContents = document.querySelectorAll('.content-panel');

    function showTab(tabId) {
        navLinks.forEach(link => link.classList.toggle('active', link.dataset.tab === tabId));
        tabContents.forEach(content => {
            const match = content.getAttribute('data-tab-content') === tabId;
            content.classList.toggle('active', match);
            content.classList.toggle('hidden', !match);
        });
    }

    // ==== CHẾ ĐỘ EDIT PROFILE ====
    const editButton = document.getElementById('edit-profile-button');
    const cancelButton = document.getElementById('cancel-edit-button');
    const displayView = document.getElementById('profile-display-view');
    const editForm = document.getElementById('profile-edit-form');

    function toggleEditMode(isEditing) {
        displayView.classList.toggle('hidden', isEditing);
        editForm.classList.toggle('hidden', !isEditing);
        editButton.querySelector('span').textContent = isEditing ? 'Cancel' : 'Edit';
    }

    const avatarForm = document.getElementById('avatar-form');
    const avatarInput = document.getElementById('avatar-input');
    const pageAvatarPreview = document.getElementById('avatar-preview');

    // Các element của modal
    const modal = document.getElementById('avatar-modal');
    const modalAvatarPreview = document.getElementById('modal-avatar-preview');
    const closeModalBtn = document.getElementById('modal-close-btn');
    const cancelModalBtn = document.getElementById('modal-cancel-btn');
    const saveModalBtn = document.getElementById('modal-save-btn');

    // Hàm để mở modal
    function openModal() {
        modal.classList.remove('hidden');
    }

    // Hàm để đóng modal
    function closeModal() {
        modal.classList.add('hidden');
        avatarInput.value = ""; // Reset input file để có thể chọn lại file cũ
    }

    // 1. Khi người dùng chọn file
    avatarInput.addEventListener('change', () => {
        const file = avatarInput.files[0];
        if (file) {
            // Hiển thị ảnh preview bên trong modal
            modalAvatarPreview.src = URL.createObjectURL(file);
            // Mở modal
            openModal();
        }
    });

    // 2. Khi người dùng nhấn nút "Lưu" trên modal
    saveModalBtn.addEventListener('click', () => {
        // Gửi form đi để upload
        avatarForm.submit();
        // Đóng modal
        closeModal();
    });

    // 3. Các cách để đóng modal mà không lưu
    closeModalBtn.addEventListener('click', closeModal);
    cancelModalBtn.addEventListener('click', closeModal);
    // Nhấn ra ngoài vùng tối cũng đóng modal
    modal.addEventListener('click', (event) => {
        if (event.target === modal) {
            closeModal();
        }
    });

    // ==== GÁN SỰ KIỆN ====
    navLinks.forEach(link => {
        link.addEventListener('click', (e) => {
            e.preventDefault();
            const tabId = link.dataset.tab;
            showTab(tabId);
        });
    });

    if (editButton) {
        editButton.addEventListener('click', () => {
            const isCurrentlyEditing = !editForm.classList.contains('hidden');
            toggleEditMode(!isCurrentlyEditing);
        });
    }

    if (cancelButton) {
        cancelButton.addEventListener('click', () => {
            toggleEditMode(false);
        });
    }

    // ==== KHỞI TẠO ====
    const urlParams = new URLSearchParams(window.location.search);
    const initialTab = urlParams.get('tab') || 'profile';
    showTab(initialTab);
});

// === HÀM HỖ TRỢ ===
function getCookie(name) {
    const value = `; ${document.cookie}`;
    const parts = value.split(`; ${name}=`);
    if (parts.length === 2) return parts.pop().split(';').shift();
    return null;
}
