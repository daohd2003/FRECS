/**
 * Global Toast Manager
 * Handles all toast notifications with automatic cleanup on page navigation
 */

// Prevent duplicate declaration
if (typeof window.ToastManager === 'undefined') {
window.ToastManager = class ToastManager {
    constructor() {
        this.toasts = new Set();
        this.init();
    }

    init() {
        this.createStyles();
        this.setupPageNavigationCleanup();
        this.processTempDataMessages();
    }

    createStyles() {
        if (document.getElementById('toast-manager-styles')) return;

        const styles = document.createElement('style');
        styles.id = 'toast-manager-styles';
        styles.textContent = `
            .toast-container {
                position: fixed;
                top: 20px;
                right: 20px;
                z-index: 9999;
                pointer-events: none;
            }

            .toast-item {
                min-width: 300px;
                margin-bottom: 10px;
                padding: 12px 20px;
                border-radius: 8px;
                color: white;
                font-size: 14px;
                box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
                transform: translateX(400px);
                opacity: 0;
                transition: all 0.3s ease-in-out;
                pointer-events: auto;
                position: relative;
                word-wrap: break-word;
            }

            .toast-item.show {
                transform: translateX(0);
                opacity: 1;
            }

            .toast-item.success {
                background: linear-gradient(135deg, #4CAF50, #45a049);
            }

            .toast-item.error {
                background: linear-gradient(135deg, #f44336, #da190b);
            }

            .toast-item.info {
                background: linear-gradient(135deg, #2196F3, #0b7dda);
            }

            .toast-item.warning {
                background: linear-gradient(135deg, #ff9800, #e68900);
            }

            .toast-close {
                position: absolute;
                top: 8px;
                right: 10px;
                background: none;
                border: none;
                color: white;
                font-size: 18px;
                cursor: pointer;
                opacity: 0.7;
                transition: opacity 0.2s;
            }

            .toast-close:hover {
                opacity: 1;
            }

            .toast-progress {
                position: absolute;
                bottom: 0;
                left: 0;
                height: 3px;
                background: rgba(255, 255, 255, 0.3);
                transition: width linear;
            }
        `;
        document.head.appendChild(styles);
    }

    getContainer() {
        let container = document.getElementById('toast-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toast-container';
            container.className = 'toast-container';
            document.body.appendChild(container);
        }
        return container;
    }

    show(message, type = 'info', duration = 5000) {
        const container = this.getContainer();
        const toast = document.createElement('div');
        const toastId = Date.now() + Math.random();
        
        toast.className = `toast-item ${type}`;
        toast.dataset.toastId = toastId;
        toast.innerHTML = `
            <div>${message}</div>
            <button class="toast-close" onclick="window.toastManager.close('${toastId}')">&times;</button>
            <div class="toast-progress"></div>
        `;

        container.appendChild(toast);
        this.toasts.add(toastId);

        // Animate in
        setTimeout(() => toast.classList.add('show'), 50);

        // Progress bar animation
        const progressBar = toast.querySelector('.toast-progress');
        setTimeout(() => {
            progressBar.style.width = '100%';
            progressBar.style.transitionDuration = `${duration}ms`;
        }, 100);

        // Auto remove
        setTimeout(() => {
            this.close(toastId);
        }, duration);

        return toastId;
    }

    close(toastId) {
        const toast = document.querySelector(`[data-toast-id="${toastId}"]`);
        if (toast) {
            toast.classList.remove('show');
            setTimeout(() => {
                toast.remove();
                this.toasts.delete(toastId);
            }, 300);
        }
    }

    closeAll() {
        this.toasts.forEach(toastId => {
            const toast = document.querySelector(`[data-toast-id="${toastId}"]`);
            if (toast) {
                toast.style.transition = 'all 0.2s ease-out';
                toast.style.transform = 'translateX(400px)';
                toast.style.opacity = '0';
            }
        });
        
        setTimeout(() => {
            const container = document.getElementById('toast-container');
            if (container) {
                container.innerHTML = '';
            }
            this.toasts.clear();
        }, 200);
    }

    setupPageNavigationCleanup() {
        // Clear toasts before page unload
        window.addEventListener('beforeunload', () => {
            this.closeAll();
        });

        // Clear toasts on page visibility change (when switching tabs)
        document.addEventListener('visibilitychange', () => {
            if (document.visibilityState === 'hidden') {
                this.closeAll();
            }
        });

        // Handle single page application navigation
        const originalPushState = history.pushState;
        const originalReplaceState = history.replaceState;

        history.pushState = (...args) => {
            this.closeAll();
            originalPushState.apply(history, args);
        };

        history.replaceState = (...args) => {
            this.closeAll();
            originalReplaceState.apply(history, args);
        };

        window.addEventListener('popstate', () => {
            this.closeAll();
        });
    }

    processTempDataMessages() {
        // Process TempData messages from server-side
        const tempDataElement = document.getElementById('tempdata-messages');
        if (tempDataElement) {
            try {
                const messages = JSON.parse(tempDataElement.textContent);
                messages.forEach(msg => {
                    this.show(msg.message, msg.type, msg.duration || 5000);
                });
                tempDataElement.remove(); // Remove to prevent re-processing
            } catch (e) {
                console.warn('Failed to parse TempData messages:', e);
            }
        }

        // Process legacy toast elements and convert them
        this.convertLegacyToasts();
    }

    convertLegacyToasts() {
        // Convert existing success/error messages to toast
        const successMsg = document.getElementById('toast-success');
        const errorMsg = document.getElementById('toast-error');

        if (successMsg && successMsg.textContent.trim()) {
            this.show(successMsg.textContent.trim(), 'success');
            successMsg.remove();
        }

        if (errorMsg && errorMsg.textContent.trim()) {
            this.show(errorMsg.textContent.trim(), 'error');
            errorMsg.remove();
        }

        // Convert other legacy toast formats
        document.querySelectorAll('.toast-message, .alert-success, .alert-danger, .alert-info').forEach(el => {
            if (el.textContent.trim()) {
                let type = 'info';
                if (el.classList.contains('alert-success') || el.classList.contains('bg-success')) type = 'success';
                if (el.classList.contains('alert-danger') || el.classList.contains('bg-danger')) type = 'error';
                if (el.classList.contains('alert-warning') || el.classList.contains('bg-warning')) type = 'warning';
                
                this.show(el.textContent.trim(), type);
                el.remove();
            }
        });
    }

    // Convenience methods
    success(message, duration) {
        return this.show(message, 'success', duration);
    }

    error(message, duration) {
        return this.show(message, 'error', duration);
    }

    info(message, duration) {
        return this.show(message, 'info', duration);
    }

    warning(message, duration) {
        return this.show(message, 'warning', duration);
    }
};
} // End of ToastManager check

// Global instance (only create if not exists)
if (!window.toastManager) {
    window.toastManager = new window.ToastManager();
}

// Global functions for backward compatibility
window.showToast = (message, type, duration) => window.toastManager.show(message, type, duration);
window.showSuccessToast = (message, duration) => window.toastManager.success(message, duration);
window.showErrorToast = (message, duration) => window.toastManager.error(message, duration);
window.showInfoToast = (message, duration) => window.toastManager.info(message, duration);
window.showWarningToast = (message, duration) => window.toastManager.warning(message, duration);
