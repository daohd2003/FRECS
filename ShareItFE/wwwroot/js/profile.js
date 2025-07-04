document.addEventListener('DOMContentLoaded', () => {
    // ==== KHAI BÁO BIẾN DOM ====
    // Tab
    const navLinks = document.querySelectorAll('.nav-link');
    const tabContents = document.querySelectorAll('.content-panel');

    // Edit Profile
    const editButton = document.getElementById('edit-profile-button');
    const cancelButton = document.getElementById('cancel-edit-button');
    const displayView = document.getElementById('profile-display-view');
    const editForm = document.getElementById('profile-edit-form');

    // Avatar và Modal
    const avatarForm = document.getElementById('avatar-form');
    const avatarInput = document.getElementById('avatar-input');
    const modal = document.getElementById('avatar-modal');
    const modalAvatarPreview = document.getElementById('modal-avatar-preview');
    const closeModalBtn = document.getElementById('modal-close-btn');
    const cancelModalBtn = document.getElementById('modal-cancel-btn');
    const saveModalBtn = document.getElementById('modal-save-btn');


    // ==== CÁC HÀM XỬ LÝ ====

    // Hàm hiển thị tab dựa vào tabId
    function showTab(tabId) {
        navLinks.forEach(link => link.classList.toggle('active', link.dataset.tab === tabId));
        tabContents.forEach(content => {
            const match = content.getAttribute('data-tab-content') === tabId;
            content.classList.toggle('hidden', !match);
        });
    }

    // Hàm bật/tắt chế độ sửa profile
    function toggleEditMode(isEditing) {
        if (displayView && editForm && editButton) {
            displayView.classList.toggle('hidden', isEditing);
            editForm.classList.toggle('hidden', !isEditing);
            // Thay đổi text của nút Edit/Cancel
            const buttonTextSpan = editButton.querySelector('span');
            if (buttonTextSpan) {
                buttonTextSpan.textContent = isEditing ? 'Cancel' : 'Edit';
            }
        }
    }

    // Hàm đóng modal upload avatar
    function closeModal() {
        if (modal) {
            modal.classList.add('hidden');
        }
        // Reset input để có thể chọn lại cùng 1 file ảnh
        if (avatarInput) {
            avatarInput.value = "";
        }
    }


    // ==== GÁN CÁC SỰ KIỆN (EVENT LISTENERS) ====

    // 1. Sự kiện click vào các link tab
    navLinks.forEach(link => {
        link.addEventListener('click', (e) => {
            e.preventDefault();
            const tabId = link.dataset.tab;

            // ---- PHẦN SỬA QUAN TRỌNG ĐƯỢC THÊM LẠI ----
            // Cập nhật URL với tham số 'tab' mà không tải lại trang.
            // Điều này cực kỳ quan trọng để phân trang hoạt động đúng.
            const url = new URL(window.location);
            url.searchParams.set('tab', tabId);
            // Khi người dùng chủ động chuyển tab, nên reset phân trang về trang 1
            url.searchParams.set('page', '1');
            history.pushState({}, '', url);
            // ---- HẾT PHẦN SỬA ----

            showTab(tabId);
        });
    });

    // 2. Sự kiện cho nút Edit/Cancel Profile
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

    // 3. Sự kiện cho việc chọn file Avatar
    if (avatarInput) {
        avatarInput.addEventListener('change', () => {
            const file = avatarInput.files[0];
            if (file && modalAvatarPreview) {
                modalAvatarPreview.src = URL.createObjectURL(file);
                if (modal) modal.classList.remove('hidden');
            }
        });
    }

    // 4. Sự kiện cho các nút trong Modal
    if (saveModalBtn) {
        saveModalBtn.addEventListener('click', () => {
            if (avatarForm) avatarForm.submit();
            closeModal();
        });
    }
    if (closeModalBtn) closeModalBtn.addEventListener('click', closeModal);
    if (cancelModalBtn) cancelModalBtn.addEventListener('click', closeModal);
    if (modal) {
        modal.addEventListener('click', (event) => {
            if (event.target === modal) {
                closeModal(); // Đóng modal khi click ra vùng nền tối
            }
        });
    }


    // ==== KHỞI TẠO TRẠNG THÁI BAN ĐẦU CỦA TRANG ====
    // Đọc tham số 'tab' từ URL để mở đúng tab khi tải trang (hoặc sau khi phân trang)
    const urlParams = new URLSearchParams(window.location.search);
    const initialTab = urlParams.get('tab') || 'profile';
    showTab(initialTab);
});


// Bạn có thể giữ lại hàm helper này nếu cần dùng đến ở đâu đó
function getCookie(name) {
    const value = `; ${document.cookie}`;
    const parts = value.split(`; ${name}=`);
    if (parts.length === 2) return parts.pop().split(';').shift();
    return null;
}