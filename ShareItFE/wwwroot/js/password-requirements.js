/**
 * Password Requirements Component
 * Reusable component for password validation with visual feedback
 */

class PasswordRequirements {
    constructor(inputId, requirementsId, options = {}) {
        this.inputId = inputId;
        this.requirementsId = requirementsId;
        this.options = {
            minLength: 8,
            requireUppercase: true,
            requireLowercase: true,
            requireNumber: true,
            requireSpecial: true,
            autoHide: true,
            hideDelay: 150,
            ...options
        };
        
        this.init();
    }
    
    init() {
        this.input = document.getElementById(this.inputId);
        this.requirementsDiv = document.getElementById(this.requirementsId);
        
        if (!this.input || !this.requirementsDiv) {
            console.error('Password requirements: Input or requirements container not found');
            return;
        }
        
        this.bindEvents();
        this.initializeState();
    }
    
    bindEvents() {
        this.input.addEventListener('focus', () => this.show());
        this.input.addEventListener('input', (e) => this.validate(e.target.value));
        
        if (this.options.autoHide) {
            this.input.addEventListener('blur', () => this.hide());
        }
    }
    
    initializeState() {
        // Initialize all requirements as neutral
        this.validate('');
    }
    
    show() {
        if (this.requirementsDiv) {
            this.requirementsDiv.style.display = 'block';
            this.requirementsDiv.classList.remove('login-mode');
        }
    }
    
    hide() {
        if (this.requirementsDiv && this.options.autoHide) {
            setTimeout(() => {
                this.requirementsDiv.style.display = 'none';
            }, this.options.hideDelay);
        }
    }
    
    validate(password) {
        const requirements = {
            length: password.length >= this.options.minLength,
            uppercase: this.options.requireUppercase ? /[A-Z]/.test(password) : true,
            lowercase: this.options.requireLowercase ? /[a-z]/.test(password) : true,
            number: this.options.requireNumber ? /\d/.test(password) : true,
            special: this.options.requireSpecial ? /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password) : true
        };
        
        // Update UI for each requirement
        Object.keys(requirements).forEach(requirement => {
            const item = this.requirementsDiv.querySelector(`[data-requirement="${requirement}"]`);
            if (item) {
                const isValid = requirements[requirement];
                item.classList.remove('valid', 'invalid', 'neutral');
                
                if (password.length === 0) {
                    item.classList.add('neutral');
                } else if (isValid) {
                    item.classList.add('valid');
                } else {
                    item.classList.add('invalid');
                }
            }
        });
        
        return requirements;
    }
    
    isValid(password) {
        const requirements = this.validate(password);
        return Object.values(requirements).every(req => req === true);
    }
    
    // Static method to create requirements HTML
    static createRequirementsHTML(options = {}) {
        const requirements = [
            { key: 'length', text: `At least ${options.minLength || 8} characters` },
            { key: 'uppercase', text: 'One uppercase letter' },
            { key: 'lowercase', text: 'One lowercase letter' },
            { key: 'number', text: 'One number' },
            { key: 'special', text: 'One special character (!@#$%^&*)' }
        ];
        
        return `
            <div class="password-requirements" style="display: none;">
                <div class="requirements-header">
                    <h4>Password Requirements</h4>
                </div>
                <div class="requirements-list">
                    ${requirements.map(req => `
                        <div class="requirement-item" data-requirement="${req.key}">
                            <span class="requirement-icon">
                                <svg class="check-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <polyline points="20 6 9 17 4 12"></polyline>
                                </svg>
                                <svg class="x-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <line x1="18" y1="6" x2="6" y2="18"></line>
                                    <line x1="6" y1="6" x2="18" y2="18"></line>
                                </svg>
                            </span>
                            <span class="requirement-text">${req.text}</span>
                        </div>
                    `).join('')}
                </div>
            </div>
        `;
    }
}

// Legacy functions for backward compatibility
function showPasswordRequirements() {
    if (window.passwordRequirements) {
        window.passwordRequirements.show();
    }
}

function hidePasswordRequirements() {
    if (window.passwordRequirements) {
        window.passwordRequirements.hide();
    }
}

function validatePasswordRequirements(password) {
    if (window.passwordRequirements) {
        return window.passwordRequirements.validate(password);
    }
}

function isPasswordValid(password) {
    if (window.passwordRequirements) {
        return window.passwordRequirements.isValid(password);
    }
    return false;
}

// Auto-initialize if elements are found
document.addEventListener('DOMContentLoaded', function() {
    const passwordInput = document.getElementById('password');
    const requirementsDiv = document.getElementById('password-requirements');
    
    if (passwordInput && requirementsDiv) {
        window.passwordRequirements = new PasswordRequirements('password', 'password-requirements');
    }
});
