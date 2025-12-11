/**
 * Floating Chat Manager - Messenger Style
 * Manages multiple floating chat windows that persist across page navigation
 */

// Guard against duplicate declaration
if (typeof FloatingChatManager === 'undefined') {

class FloatingChatManager {
    constructor() {
        this.chats = new Map();
        this.signalRConnection = null;
        this.currentUserId = null;
        this.accessToken = null;
        this.apiUrl = null;
        this.signalRUrl = null;
        this.container = null;
        this.timestampUpdateInterval = null;
        
        this.init();
    }

    init() {
        // Get config from window or cookies
        this.loadConfig();
        
        // Create container
        this.createContainer();
        
        // Load saved chats from localStorage
        this.loadSavedChats();
        
        // Setup SignalR if we have token
        if (this.accessToken) {
            this.setupSignalR();
        }
        
        // Start timestamp auto-update interval (every 30 seconds)
        this.startTimestampUpdater();
    }
    
    /**
     * Start interval to auto-update message timestamps
     * Updates "Just now" -> "1m ago" -> "2m ago" etc.
     */
    startTimestampUpdater() {
        // Clear existing interval if any
        if (this.timestampUpdateInterval) {
            clearInterval(this.timestampUpdateInterval);
        }
        
        // Update timestamps every 30 seconds
        this.timestampUpdateInterval = setInterval(() => {
            this.updateAllTimestamps();
        }, 30000);
    }
    
    /**
     * Update all message timestamps in all chat windows
     */
    updateAllTimestamps() {
        this.chats.forEach((chat) => {
            if (!chat.element) return;
            
            const timeElements = chat.element.querySelectorAll('.message-time[data-timestamp]');
            timeElements.forEach((el) => {
                const timestamp = el.dataset.timestamp;
                if (timestamp) {
                    el.textContent = this.formatTime(timestamp);
                }
            });
        });
    }

    loadConfig() {
        // Try to get from adminChatConfig first (server-side rendered)
        if (window.adminChatConfig) {
            this.currentUserId = window.adminChatConfig.currentUserId;
            this.accessToken = window.adminChatConfig.accessToken;
            this.apiUrl = window.adminChatConfig.apiBaseUrl;
            this.signalRUrl = window.adminChatConfig.signalRRootUrl;
            this.currentUserRole = window.adminChatConfig.currentUserRole;
        }
        
        // Fallback to cookies if not available from config
        if (!this.currentUserId) {
            this.currentUserId = this.getCookie('UserId');
        }
        if (!this.accessToken) {
            this.accessToken = this.getCookie('AccessToken');
        }
        if (!this.currentUserRole) {
            this.currentUserRole = this.getCookie('UserRole');
        }
        // Fallback: Try to detect role from URL path
        if (!this.currentUserRole) {
            const path = window.location.pathname.toLowerCase();
            if (path.includes('/provider/')) {
                this.currentUserRole = 'provider';
            } else if (path.includes('/admin/')) {
                this.currentUserRole = 'admin';
            } else if (path.includes('/staff/')) {
                this.currentUserRole = 'staff';
            } else if (path.includes('/customer/')) {
                this.currentUserRole = 'customer';
            }
        }
        
        // Fallback to global config for API URLs
        if (!this.apiUrl && window.apiSettings) {
            this.apiUrl = window.apiSettings.baseUrl;
            this.signalRUrl = window.apiSettings.rootUrl;
        }
    }

    getCookie(name) {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return null;
    }

    createContainer() {
        // Wait for DOM to be ready before creating container
        const createContainerElements = () => {
            if (document.getElementById('floating-chats-container')) {
                this.container = document.getElementById('floating-chats-container');
                this.expandedContainer = document.getElementById('floating-chats-expanded');
                this.minimizedContainer = document.getElementById('floating-chats-minimized');
                return;
            }

            this.container = document.createElement('div');
            this.container.id = 'floating-chats-container';
            this.container.className = 'floating-chats-container';
            
            // Create expanded container (horizontal)
            this.expandedContainer = document.createElement('div');
            this.expandedContainer.id = 'floating-chats-expanded';
            this.expandedContainer.className = 'floating-chats-expanded';
            
            // Create minimized container (vertical)
            this.minimizedContainer = document.createElement('div');
            this.minimizedContainer.id = 'floating-chats-minimized';
            this.minimizedContainer.className = 'floating-chats-minimized';
            
            this.container.appendChild(this.expandedContainer);
            this.container.appendChild(this.minimizedContainer);
            document.body.appendChild(this.container);
        };

        // If DOM is ready, create immediately, otherwise wait
        if (document.body) {
            createContainerElements();
        } else {
            document.addEventListener('DOMContentLoaded', createContainerElements);
        }
    }

    loadSavedChats() {
        try {
            const saved = localStorage.getItem('floatingChats');
            if (!saved) return;

            const chatStates = JSON.parse(saved);
            chatStates.forEach(state => {
                this.openChat(state.userId, state.userName, state.avatar, state.minimized, state.role);
            });
        } catch (e) {
            console.error('Error loading saved chats:', e);
        }
    }

    saveChatsState() {
        try {
            const states = [];
            this.chats.forEach((chat) => {
                states.push({
                    userId: chat.userId,
                    userName: chat.userName,
                    avatar: chat.avatar,
                    minimized: chat.isMinimized,
                    role: chat.role
                });
            });
            localStorage.setItem('floatingChats', JSON.stringify(states));
        } catch (e) {
            console.error('Error saving chats state:', e);
        }
    }

    async setupSignalR() {
        if (!this.accessToken || this.signalRConnection) return;

        try {
            this.signalRConnection = new signalR.HubConnectionBuilder()
                .withUrl(`${this.signalRUrl}/chathub?access_token=${this.accessToken}`)
                .withAutomaticReconnect()
                .build();

            // IMPORTANT: Remove all old handlers first to prevent duplicates
            this.signalRConnection.off("ReceiveMessage");
            
            // Then register the new handler
            this.signalRConnection.on("ReceiveMessage", (message) => {
                this.handleIncomingMessage(message);
            });

            await this.signalRConnection.start();
            console.log('FloatingChat: SignalR Connected');
        } catch (err) {
            console.error('FloatingChat: SignalR Error:', err);
        }
    }

    async handleIncomingMessage(message) {
        // Ignore messages sent by current user (prevent duplicate chat window)
        if (message.senderId === this.currentUserId) {
            return;
        }
        
        let chat = this.chats.get(message.senderId);
        
        if (chat) {
            // Update product context if message has new product context
            // All users (Provider, Staff, Admin) see product banner when receiving message with product
            if (message.productContext && message.productContext.id) {
                chat.productContext = message.productContext;
                chat._productContextExplicitlySet = true;
                this.updateProductBanner(chat);
            }
            
            // Add message to existing chat
            this.addMessageToChat(chat, message, false);
            
            // Show unread badge if minimized, mark as read if expanded
            if (chat.isMinimized) {
                chat.unreadCount++;
                this.updateUnreadBadge(chat);
            } else {
                this.scrollToBottom(chat);
                // Mark messages as read since chat is expanded and visible
                if (chat.conversationId) {
                    this.markMessagesAsRead(chat.conversationId);
                }
            }
        } else {
            // Auto-open new chat window for incoming message (will be expanded by default)
            // Get sender info from message or fetch from API
            await this.openChatForIncomingMessage(message);
        }
    }
    
    /**
     * Mark all messages in a conversation as read
     * Called when chat window is expanded and messages are visible
     */
    async markMessagesAsRead(conversationId) {
        if (!conversationId || !this.accessToken) return;
        
        try {
            await fetch(`${this.apiUrl}/conversations/${conversationId}/mark-read`, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${this.accessToken}`
                }
            });
        } catch (error) {
            // Silently fail - not critical if mark-read fails
            console.error('Error marking messages as read:', error);
        }
    }
    
    async openChatForIncomingMessage(message) {
        try {
            // Try to get user info from conversation
            const response = await fetch(
                `${this.apiUrl}/conversations/find-or-create`,
                {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${this.accessToken}`
                    },
                    body: JSON.stringify({ recipientId: message.senderId })
                }
            );
            
            if (response.ok) {
                const conversation = await response.json();
                const senderName = conversation.otherParticipant?.fullName || 'User';
                const senderRole = conversation.otherParticipant?.role || null;
                const avatar = this.getInitials(senderName);
                
                // Open chat with proper name, role and product context from message
                // All users see product banner when receiving message with product
                const productContext = message.productContext || null;
                await this.openChat(message.senderId, senderName, avatar, false, senderRole, productContext);
                
                // Add the message
                const chat = this.chats.get(message.senderId);
                if (chat) {
                    this.addMessageToChat(chat, message, false);
                    this.scrollToBottom(chat);
                    // Mark as read since chat is opened in expanded state
                    if (chat.conversationId) {
                        this.markMessagesAsRead(chat.conversationId);
                    }
                }
            }
        } catch (error) {
            console.error('Error opening chat for incoming message:', error);
            // Fallback: open with basic info
            const senderName = 'User';
            const avatar = this.getInitials(senderName);
            await this.openChat(message.senderId, senderName, avatar, false, null);
        }
    }

    async openChat(userId, userName, avatar = null, minimized = false, role = null, productContext = null) {
        // Check if chat already exists
        if (this.chats.has(userId)) {
            const chat = this.chats.get(userId);
            
            // Update name if a better name is provided (not generic like "Provider", "User", etc.)
            const genericNames = ['Provider', 'User', 'Customer', 'Staff', 'Admin'];
            if (userName && !genericNames.includes(userName) && genericNames.includes(chat.userName)) {
                chat.userName = userName;
                chat.displayName = role ? `${userName} (${role})` : (chat.role ? `${userName} (${chat.role})` : userName);
                // Update UI
                const nameElement = chat.element?.querySelector('.chat-user-name');
                if (nameElement) {
                    nameElement.textContent = chat.displayName;
                }
                this.saveChatsState();
            }
            
            // Update product context if provided, or clear if explicitly set to null
            if (productContext !== undefined) {
                chat.productContext = productContext;
                chat._productContextExplicitlySet = true; // Flag to prevent override from message history
                this.updateProductBanner(chat);
            }
            
            if (chat.isMinimized) {
                this.toggleMinimize(userId);
            }
            return;
        }

        // Format display name with role if available
        const displayName = role ? `${userName} (${role})` : userName;

        // Create chat window
        const chat = {
            userId: userId,
            userName: userName,
            displayName: displayName,
            role: role,
            avatar: avatar || this.getInitials(userName),
            conversationId: null,
            messages: [],
            messagesLoaded: false, // Track if messages have been loaded
            isMinimized: minimized,
            unreadCount: 0,
            element: null,
            productContext: productContext, // Store product context for this chat
            _productContextExplicitlySet: productContext !== undefined // Flag to prevent override from message history
        };

        // Create UI
        chat.element = this.createChatWindow(chat);
        
        // Add to appropriate container
        if (chat.isMinimized) {
            this.minimizedContainer.appendChild(chat.element);
        } else {
            this.expandedContainer.appendChild(chat.element);
        }

        // Add to map
        this.chats.set(userId, chat);

        // Load conversation (this will update userName from API)
        if (!minimized) {
            await this.loadConversation(chat);
        }

        // Save state after loadConversation updates userName
        this.saveChatsState();
    }

    createChatWindow(chat) {
        const window = document.createElement('div');
        window.className = `floating-chat-window ${chat.isMinimized ? 'minimized' : ''}`;
        window.dataset.userId = chat.userId;

        window.innerHTML = `
            <div class="floating-chat-header">
                <div class="chat-header-info">
                    <div class="chat-avatar">${this.escapeHtml(chat.avatar)}</div>
                    <div class="chat-user-name">${this.escapeHtml(chat.displayName || chat.userName)}</div>
                </div>
                <div class="chat-header-actions">
                    <button type="button" class="chat-header-btn minimize-btn" title="Minimize">
                        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <line x1="5" y1="12" x2="19" y2="12"></line>
                        </svg>
                    </button>
                    <button type="button" class="chat-header-btn close-btn" title="Close">
                        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <line x1="18" y1="6" x2="6" y2="18"></line>
                            <line x1="6" y1="6" x2="18" y2="18"></line>
                        </svg>
                    </button>
                </div>
                ${chat.unreadCount > 0 ? `<span class="chat-unread-badge">${chat.unreadCount}</span>` : ''}
            </div>
            <div class="floating-chat-body">
                <div class="chat-product-banner hidden">
                    <img class="chat-product-thumb" src="" alt="Product" />
                    <div class="chat-product-info">
                        <div class="chat-product-label">About product:</div>
                        <div class="chat-product-name"></div>
                    </div>
                    <a href="#" class="chat-product-view-link" target="_blank" title="View product">
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path>
                            <polyline points="15 3 21 3 21 9"></polyline>
                            <line x1="10" y1="14" x2="21" y2="3"></line>
                        </svg>
                    </a>
                </div>
                <div class="chat-messages-area">
                    <div class="chat-loading">
                        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="animate-spin">
                            <path d="M21 12a9 9 0 1 1-6.219-8.56"></path>
                        </svg>
                        Loading messages...
                    </div>
                </div>
                <div class="chat-input-area">
                    <div class="chat-media-actions">
                        <button type="button" class="chat-media-btn" data-action="voice" title="Voice message">
                            <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                                <path d="M12 14c1.66 0 3-1.34 3-3V5c0-1.66-1.34-3-3-3S9 3.34 9 5v6c0 1.66 1.34 3 3 3zm-1-9c0-.55.45-1 1-1s1 .45 1 1v6c0 .55-.45 1-1 1s-1-.45-1-1V5z"/>
                                <path d="M17 11c0 2.76-2.24 5-5 5s-5-2.24-5-5H5c0 3.53 2.61 6.43 6 6.92V21h2v-3.08c3.39-.49 6-3.39 6-6.92h-2z"/>
                            </svg>
                        </button>
                        <button type="button" class="chat-media-btn" data-action="image" title="Send image/video">
                            <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                                <path d="M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H5V5h14v14zm-5-7l-3 3.72L9 13l-3 4h12l-4-5z"/>
                            </svg>
                        </button>
                        <button type="button" class="chat-media-btn" data-action="sticker" title="Stickers">
                            <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                                <path d="M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm3.5-9c.83 0 1.5-.67 1.5-1.5S16.33 8 15.5 8 14 8.67 14 9.5s.67 1.5 1.5 1.5zm-7 0c.83 0 1.5-.67 1.5-1.5S9.33 8 8.5 8 7 8.67 7 9.5 7.67 11 8.5 11zm3.5 6.5c2.33 0 4.31-1.46 5.11-3.5H6.89c.8 2.04 2.78 3.5 5.11 3.5z"/>
                            </svg>
                        </button>
                        <button type="button" class="chat-media-btn" data-action="gif" title="Send GIF">
                            <span class="gif-text">GIF</span>
                        </button>
                        <button type="button" class="chat-media-btn" data-action="product" title="Attach product">
                            <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                                <path d="M18 6h-2c0-2.21-1.79-4-4-4S8 3.79 8 6H6c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm-6-2c1.1 0 2 .9 2 2h-4c0-1.1.9-2 2-2zm6 16H6V8h2v2c0 .55.45 1 1 1s1-.45 1-1V8h4v2c0 .55.45 1 1 1s1-.45 1-1V8h2v12z"/>
                            </svg>
                        </button>
                    </div>
                    <form class="chat-input-form">
                        <textarea class="chat-input" placeholder="Aa" rows="1"></textarea>
                        <button type="button" class="chat-emoji-btn" data-action="emoji" title="Emoji">
                            <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                                <path d="M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm3.5-9c.83 0 1.5-.67 1.5-1.5S16.33 8 15.5 8 14 8.67 14 9.5s.67 1.5 1.5 1.5zm-7 0c.83 0 1.5-.67 1.5-1.5S9.33 8 8.5 8 7 8.67 7 9.5 7.67 11 8.5 11zm3.5 6.5c2.33 0 4.31-1.46 5.11-3.5H6.89c.8 2.04 2.78 3.5 5.11 3.5z"/>
                            </svg>
                        </button>
                        <button type="submit" class="chat-send-btn" title="Send">
                            <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
                                <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/>
                            </svg>
                        </button>
                        <button type="button" class="chat-like-btn" title="Like">
                            <svg width="24" height="24" viewBox="0 0 24 24" fill="currentColor">
                                <path d="M2 20h2c.55 0 1-.45 1-1v-9c0-.55-.45-1-1-1H2v11zm19.83-7.12c.11-.25.17-.52.17-.8V11c0-1.1-.9-2-2-2h-5.5l.92-4.65c.05-.22.02-.46-.08-.66-.23-.45-.52-.86-.88-1.22L14 2 7.59 8.41C7.21 8.79 7 9.3 7 9.83v7.84C7 18.95 8.05 20 9.34 20h8.11c.7 0 1.36-.37 1.72-.97l2.66-6.15z"/>
                            </svg>
                        </button>
                    </form>
                    <input type="file" class="chat-file-input" accept="image/*,video/*,audio/*" style="display:none">
                </div>
            </div>
        `;
        
        // Update product banner if product context exists
        if (chat.productContext) {
            this.updateProductBannerElement(window, chat.productContext);
        }

        // Event listeners
        const header = window.querySelector('.floating-chat-header');
        const minimizeBtn = window.querySelector('.minimize-btn');
        const closeBtn = window.querySelector('.close-btn');
        const form = window.querySelector('.chat-input-form');

        header.addEventListener('click', (e) => {
            if (chat.isMinimized && !e.target.closest('.chat-header-btn')) {
                this.toggleMinimize(chat.userId);
            }
        });

        if (minimizeBtn) {
            minimizeBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                this.toggleMinimize(chat.userId);
            });
        }

        if (closeBtn) {
            closeBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                this.closeChat(chat.userId);
            });
        }

        form.addEventListener('submit', (e) => {
            e.preventDefault();
            this.sendMessage(chat);
        });

        // Media action buttons
        const mediaButtons = window.querySelectorAll('.chat-media-btn');
        const fileInput = window.querySelector('.chat-file-input');
        const emojiBtn = window.querySelector('.chat-emoji-btn');
        const likeBtn = window.querySelector('.chat-like-btn');
        const chatInput = window.querySelector('.chat-input');

        mediaButtons.forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                const action = btn.dataset.action;
                this.handleMediaAction(chat, action, fileInput);
            });
        });

        if (emojiBtn) {
            emojiBtn.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                this.toggleEmojiPicker(chat, emojiBtn);
            });
        }

        if (likeBtn) {
            likeBtn.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                this.sendLikeEmoji(chat);
            });
        }

        // Toggle between like button and send button based on input + auto-resize textarea
        if (chatInput && likeBtn) {
            const sendBtn = form.querySelector('.chat-send-btn');
            const maxHeight = 150;
            
            chatInput.addEventListener('input', () => {
                // Auto-resize textarea
                chatInput.style.height = 'auto';
                const newHeight = Math.min(chatInput.scrollHeight, maxHeight);
                chatInput.style.height = newHeight + 'px';
                
                // Show scrollbar only when content exceeds max height
                if (chatInput.scrollHeight > maxHeight) {
                    chatInput.classList.add('has-scroll');
                } else {
                    chatInput.classList.remove('has-scroll');
                }
                
                // Toggle send/like button
                if (chatInput.value.trim()) {
                    likeBtn.style.display = 'none';
                    if (sendBtn) {
                        sendBtn.style.display = 'flex';
                        sendBtn.style.alignItems = 'center';
                        sendBtn.style.justifyContent = 'center';
                    }
                } else {
                    likeBtn.style.display = 'flex';
                    if (sendBtn) sendBtn.style.display = 'none';
                }
            });
            
            // Handle Enter key to send, Shift+Enter for new line
            chatInput.addEventListener('keydown', (e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    if (chatInput.value.trim()) {
                        form.dispatchEvent(new Event('submit'));
                    }
                }
            });
        }

        // File input change handler
        if (fileInput) {
            fileInput.addEventListener('change', (e) => {
                if (e.target.files && e.target.files[0]) {
                    this.handleFileUpload(chat, e.target.files[0]);
                    e.target.value = ''; // Reset input
                }
            });
        }

        return window;
    }

    handleMediaAction(chat, action, fileInput) {
        switch (action) {
            case 'voice':
                this.startVoiceRecording(chat);
                break;
            case 'image':
                if (fileInput) fileInput.click();
                break;
            case 'sticker':
                this.toggleStickerPicker(chat);
                break;
            case 'gif':
                this.toggleGifPicker(chat);
                break;
            case 'product':
                this.toggleProductPicker(chat);
                break;
        }
    }

    toggleEmojiPicker(chat, button) {
        // Simple emoji picker - insert common emojis
        const emojis = ['😀', '😂', '😍', '🥰', '😊', '😎', '🤔', '😢', '😡', '👍', '👎', '❤️', '🔥', '🎉', '👏'];
        const chatInput = chat.element.querySelector('.chat-input');
        
        // Check if picker already exists
        let picker = chat.element.querySelector('.emoji-picker');
        if (picker) {
            picker.remove();
            // Restore textarea height
            if (chatInput) {
                chatInput.classList.remove('picker-open');
                chatInput.style.height = 'auto';
                chatInput.style.height = Math.min(chatInput.scrollHeight, 150) + 'px';
            }
            return;
        }

        // Close sticker/gif picker if open
        const stickerPicker = chat.element.querySelector('.sticker-picker');
        if (stickerPicker) stickerPicker.remove();
        const gifPicker = chat.element.querySelector('.gif-picker');
        if (gifPicker) gifPicker.remove();
        
        // Collapse textarea when picker opens
        if (chatInput) {
            chatInput.classList.add('picker-open');
            chatInput.style.height = Math.min(chatInput.scrollHeight, 60) + 'px';
        }

        picker = document.createElement('div');
        picker.className = 'emoji-picker';
        picker.innerHTML = emojis.map(e => `<span class="emoji-item">${e}</span>`).join('');
        
        // Position above input area
        const inputArea = chat.element.querySelector('.chat-input-area');
        inputArea.appendChild(picker);

        // Handle emoji selection - insert at cursor position (keep picker open)
        picker.addEventListener('click', (e) => {
            if (e.target.classList.contains('emoji-item')) {
                const input = chat.element.querySelector('.chat-input');
                const emoji = e.target.textContent;
                // Insert at cursor position
                const cursorPos = input.selectionStart || input.value.length;
                const textBefore = input.value.substring(0, cursorPos);
                const textAfter = input.value.substring(input.selectionEnd || cursorPos);
                input.value = textBefore + emoji + textAfter;
                const newPos = cursorPos + emoji.length;
                input.setSelectionRange(newPos, newPos);
                input.dispatchEvent(new Event('input'));
                // Keep picker open for quick multiple selections
            }
        });

        // Close picker when clicking outside
        const chatInputRef = chat.element.querySelector('.chat-input');
        setTimeout(() => {
            document.addEventListener('click', function closePickerHandler(e) {
                if (!picker.contains(e.target) && e.target !== button) {
                    picker.remove();
                    // Restore textarea height
                    if (chatInputRef) {
                        chatInputRef.classList.remove('picker-open');
                        chatInputRef.style.height = 'auto';
                        chatInputRef.style.height = Math.min(chatInputRef.scrollHeight, 150) + 'px';
                    }
                    document.removeEventListener('click', closePickerHandler);
                }
            });
        }, 100);
    }

    sendLikeEmoji(chat) {
        // Send a thumbs up emoji as message
        const input = chat.element.querySelector('.chat-input');
        input.value = '👍';
        this.sendMessage(chat);
    }

    async handleFileUpload(chat, file) {
        if (!file || !chat.conversationId) return;

        // Validate file size (max 10MB)
        if (file.size > 10 * 1024 * 1024) {
            window.toastManager?.error('File size must be less than 10MB');
            return;
        }

        // Validate file type
        const allowedTypes = [
            'image/jpeg', 'image/png', 'image/gif', 'image/webp',
            'video/mp4', 'video/webm',
            'audio/mpeg', 'audio/mp3', 'audio/wav', 'audio/ogg', 'audio/webm', 'audio/m4a', 'audio/aac'
        ];
        if (!allowedTypes.includes(file.type) && !file.type.startsWith('audio/')) {
            window.toastManager?.error('Only images, videos and audio files are allowed');
            return;
        }

        try {
            // Show uploading indicator
            const messagesArea = chat.element.querySelector('.chat-messages-area');
            const uploadingEl = document.createElement('div');
            uploadingEl.className = 'chat-message me uploading';
            uploadingEl.innerHTML = `
                <div class="message-bubble">
                    <div class="upload-progress">
                        <svg class="animate-spin" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M21 12a9 9 0 1 1-6.219-8.56"></path>
                        </svg>
                        <span>Uploading...</span>
                    </div>
                </div>
            `;
            messagesArea.appendChild(uploadingEl);
            this.scrollToBottom(chat);

            // Create form data
            const formData = new FormData();
            formData.append('file', file);
            formData.append('conversationId', chat.conversationId);
            formData.append('recipientId', chat.userId);

            // Step 1: Upload file to get URL
            const uploadResponse = await fetch(`${this.apiUrl}/chat/attachments`, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${this.accessToken}`
                },
                body: formData
            });

            if (!uploadResponse.ok) {
                uploadingEl.remove();
                throw new Error('Failed to upload file');
            }

            const uploadResult = await uploadResponse.json();
            const attachmentData = uploadResult.data;

            // Step 2: Send message with attachment via SignalR
            if (this.signalRConnection && chat.conversationId) {
                await this.signalRConnection.invoke(
                    "SendMessageWithAttachmentAsync",
                    chat.conversationId,
                    chat.userId,
                    "", // Empty content for attachment-only message
                    null, // productId
                    attachmentData.url,
                    attachmentData.type,
                    attachmentData.publicId,
                    attachmentData.thumbnailUrl,
                    attachmentData.mimeType,
                    attachmentData.fileName,
                    attachmentData.fileSize
                );
            }

            // Remove uploading indicator
            uploadingEl.remove();
            
            // Add message to chat immediately (don't wait for SignalR echo)
            const tempMessage = {
                content: "",
                senderId: this.currentUserId,
                sentAt: new Date().toISOString(),
                attachment: {
                    url: attachmentData.url,
                    type: attachmentData.type,
                    fileName: attachmentData.fileName,
                    thumbnailUrl: attachmentData.thumbnailUrl,
                    mimeType: attachmentData.mimeType
                }
            };
            this.addMessageToChat(chat, tempMessage);
            this.scrollToBottom(chat);
        } catch (error) {
            console.error('Error uploading file:', error);
            window.toastManager?.error('Failed to send file');
        }
    }

    // ==================== VOICE RECORDING ====================
    startVoiceRecording(chat) {
        if (chat.isRecording) return;

        // Check browser support
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            window.toastManager?.error('Voice recording is not supported in your browser');
            return;
        }

        navigator.mediaDevices.getUserMedia({ audio: true })
            .then(stream => {
                chat.mediaRecorder = new MediaRecorder(stream);
                chat.audioChunks = [];
                chat.recordingStartTime = Date.now();
                chat.isRecording = true;

                chat.mediaRecorder.ondataavailable = (e) => {
                    chat.audioChunks.push(e.data);
                };

                chat.mediaRecorder.onstop = () => {
                    const audioBlob = new Blob(chat.audioChunks, { type: 'audio/webm' });
                    chat.recordedAudioBlob = audioBlob;
                    chat.recordedAudioUrl = URL.createObjectURL(audioBlob);
                    stream.getTracks().forEach(track => track.stop());
                };

                chat.mediaRecorder.start();
                this.showRecordingUI(chat);

                // Auto stop after 60 seconds
                chat.recordingTimeout = setTimeout(() => {
                    if (chat.isRecording) {
                        this.stopVoiceRecording(chat);
                    }
                }, 60000);

                // Update timer every second
                chat.recordingInterval = setInterval(() => {
                    this.updateRecordingTimer(chat);
                }, 1000);
            })
            .catch(err => {
                console.error('Error accessing microphone:', err);
                window.toastManager?.error('Could not access microphone. Please allow microphone permission.');
            });
    }

    showRecordingUI(chat) {
        const inputArea = chat.element.querySelector('.chat-input-area');
        inputArea.innerHTML = `
            <div class="voice-recording-ui">
                <button type="button" class="recording-cancel-btn" title="Cancel">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/>
                    </svg>
                </button>
                <button type="button" class="recording-stop-btn" title="Stop">
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="currentColor">
                        <rect x="6" y="6" width="12" height="12" rx="2"/>
                    </svg>
                </button>
                <div class="recording-timer">0:00</div>
                <button type="button" class="recording-send-btn" title="Send" style="display:none">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/>
                    </svg>
                </button>
            </div>
        `;

        // Event listeners
        inputArea.querySelector('.recording-cancel-btn').addEventListener('click', () => {
            this.cancelVoiceRecording(chat);
        });

        inputArea.querySelector('.recording-stop-btn').addEventListener('click', () => {
            this.stopVoiceRecording(chat);
        });

        inputArea.querySelector('.recording-send-btn').addEventListener('click', () => {
            this.sendVoiceMessage(chat);
        });
    }

    updateRecordingTimer(chat) {
        const elapsed = Math.floor((Date.now() - chat.recordingStartTime) / 1000);
        const minutes = Math.floor(elapsed / 60);
        const seconds = elapsed % 60;
        const timerEl = chat.element.querySelector('.recording-timer');
        if (timerEl) {
            timerEl.textContent = `${minutes}:${seconds.toString().padStart(2, '0')}`;
        }
    }

    stopVoiceRecording(chat) {
        if (!chat.isRecording || !chat.mediaRecorder) return;

        clearTimeout(chat.recordingTimeout);
        clearInterval(chat.recordingInterval);
        chat.mediaRecorder.stop();
        chat.isRecording = false;

        // Show preview UI
        setTimeout(() => {
            this.showVoicePreviewUI(chat);
        }, 100);
    }

    showVoicePreviewUI(chat) {
        const inputArea = chat.element.querySelector('.chat-input-area');
        const totalDuration = Math.floor((Date.now() - chat.recordingStartTime) / 1000);
        const barCount = 20;

        inputArea.innerHTML = `
            <div class="voice-preview-ui">
                <button type="button" class="preview-cancel-btn" title="Cancel">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/>
                    </svg>
                </button>
                <button type="button" class="preview-play-btn" title="Play">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M8 5v14l11-7z"/>
                    </svg>
                </button>
                <div class="voice-waveform">
                    <div class="waveform-bars">
                        ${this.generateWaveformBars(barCount)}
                    </div>
                </div>
                <div class="preview-duration">${this.formatDuration(totalDuration)}</div>
                <button type="button" class="preview-send-btn" title="Send">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/>
                    </svg>
                </button>
            </div>
            <audio class="voice-preview-audio" src="${chat.recordedAudioUrl}" style="display:none"></audio>
        `;

        const audio = inputArea.querySelector('.voice-preview-audio');
        const playBtn = inputArea.querySelector('.preview-play-btn');
        const durationEl = inputArea.querySelector('.preview-duration');
        const waveformBars = inputArea.querySelectorAll('.waveform-bar');
        let isPlaying = false;
        let previewInterval = null;

        const updatePreviewUI = () => {
            if (audio && durationEl) {
                // Update remaining time (countdown)
                const remaining = Math.max(0, totalDuration - Math.floor(audio.currentTime));
                durationEl.textContent = this.formatDuration(remaining);
                
                // Update waveform progress
                const progress = audio.currentTime / totalDuration;
                const activeBarCount = Math.floor(progress * barCount);
                waveformBars.forEach((bar, index) => {
                    if (index < activeBarCount) {
                        bar.classList.add('active');
                    } else {
                        bar.classList.remove('active');
                    }
                });
            }
        };

        playBtn.addEventListener('click', () => {
            if (isPlaying) {
                audio.pause();
                clearInterval(previewInterval);
                playBtn.innerHTML = `<svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M8 5v14l11-7z"/></svg>`;
            } else {
                audio.play();
                previewInterval = setInterval(updatePreviewUI, 50);
                playBtn.innerHTML = `<svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><rect x="6" y="4" width="4" height="16"/><rect x="14" y="4" width="4" height="16"/></svg>`;
            }
            isPlaying = !isPlaying;
        });

        audio.onended = () => {
            isPlaying = false;
            clearInterval(previewInterval);
            audio.currentTime = 0;
            durationEl.textContent = this.formatDuration(totalDuration);
            waveformBars.forEach(bar => bar.classList.remove('active'));
            playBtn.innerHTML = `<svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M8 5v14l11-7z"/></svg>`;
        };

        inputArea.querySelector('.preview-cancel-btn').addEventListener('click', () => {
            clearInterval(previewInterval);
            this.cancelVoiceRecording(chat);
        });

        inputArea.querySelector('.preview-send-btn').addEventListener('click', () => {
            clearInterval(previewInterval);
            this.sendVoiceMessage(chat);
        });
    }
    
    formatDuration(seconds) {
        const mins = Math.floor(seconds / 60);
        const secs = seconds % 60;
        return `${mins}:${secs.toString().padStart(2, '0')}`;
    }

    generateWaveformBars(count = 20) {
        let bars = '';
        for (let i = 0; i < count; i++) {
            const height = Math.random() * 60 + 20;
            bars += `<div class="waveform-bar" style="height: ${height}%"></div>`;
        }
        return bars;
    }

    cancelVoiceRecording(chat) {
        if (chat.isRecording && chat.mediaRecorder) {
            chat.mediaRecorder.stop();
        }
        clearTimeout(chat.recordingTimeout);
        clearInterval(chat.recordingInterval);
        chat.isRecording = false;
        chat.recordedAudioBlob = null;
        chat.recordedAudioUrl = null;
        this.restoreInputUI(chat);
    }

    async sendVoiceMessage(chat) {
        // Prevent duplicate sends
        if (!chat.recordedAudioBlob || !chat.conversationId || chat.isSendingVoice) return;
        
        // Mark as sending and store blob before clearing
        chat.isSendingVoice = true;
        const audioBlob = chat.recordedAudioBlob;
        
        // Clear recorded data immediately to prevent re-send
        chat.recordedAudioBlob = null;
        chat.recordedAudioUrl = null;
        
        // Restore input UI immediately (hide preview)
        this.restoreInputUI(chat);

        try {
            // Show sending indicator
            const messagesArea = chat.element.querySelector('.chat-messages-area');
            const sendingEl = document.createElement('div');
            sendingEl.className = 'chat-message me uploading';
            sendingEl.innerHTML = `
                <div class="message-bubble">
                    <div class="upload-progress">
                        <svg class="animate-spin" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M21 12a9 9 0 1 1-6.219-8.56"></path>
                        </svg>
                        <span>Sending...</span>
                    </div>
                </div>
            `;
            messagesArea.appendChild(sendingEl);
            this.scrollToBottom(chat);

            // Upload audio file
            const formData = new FormData();
            const audioFile = new File([audioBlob], `voice_${Date.now()}.webm`, { type: 'audio/webm' });
            formData.append('file', audioFile);

            const uploadResponse = await fetch(`${this.apiUrl}/chat/attachments`, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${this.accessToken}`
                },
                body: formData
            });

            if (!uploadResponse.ok) {
                sendingEl.remove();
                throw new Error('Failed to upload voice message');
            }

            const uploadResult = await uploadResponse.json();
            const attachmentData = uploadResult.data;

            // Send via SignalR
            if (this.signalRConnection) {
                await this.signalRConnection.invoke(
                    "SendMessageWithAttachmentAsync",
                    chat.conversationId,
                    chat.userId,
                    "",
                    null, // productId
                    attachmentData.url,
                    "audio",
                    attachmentData.publicId,
                    attachmentData.thumbnailUrl,
                    attachmentData.mimeType,
                    attachmentData.fileName,
                    attachmentData.fileSize
                );
            }

            sendingEl.remove();
            
            // Add voice message to chat immediately (don't wait for SignalR echo)
            const tempMessage = {
                content: "",
                senderId: this.currentUserId,
                sentAt: new Date().toISOString(),
                attachment: {
                    url: attachmentData.url,
                    type: "audio",
                    fileName: attachmentData.fileName,
                    thumbnailUrl: attachmentData.thumbnailUrl,
                    mimeType: attachmentData.mimeType
                }
            };
            this.addMessageToChat(chat, tempMessage);
            this.scrollToBottom(chat);
        } catch (error) {
            console.error('Error sending voice message:', error);
            window.toastManager?.error('Failed to send voice message');
        } finally {
            // Reset sending flag
            chat.isSendingVoice = false;
        }
    }

    restoreInputUI(chat) {
        const inputArea = chat.element.querySelector('.chat-input-area');
        inputArea.innerHTML = `
            <div class="chat-media-actions">
                <button type="button" class="chat-media-btn" data-action="voice" title="Voice message">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M12 14c1.66 0 3-1.34 3-3V5c0-1.66-1.34-3-3-3S9 3.34 9 5v6c0 1.66 1.34 3 3 3zm-1-9c0-.55.45-1 1-1s1 .45 1 1v6c0 .55-.45 1-1 1s-1-.45-1-1V5z"/>
                        <path d="M17 11c0 2.76-2.24 5-5 5s-5-2.24-5-5H5c0 3.53 2.61 6.43 6 6.92V21h2v-3.08c3.39-.49 6-3.39 6-6.92h-2z"/>
                    </svg>
                </button>
                <button type="button" class="chat-media-btn" data-action="image" title="Send image/video">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H5V5h14v14zm-5-7l-3 3.72L9 13l-3 4h12l-4-5z"/>
                    </svg>
                </button>
                <button type="button" class="chat-media-btn" data-action="sticker" title="Stickers">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm3.5-9c.83 0 1.5-.67 1.5-1.5S16.33 8 15.5 8 14 8.67 14 9.5s.67 1.5 1.5 1.5zm-7 0c.83 0 1.5-.67 1.5-1.5S9.33 8 8.5 8 7 8.67 7 9.5 7.67 11 8.5 11zm3.5 6.5c2.33 0 4.31-1.46 5.11-3.5H6.89c.8 2.04 2.78 3.5 5.11 3.5z"/>
                    </svg>
                </button>
                <button type="button" class="chat-media-btn" data-action="gif" title="Send GIF">
                    <span class="gif-text">GIF</span>
                </button>
                <button type="button" class="chat-media-btn" data-action="product" title="Attach product">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M18 6h-2c0-2.21-1.79-4-4-4S8 3.79 8 6H6c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm-6-2c1.1 0 2 .9 2 2h-4c0-1.1.9-2 2-2zm6 16H6V8h2v2c0 .55.45 1 1 1s1-.45 1-1V8h4v2c0 .55.45 1 1 1s1-.45 1-1V8h2v12z"/>
                    </svg>
                </button>
            </div>
            <form class="chat-input-form">
                <textarea class="chat-input" placeholder="Aa" rows="1"></textarea>
                <button type="button" class="chat-emoji-btn" data-action="emoji" title="Emoji">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm3.5-9c.83 0 1.5-.67 1.5-1.5S16.33 8 15.5 8 14 8.67 14 9.5s.67 1.5 1.5 1.5zm-7 0c.83 0 1.5-.67 1.5-1.5S9.33 8 8.5 8 7 8.67 7 9.5 7.67 11 8.5 11zm3.5 6.5c2.33 0 4.31-1.46 5.11-3.5H6.89c.8 2.04 2.78 3.5 5.11 3.5z"/>
                    </svg>
                </button>
                <button type="submit" class="chat-send-btn" title="Send">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/>
                    </svg>
                </button>
                <button type="button" class="chat-like-btn" title="Like">
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M2 20h2c.55 0 1-.45 1-1v-9c0-.55-.45-1-1-1H2v11zm19.83-7.12c.11-.25.17-.52.17-.8V11c0-1.1-.9-2-2-2h-5.5l.92-4.65c.05-.22.02-.46-.08-.66-.23-.45-.52-.86-.88-1.22L14 2 7.59 8.41C7.21 8.79 7 9.3 7 9.83v7.84C7 18.95 8.05 20 9.34 20h8.11c.7 0 1.36-.37 1.72-.97l2.66-6.15z"/>
                    </svg>
                </button>
            </form>
            <input type="file" class="chat-file-input" accept="image/*,video/*,audio/*" style="display:none">
        `;

        // Re-attach event listeners
        this.attachInputEventListeners(chat);
    }

    attachInputEventListeners(chat) {
        const inputArea = chat.element.querySelector('.chat-input-area');
        const form = inputArea.querySelector('.chat-input-form');
        const mediaButtons = inputArea.querySelectorAll('.chat-media-btn');
        const fileInput = inputArea.querySelector('.chat-file-input');
        const emojiBtn = inputArea.querySelector('.chat-emoji-btn');
        const likeBtn = inputArea.querySelector('.chat-like-btn');
        const chatInput = inputArea.querySelector('.chat-input');
        const sendBtn = form.querySelector('.chat-send-btn');

        form.addEventListener('submit', (e) => {
            e.preventDefault();
            this.sendMessage(chat);
        });

        mediaButtons.forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                const action = btn.dataset.action;
                this.handleMediaAction(chat, action, fileInput);
            });
        });

        if (emojiBtn) {
            emojiBtn.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                this.toggleEmojiPicker(chat, emojiBtn);
            });
        }

        if (likeBtn) {
            likeBtn.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                this.sendLikeEmoji(chat);
            });
        }

        if (chatInput && likeBtn) {
            const maxHeight = 150;
            
            chatInput.addEventListener('input', () => {
                // Auto-resize textarea
                chatInput.style.height = 'auto';
                const newHeight = Math.min(chatInput.scrollHeight, maxHeight);
                chatInput.style.height = newHeight + 'px';
                
                // Show scrollbar only when content exceeds max height
                if (chatInput.scrollHeight > maxHeight) {
                    chatInput.classList.add('has-scroll');
                } else {
                    chatInput.classList.remove('has-scroll');
                }
                
                // Toggle send/like button
                if (chatInput.value.trim()) {
                    likeBtn.style.display = 'none';
                    if (sendBtn) {
                        sendBtn.style.display = 'flex';
                        sendBtn.style.alignItems = 'center';
                        sendBtn.style.justifyContent = 'center';
                    }
                } else {
                    likeBtn.style.display = 'flex';
                    if (sendBtn) sendBtn.style.display = 'none';
                }
            });
            
            // Handle Enter key to send, Shift+Enter for new line
            chatInput.addEventListener('keydown', (e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    if (chatInput.value.trim()) {
                        form.dispatchEvent(new Event('submit'));
                    }
                }
            });
        }

        if (fileInput) {
            fileInput.addEventListener('change', (e) => {
                if (e.target.files && e.target.files[0]) {
                    this.handleFileUpload(chat, e.target.files[0]);
                    e.target.value = '';
                }
            });
        }
    }

    // ==================== STICKERS ====================
    getStickerPacks() {
        if (!this._stickerPacks) {
            this._stickerPacks = {
                recent: { name: '🕐 Recent', icon: '🕐', stickers: [] },
                smileys: { name: '😀 Smileys', icon: '😀', stickers: ['😀', '😃', '😄', '😁', '😆', '😅', '🤣', '😂', '🙂', '😊', '😇', '🥰', '😍', '🤩', '😘', '😗', '😚', '😙', '🥲', '😋', '😛', '😜', '🤪', '😝', '🤑', '🤗', '🤭', '🤫', '🤔', '🤐', '🤨', '😐', '😑', '😶', '😏', '😒', '🙄', '😬', '🤥', '😌', '😔', '😪', '🤤', '😴', '😷', '🤒', '🤕', '🤢', '🤮', '🤧', '🥵', '🥶', '🥴', '😵', '🤯', '🤠', '🥳', '🥸', '😎', '🤓', '🧐'] },
                gestures: { name: '👋 Gestures', icon: '👋', stickers: ['👋', '🤚', '🖐️', '✋', '🖖', '👌', '🤌', '🤏', '✌️', '🤞', '🤟', '🤘', '🤙', '👈', '👉', '👆', '🖕', '👇', '☝️', '👍', '👎', '✊', '👊', '🤛', '🤜', '👏', '🙌', '👐', '🤲', '🤝', '🙏', '✍️', '💅', '🤳', '💪', '🦾', '🦿', '🦵', '🦶', '👂', '🦻', '👃', '🧠', '🫀', '🫁', '🦷', '🦴', '👀', '👁️', '👅', '👄'] },
                hearts: { name: '❤️ Hearts', icon: '❤️', stickers: ['❤️', '🧡', '💛', '💚', '💙', '💜', '🖤', '🤍', '🤎', '💔', '❣️', '💕', '💞', '💓', '💗', '💖', '💘', '💝', '💟', '♥️', '💌', '💋', '😻', '😽', '🫶', '🥰', '😍', '🤩', '😘', '💑', '👩‍❤️‍👨', '👨‍❤️‍👨', '👩‍❤️‍👩', '💏'] },
                animals: { name: '🐶 Animals', icon: '🐶', stickers: ['🐶', '🐱', '🐭', '🐹', '🐰', '🦊', '🐻', '🐼', '🐻‍❄️', '🐨', '🐯', '🦁', '🐮', '🐷', '🐸', '🐵', '🙈', '🙉', '🙊', '🐔', '🐧', '🐦', '🐤', '🦆', '🦅', '🦉', '🦇', '🐺', '🐗', '🐴', '🦄', '🐝', '🪱', '🐛', '🦋', '🐌', '🐞', '🐜', '🪰', '🪲', '🪳', '🦟', '🦗', '🕷️', '🦂', '🐢', '🐍', '🦎', '🦖', '🦕', '🐙', '🦑', '🦐', '🦞', '🦀', '🐡', '🐠', '🐟', '🐬', '🐳', '🐋', '🦈', '🐊'] },
                food: { name: '🍔 Food', icon: '🍔', stickers: ['🍎', '🍐', '🍊', '🍋', '🍌', '🍉', '🍇', '🍓', '🫐', '🍈', '🍒', '🍑', '🥭', '🍍', '🥥', '🥝', '🍅', '🍆', '🥑', '🥦', '🥬', '🥒', '🌶️', '🫑', '🌽', '🥕', '🫒', '🧄', '🧅', '🥔', '🍠', '🥐', '🥯', '🍞', '🥖', '🥨', '🧀', '🥚', '🍳', '🧈', '🥞', '🧇', '🥓', '🥩', '🍗', '🍖', '🦴', '🌭', '🍔', '🍟', '🍕', '🫓', '🥪', '🥙', '🧆', '🌮', '🌯', '🫔', '🥗', '🥘', '🫕', '🍝', '🍜', '🍲', '🍛', '🍣', '🍱', '🥟', '🦪', '🍤', '🍙', '🍚', '🍘', '🍥', '🥠', '🥮', '🍢', '🍡', '🍧', '🍨', '🍦', '🥧', '🧁', '🍰', '🎂', '🍮', '🍭', '🍬', '🍫', '🍿', '🍩', '🍪', '🌰', '🥜', '🍯', '🥛', '🍼', '🫖', '☕', '🍵', '🧃', '🥤', '🧋', '🍶', '🍺', '🍻', '🥂', '🍷', '🥃', '🍸', '🍹', '🧉', '🍾'] },
                activities: { name: '⚽ Activities', icon: '⚽', stickers: ['⚽', '🏀', '🏈', '⚾', '🥎', '🎾', '🏐', '🏉', '🥏', '🎱', '🪀', '🏓', '🏸', '🏒', '🏑', '🥍', '🏏', '🪃', '🥅', '⛳', '🪁', '🏹', '🎣', '🤿', '🥊', '🥋', '🎽', '🛹', '🛼', '🛷', '⛸️', '🥌', '🎿', '⛷️', '🏂', '🪂', '🏋️', '🤼', '🤸', '⛹️', '🤺', '🤾', '🏌️', '🏇', '⛑️', '🧘', '🏄', '🏊', '🤽', '🚣', '🧗', '🚵', '🚴', '🏆', '🥇', '🥈', '🥉', '🏅', '🎖️', '🏵️', '🎗️', '🎫', '🎟️', '🎪', '🎭', '🎨', '🎬', '🎤', '🎧', '🎼', '🎹', '🥁', '🪘', '🎷', '🎺', '🪗', '🎸', '🪕', '🎻', '🎲', '♟️', '🎯', '🎳', '🎮', '🎰', '🧩'] },
                objects: { name: '💡 Objects', icon: '💡', stickers: ['⌚', '📱', '💻', '🖥️', '🖨️', '🖱️', '💽', '💾', '💿', '📷', '📹', '🎥', '📞', '📺', '📻', '⏰', '🔋', '🔌', '💡', '🔦', '💸', '💵', '💰', '💳', '💎', '🔧', '🔨', '🔩', '⚙️', '🔫', '💣', '🔪', '🛡️', '🔮', '💊', '💉', '🧬', '🔬', '🔭', '🧹', '🧺', '🧻', '🚽', '🛁', '🧼', '🔑', '🚪', '🛋️', '🛏️', '🧸', '🎁', '🎈', '🎀', '🎊', '🎉', '✉️', '📦', '📜', '📄', '📊', '📅', '📁', '📰', '📚', '📖', '🔖', '📎', '✂️', '📝', '✏️', '🔍', '🔒'] },
                symbols: { name: '💯 Symbols', icon: '💯', stickers: ['💯', '✅', '❌', '❓', '❗', '💢', '💥', '💫', '💦', '💨', '🔴', '🟠', '🟡', '🟢', '🔵', '🟣', '⚫', '⚪', '🔶', '🔷', '🔸', '🔹', '▶️', '⏸️', '⏹️', '⏺️', '⏭️', '⏮️', '🔀', '🔁', '🔂', '➕', '➖', '➗', '✖️', '♾️', '💲', '🔃', '🔄', '🔙', '🔚', '🔛', '🔜', '🔝', '🏁', '🚩', '🎌', '🏴', '🏳️'] }
            };
        }
        return this._stickerPacks;
    }

    toggleStickerPicker(chat) {
        const chatInput = chat.element.querySelector('.chat-input');
        let picker = chat.element.querySelector('.sticker-picker');
        if (picker) {
            picker.remove();
            // Restore textarea height
            if (chatInput) {
                chatInput.classList.remove('picker-open');
                chatInput.style.height = 'auto';
                chatInput.style.height = Math.min(chatInput.scrollHeight, 150) + 'px';
            }
            return;
        }

        // Close other pickers if open
        const gifPicker = chat.element.querySelector('.gif-picker');
        if (gifPicker) gifPicker.remove();
        const emojiPicker = chat.element.querySelector('.emoji-picker');
        if (emojiPicker) emojiPicker.remove();
        
        // Collapse textarea when picker opens
        if (chatInput) {
            chatInput.classList.add('picker-open');
            chatInput.style.height = Math.min(chatInput.scrollHeight, 60) + 'px';
        }

        // Load recent stickers from localStorage
        this.loadRecentStickers();
        
        const stickerPacks = this.getStickerPacks();

        picker = document.createElement('div');
        picker.className = 'sticker-picker';
        picker.innerHTML = `
            <div class="sticker-search-container">
                <input type="text" class="sticker-search-input" placeholder="Search stickers..." autocomplete="off">
            </div>
            <div class="sticker-tabs">
                ${Object.keys(stickerPacks).map(key => 
                    `<button type="button" class="sticker-tab ${key === 'recent' ? 'active' : ''}" data-pack="${key}" title="${stickerPacks[key].name}">${stickerPacks[key].icon}</button>`
                ).join('')}
            </div>
            <div class="sticker-content">
                <div class="sticker-grid"></div>
            </div>
        `;

        const inputArea = chat.element.querySelector('.chat-input-area');
        inputArea.appendChild(picker);

        const searchInput = picker.querySelector('.sticker-search-input');
        const tabs = picker.querySelectorAll('.sticker-tab');
        const grid = picker.querySelector('.sticker-grid');
        let currentPack = 'recent';

        // Render stickers for current pack
        const renderStickers = (packKey, searchQuery = '') => {
            let stickers = stickerPacks[packKey]?.stickers || [];
            
            if (searchQuery) {
                // Search across all packs
                stickers = [];
                Object.values(stickerPacks).forEach(pack => {
                    stickers.push(...pack.stickers.filter(s => s.includes(searchQuery)));
                });
                stickers = [...new Set(stickers)]; // Remove duplicates
            }
            
            if (stickers.length === 0) {
                grid.innerHTML = '<div class="sticker-empty">No stickers found</div>';
            } else {
                grid.innerHTML = stickers.map(s => `<span class="sticker-item">${s}</span>`).join('');
            }
        };

        // Initial render
        renderStickers(currentPack);

        // Tab click handler
        tabs.forEach(tab => {
            tab.addEventListener('click', (e) => {
                e.stopPropagation();
                tabs.forEach(t => t.classList.remove('active'));
                tab.classList.add('active');
                currentPack = tab.dataset.pack;
                searchInput.value = '';
                renderStickers(currentPack);
            });
        });

        // Search handler
        let searchTimeout;
        searchInput.addEventListener('input', (e) => {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(() => {
                const query = e.target.value.trim();
                if (query) {
                    tabs.forEach(t => t.classList.remove('active'));
                    renderStickers(null, query);
                } else {
                    tabs.forEach(t => t.classList.toggle('active', t.dataset.pack === currentPack));
                    renderStickers(currentPack);
                }
            }, 200);
        });

        searchInput.addEventListener('click', (e) => e.stopPropagation());

        // Sticker click handler - insert into input instead of sending (keep picker open)
        grid.addEventListener('click', (e) => {
            if (e.target.classList.contains('sticker-item')) {
                const sticker = e.target.textContent;
                this.addRecentSticker(sticker);
                const input = chat.element.querySelector('.chat-input');
                // Insert sticker at cursor position or append
                const cursorPos = input.selectionStart || input.value.length;
                const textBefore = input.value.substring(0, cursorPos);
                const textAfter = input.value.substring(input.selectionEnd || cursorPos);
                input.value = textBefore + sticker + textAfter;
                // Set cursor after inserted sticker
                const newPos = cursorPos + sticker.length;
                input.setSelectionRange(newPos, newPos);
                // Trigger input event to update UI (show send button, resize)
                input.dispatchEvent(new Event('input'));
                // Keep picker open for quick multiple selections
            }
        });

        // Focus search
        setTimeout(() => searchInput.focus(), 100);

        // Close on outside click
        const chatInputRef = chat.element.querySelector('.chat-input');
        setTimeout(() => {
            document.addEventListener('click', function closeStickerHandler(e) {
                if (!picker.contains(e.target) && !e.target.closest('[data-action="sticker"]')) {
                    picker.remove();
                    // Restore textarea height
                    if (chatInputRef) {
                        chatInputRef.classList.remove('picker-open');
                        chatInputRef.style.height = 'auto';
                        chatInputRef.style.height = Math.min(chatInputRef.scrollHeight, 150) + 'px';
                    }
                    document.removeEventListener('click', closeStickerHandler);
                }
            });
        }, 100);
    }

    loadRecentStickers() {
        try {
            const recent = localStorage.getItem('recentStickers');
            if (recent) {
                this.getStickerPacks().recent.stickers = JSON.parse(recent);
            }
        } catch (e) {
            console.error('Error loading recent stickers:', e);
        }
    }

    addRecentSticker(sticker) {
        try {
            const stickerPacks = this.getStickerPacks();
            let recent = stickerPacks.recent.stickers;
            // Remove if exists, add to front
            recent = recent.filter(s => s !== sticker);
            recent.unshift(sticker);
            // Keep only last 24
            recent = recent.slice(0, 24);
            stickerPacks.recent.stickers = recent;
            localStorage.setItem('recentStickers', JSON.stringify(recent));
        } catch (e) {
            console.error('Error saving recent sticker:', e);
        }
    }

    // ==================== GIF PICKER ====================
    toggleGifPicker(chat) {
        const chatInput = chat.element.querySelector('.chat-input');
        let picker = chat.element.querySelector('.gif-picker');
        if (picker) {
            picker.remove();
            // Restore textarea height
            if (chatInput) {
                chatInput.classList.remove('picker-open');
                chatInput.style.height = 'auto';
                chatInput.style.height = Math.min(chatInput.scrollHeight, 150) + 'px';
            }
            return;
        }

        // Close other pickers if open
        const stickerPicker = chat.element.querySelector('.sticker-picker');
        if (stickerPicker) stickerPicker.remove();
        const emojiPicker = chat.element.querySelector('.emoji-picker');
        if (emojiPicker) emojiPicker.remove();
        
        // Collapse textarea when picker opens
        if (chatInput) {
            chatInput.classList.add('picker-open');
            chatInput.style.height = Math.min(chatInput.scrollHeight, 60) + 'px';
        }

        picker = document.createElement('div');
        picker.className = 'gif-picker';
        picker.innerHTML = `
            <div class="gif-search-container">
                <input type="text" class="gif-search-input" placeholder="Search GIFs..." autocomplete="off">
            </div>
            <div class="gif-grid-container">
                <div class="gif-grid"></div>
            </div>
        `;

        const inputArea = chat.element.querySelector('.chat-input-area');
        inputArea.appendChild(picker);

        const searchInput = picker.querySelector('.gif-search-input');
        const gridContainer = picker.querySelector('.gif-grid-container');
        let searchTimeout = null;

        // Load trending GIFs initially
        this.loadGifs(chat, picker, '');

        // Search handler with debounce
        searchInput.addEventListener('input', (e) => {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(() => {
                this.loadGifs(chat, picker, e.target.value.trim());
            }, 300);
        });

        // Prevent picker from closing when clicking inside
        searchInput.addEventListener('click', (e) => {
            e.stopPropagation();
        });

        // Handle GIF selection
        gridContainer.addEventListener('click', async (e) => {
            if (e.target.classList.contains('gif-item')) {
                const gifUrl = e.target.dataset.url || e.target.src;
                picker.remove();
                // Restore textarea height
                if (chatInput) {
                    chatInput.classList.remove('picker-open');
                    chatInput.style.height = 'auto';
                    chatInput.style.height = Math.min(chatInput.scrollHeight, 150) + 'px';
                }
                await this.sendGifMessage(chat, gifUrl);
            }
        });

        // Focus search input
        setTimeout(() => searchInput.focus(), 100);

        // Close on outside click
        const chatInputRef = chat.element.querySelector('.chat-input');
        setTimeout(() => {
            document.addEventListener('click', function closeGifHandler(e) {
                if (!picker.contains(e.target) && !e.target.closest('[data-action="gif"]')) {
                    picker.remove();
                    // Restore textarea height
                    if (chatInputRef) {
                        chatInputRef.classList.remove('picker-open');
                        chatInputRef.style.height = 'auto';
                        chatInputRef.style.height = Math.min(chatInputRef.scrollHeight, 150) + 'px';
                    }
                    document.removeEventListener('click', closeGifHandler);
                }
            });
        }, 100);
    }

    async loadGifs(chat, picker, query) {
        const gridContainer = picker.querySelector('.gif-grid-container');
        const grid = picker.querySelector('.gif-grid');
        
        // Show loading
        grid.innerHTML = `
            <div class="gif-loading" style="grid-column: span 2;">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M21 12a9 9 0 1 1-6.219-8.56"></path>
                </svg>
                <div>Loading GIFs...</div>
            </div>
        `;

        try {
            // Giphy API - using public beta key (for production, use your own API key)
            const apiKey = 'Gc7131jiJuvI7IdN0HZ1D7nh0ow5BU6g'; // Giphy public beta key
            const limit = 20;
            let url;
            
            if (query) {
                url = `https://api.giphy.com/v1/gifs/search?api_key=${apiKey}&q=${encodeURIComponent(query)}&limit=${limit}&rating=g`;
            } else {
                url = `https://api.giphy.com/v1/gifs/trending?api_key=${apiKey}&limit=${limit}&rating=g`;
            }

            const response = await fetch(url);
            const data = await response.json();

            if (data.data && data.data.length > 0) {
                grid.innerHTML = data.data.map(gif => {
                    const previewUrl = gif.images.fixed_height_small?.url || gif.images.fixed_height?.url;
                    const fullUrl = gif.images.original?.url || gif.images.fixed_height?.url;
                    return `<img class="gif-item" src="${previewUrl}" data-url="${fullUrl}" alt="${gif.title || 'GIF'}" loading="lazy">`;
                }).join('');
            } else {
                grid.innerHTML = `
                    <div class="gif-empty" style="grid-column: span 2;">
                        No GIFs found. Try another search.
                    </div>
                `;
            }
        } catch (error) {
            console.error('Error loading GIFs:', error);
            // Fallback to static GIFs
            const fallbackGifs = [
                { url: 'https://media.giphy.com/media/3o7TKSjRrfIPjeiVyM/giphy.gif', alt: 'thumbs up' },
                { url: 'https://media.giphy.com/media/l0MYt5jPR6QX5pnqM/giphy.gif', alt: 'clapping' },
                { url: 'https://media.giphy.com/media/3oEjI6SIIHBdRxXI40/giphy.gif', alt: 'laughing' },
                { url: 'https://media.giphy.com/media/l4FGuhL4U2WyjdkaY/giphy.gif', alt: 'love' },
                { url: 'https://media.giphy.com/media/26u4cqiYI30juCOGY/giphy.gif', alt: 'wow' },
                { url: 'https://media.giphy.com/media/3o7TKMt1VVNkHV2PaE/giphy.gif', alt: 'sad' },
                { url: 'https://media.giphy.com/media/l0HlvtIPzPdt2usKs/giphy.gif', alt: 'party' },
                { url: 'https://media.giphy.com/media/xT9IgG50Fb7Mi0prBC/giphy.gif', alt: 'fire' }
            ];
            grid.innerHTML = fallbackGifs.map(g => 
                `<img class="gif-item" src="${g.url}" data-url="${g.url}" alt="${g.alt}" loading="lazy">`
            ).join('');
        }
    }

    async sendGifMessage(chat, gifUrl) {
        if (!chat.conversationId || !this.signalRConnection) return;

        try {
            await this.signalRConnection.invoke(
                "SendMessageWithAttachmentAsync",
                chat.conversationId,
                chat.userId,
                "",
                null, // productId
                gifUrl,
                "image", // type
                null, // publicId
                null, // thumbnailUrl
                "image/gif", // mimeType
                "gif", // fileName
                null // fileSize
            );
            
            // Add GIF message to chat immediately (don't wait for SignalR echo)
            const tempMessage = {
                content: "",
                senderId: this.currentUserId,
                sentAt: new Date().toISOString(),
                attachment: {
                    url: gifUrl,
                    type: "image",
                    fileName: "gif",
                    mimeType: "image/gif"
                }
            };
            this.addMessageToChat(chat, tempMessage);
            this.scrollToBottom(chat);
        } catch (error) {
            console.error('Error sending GIF:', error);
            window.toastManager?.error('Failed to send GIF');
        }
    }

    // ==================== PRODUCT PICKER ====================
    toggleProductPicker(chat) {
        const chatInput = chat.element.querySelector('.chat-input');
        let picker = chat.element.querySelector('.product-picker');
        if (picker) {
            picker.remove();
            if (chatInput) {
                chatInput.classList.remove('picker-open');
                chatInput.style.height = 'auto';
                chatInput.style.height = Math.min(chatInput.scrollHeight, 150) + 'px';
            }
            return;
        }

        // Close other pickers if open
        const stickerPicker = chat.element.querySelector('.sticker-picker');
        if (stickerPicker) stickerPicker.remove();
        const gifPicker = chat.element.querySelector('.gif-picker');
        if (gifPicker) gifPicker.remove();
        const emojiPicker = chat.element.querySelector('.emoji-picker');
        if (emojiPicker) emojiPicker.remove();

        // Collapse textarea when picker opens
        if (chatInput) {
            chatInput.classList.add('picker-open');
            chatInput.style.height = Math.min(chatInput.scrollHeight, 60) + 'px';
        }

        // Provider always has tabs to choose between own products and all products
        const currentRole = this.currentUserRole?.toLowerCase();
        const showTabs = currentRole === 'provider';

        picker = document.createElement('div');
        picker.className = 'product-picker';
        picker.innerHTML = `
            <div class="product-picker-header">
                <span class="product-picker-title">${showTabs ? 'My Products' : 'Select a Product'}</span>
                ${showTabs ? `
                <button type="button" class="product-picker-toggle" data-source="my" title="Switch to All Products">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M16 3h5v5M4 20L21 3M21 16v5h-5M15 15l6 6M4 4l5 5"/>
                    </svg>
                </button>
                ` : ''}
                <button type="button" class="product-picker-close" title="Close">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <line x1="18" y1="6" x2="6" y2="18"></line>
                        <line x1="6" y1="6" x2="18" y2="18"></line>
                    </svg>
                </button>
            </div>
            <div class="product-picker-search">
                <input type="text" class="product-picker-search-input" placeholder="Search products..." autocomplete="off">
            </div>
            <div class="product-picker-content">
                <div class="product-picker-list">
                    <div class="product-picker-loading">
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M21 12a9 9 0 1 1-6.219-8.56"></path>
                        </svg>
                        <div>Loading products...</div>
                    </div>
                </div>
            </div>
        `;

        const inputArea = chat.element.querySelector('.chat-input-area');
        inputArea.appendChild(picker);

        const searchInput = picker.querySelector('.product-picker-search-input');
        const closeBtn = picker.querySelector('.product-picker-close');
        const listContainer = picker.querySelector('.product-picker-list');
        const toggleBtn = picker.querySelector('.product-picker-toggle');
        const titleEl = picker.querySelector('.product-picker-title');

        // State for pagination and source
        let currentPage = 1;
        let totalCount = 0;
        let isLoading = false;
        let searchTimeout = null;
        let currentSource = 'my'; // 'my' for own products, 'all' for browse products

        // Load products
        const loadProducts = async (searchTerm = '', page = 1, append = false) => {
            if (isLoading) return;
            isLoading = true;

            if (!append) {
                listContainer.innerHTML = `
                    <div class="product-picker-loading">
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M21 12a9 9 0 1 1-6.219-8.56"></path>
                        </svg>
                        <div>Loading products...</div>
                    </div>
                `;
            }

            try {
                const params = new URLSearchParams({
                    page: page.toString(),
                    pageSize: '10'
                });
                if (searchTerm) params.append('searchTerm', searchTerm);
                // Pass recipientId and recipientRole for proper product filtering
                if (chat.userId) params.append('recipientId', chat.userId);
                if (chat.role) params.append('recipientRole', chat.role);
                // Pass source for Provider to choose between own products or all products
                if (showTabs) params.append('source', currentSource);

                const response = await fetch(`${this.apiUrl}/conversations/products-for-chat?${params}`, {
                    headers: { 'Authorization': `Bearer ${this.accessToken}` }
                });

                if (!response.ok) throw new Error('Failed to load products');

                const data = await response.json();
                totalCount = data.totalCount;
                currentPage = page;

                if (!append) {
                    listContainer.innerHTML = '';
                } else {
                    // Remove load more button if exists
                    const loadMoreBtn = listContainer.querySelector('.product-picker-load-more');
                    if (loadMoreBtn) loadMoreBtn.remove();
                }

                if (data.items && data.items.length > 0) {
                    data.items.forEach(product => {
                        const item = document.createElement('div');
                        item.className = 'product-picker-item';
                        item.dataset.productId = product.id;
                        // Build price display based on available options
                        let priceHtml = '';
                        if (product.pricePerDay && product.pricePerDay > 0) {
                            priceHtml += `<span class="product-picker-item-price">₫${product.pricePerDay.toLocaleString()}/day</span>`;
                        }
                        if (product.purchasePrice && product.purchasePrice > 0) {
                            priceHtml += `<span class="product-picker-item-price product-picker-item-buy-price">₫${product.purchasePrice.toLocaleString()}</span>`;
                        }
                        if (!priceHtml) {
                            priceHtml = '<span class="product-picker-item-price">Price not set</span>';
                        }
                        
                        item.innerHTML = `
                            <img class="product-picker-item-image" src="${this.escapeHtml(product.imageUrl || '/images/placeholder.png')}" alt="${this.escapeHtml(product.name)}" onerror="this.src='/images/placeholder.png'">
                            <div class="product-picker-item-info">
                                <div class="product-picker-item-name">${this.escapeHtml(product.name)}</div>
                                <div class="product-picker-item-details">
                                    ${priceHtml}
                                    ${product.category ? `<span class="product-picker-item-category">${this.escapeHtml(product.category)}</span>` : ''}
                                </div>
                            </div>
                        `;
                        item.addEventListener('click', () => {
                            this.selectProductForChat(chat, {
                                id: product.id,
                                name: product.name,
                                imageUrl: product.imageUrl
                            });
                            picker.remove();
                            if (chatInput) {
                                chatInput.classList.remove('picker-open');
                                chatInput.style.height = 'auto';
                                chatInput.style.height = Math.min(chatInput.scrollHeight, 150) + 'px';
                            }
                        });
                        listContainer.appendChild(item);
                    });

                    // Add load more button if there are more items
                    const loadedCount = page * 10;
                    if (loadedCount < totalCount) {
                        const loadMoreBtn = document.createElement('button');
                        loadMoreBtn.className = 'product-picker-load-more';
                        loadMoreBtn.textContent = `Load more (${totalCount - loadedCount} remaining)`;
                        loadMoreBtn.addEventListener('click', () => {
                            loadProducts(searchInput.value.trim(), currentPage + 1, true);
                        });
                        listContainer.appendChild(loadMoreBtn);
                    }
                } else if (!append) {
                    listContainer.innerHTML = `
                        <div class="product-picker-empty">
                            No products found. Try a different search.
                        </div>
                    `;
                }
            } catch (error) {
                console.error('Error loading products:', error);
                if (!append) {
                    listContainer.innerHTML = `
                        <div class="product-picker-empty">
                            Failed to load products. Please try again.
                        </div>
                    `;
                }
            } finally {
                isLoading = false;
            }
        };

        // Initial load
        loadProducts();

        // Toggle button handler (for Provider to switch between My Products and All Products)
        if (toggleBtn) {
            toggleBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                // Toggle source
                currentSource = currentSource === 'my' ? 'all' : 'my';
                toggleBtn.dataset.source = currentSource;
                
                // Update title and button tooltip
                titleEl.textContent = currentSource === 'my' ? 'My Products' : 'All Products';
                toggleBtn.title = currentSource === 'my' ? 'Switch to All Products' : 'Switch to My Products';
                
                // Reload products
                searchInput.value = '';
                loadProducts('', 1, false);
            });
        }

        // Search handler with debounce
        searchInput.addEventListener('input', (e) => {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(() => {
                loadProducts(e.target.value.trim(), 1, false);
            }, 300);
        });

        searchInput.addEventListener('click', (e) => e.stopPropagation());

        // Close button handler
        closeBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            picker.remove();
            if (chatInput) {
                chatInput.classList.remove('picker-open');
                chatInput.style.height = 'auto';
                chatInput.style.height = Math.min(chatInput.scrollHeight, 150) + 'px';
            }
        });

        // Focus search input
        setTimeout(() => searchInput.focus(), 100);

        // Close on outside click
        const chatInputRef = chat.element.querySelector('.chat-input');
        setTimeout(() => {
            document.addEventListener('click', function closeProductPickerHandler(e) {
                if (!picker.contains(e.target) && !e.target.closest('[data-action="product"]')) {
                    picker.remove();
                    if (chatInputRef) {
                        chatInputRef.classList.remove('picker-open');
                        chatInputRef.style.height = 'auto';
                        chatInputRef.style.height = Math.min(chatInputRef.scrollHeight, 150) + 'px';
                    }
                    document.removeEventListener('click', closeProductPickerHandler);
                }
            });
        }, 100);
    }

    /**
     * Select a product to attach to the chat
     * Stores in pendingProductContext (banner only updates after message is sent)
     */
    selectProductForChat(chat, product) {
        // Store in pending - don't update banner until message is sent
        chat.pendingProductContext = product;
        this.showSelectedProductIndicator(chat, product);
    }

    /**
     * Show selected product indicator above input
     */
    showSelectedProductIndicator(chat, product) {
        // Remove existing indicator if any
        const existingIndicator = chat.element.querySelector('.chat-selected-product');
        if (existingIndicator) existingIndicator.remove();

        const inputArea = chat.element.querySelector('.chat-input-area');
        const indicator = document.createElement('div');
        indicator.className = 'chat-selected-product';
        indicator.innerHTML = `
            <img class="chat-selected-product-image" src="${this.escapeHtml(product.imageUrl || '/images/placeholder.png')}" alt="${this.escapeHtml(product.name)}" onerror="this.src='/images/placeholder.png'">
            <div class="chat-selected-product-info">
                <div class="chat-selected-product-label">Attaching product:</div>
                <div class="chat-selected-product-name">${this.escapeHtml(product.name)}</div>
            </div>
            <button type="button" class="chat-selected-product-remove" title="Remove">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <line x1="18" y1="6" x2="6" y2="18"></line>
                    <line x1="6" y1="6" x2="18" y2="18"></line>
                </svg>
            </button>
        `;

        // Insert at the beginning of input area
        inputArea.insertBefore(indicator, inputArea.firstChild);

        // Remove button handler
        indicator.querySelector('.chat-selected-product-remove').addEventListener('click', () => {
            chat.pendingProductContext = null;
            indicator.remove();
        });
    }

    toggleMinimize(userId) {
        const chat = this.chats.get(userId);
        if (!chat) return;

        chat.isMinimized = !chat.isMinimized;
        chat.element.classList.toggle('minimized');

        // Move to appropriate container
        if (chat.isMinimized) {
            this.minimizedContainer.appendChild(chat.element);
        } else {
            this.expandedContainer.appendChild(chat.element);
            
            // Reset unread count when opening
            chat.unreadCount = 0;
            this.updateUnreadBadge(chat);
            
            // Load conversation if not loaded
            if (!chat.conversationId || !chat.messagesLoaded) {
                this.loadConversation(chat);
            } else {
                this.scrollToBottom(chat);
                // Mark messages as read when expanding chat
                if (chat.conversationId) {
                    this.markMessagesAsRead(chat.conversationId);
                }
            }
        }

        this.saveChatsState();
    }

    closeChat(userId) {
        const chat = this.chats.get(userId);
        if (!chat) return;

        // Remove from DOM
        chat.element.remove();

        // Remove from map
        this.chats.delete(userId);

        // Save state
        this.saveChatsState();
    }

    async loadConversation(chat) {
        try {
            // Find or create conversation
            const response = await fetch(`${this.apiUrl}/conversations/find-or-create`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${this.accessToken}`
                },
                body: JSON.stringify({ recipientId: chat.userId })
            });

            if (!response.ok) throw new Error('Failed to create conversation');

            const conversation = await response.json();
            chat.conversationId = conversation.id;

            // Update userName, role and displayName from conversation API (has fullName from Profile)
            const otherParticipant = conversation.otherParticipant;
            if (otherParticipant) {
                // Always update userName from API if available (fullName from Profile table)
                if (otherParticipant.fullName) {
                    chat.userName = otherParticipant.fullName;
                }
                // Update role if available
                if (otherParticipant.role) {
                    chat.role = otherParticipant.role;
                }
                // Update displayName with real name and role
                chat.displayName = chat.role ? `${chat.userName} (${chat.role})` : chat.userName;
                // Update avatar from profile picture if available
                if (otherParticipant.profilePictureUrl) {
                    chat.avatar = this.getInitials(chat.userName);
                }
                // Update the header UI
                const nameElement = chat.element?.querySelector('.chat-user-name');
                if (nameElement) {
                    nameElement.textContent = chat.displayName;
                }
            }

            // Load messages
            await this.loadMessages(chat);
        } catch (error) {
            console.error('Error loading conversation:', error);
            this.showError(chat, 'Failed to load conversation');
        }
    }

    async loadMessages(chat) {
        // Prevent loading messages multiple times
        if (chat.messagesLoaded) {
            return;
        }

        try {
            const response = await fetch(
                `${this.apiUrl}/conversations/${chat.conversationId}/messages?page=1&pageSize=50`,
                {
                    headers: { 'Authorization': `Bearer ${this.accessToken}` }
                }
            );

            if (!response.ok) throw new Error('Failed to load messages');

            const messages = await response.json();
            
            // Clear existing messages and load fresh
            chat.messages = [];
            
            // Check message order from API by comparing timestamps
            // If API returns newest first (most common), reverse it
            if (messages.length > 1) {
                const first = new Date(messages[0].sentAt || messages[0].createdAt);
                const last = new Date(messages[messages.length - 1].sentAt || messages[messages.length - 1].createdAt);
                
                // If first message is newer than last, API returns newest first → reverse
                if (first > last) {
                    chat.messages = messages.reverse();
                } else {
                    // Already oldest first
                    chat.messages = messages;
                }
            } else {
                chat.messages = messages;
            }
            
            chat.messagesLoaded = true; // Mark as loaded

            this.renderMessages(chat);
            
            // Mark messages as read after loading (chat is expanded when loadMessages is called)
            if (chat.conversationId && !chat.isMinimized) {
                this.markMessagesAsRead(chat.conversationId);
            }
        } catch (error) {
            console.error('Error loading messages:', error);
            this.showError(chat, 'Failed to load messages');
        }
    }

    renderMessages(chat) {
        const messagesArea = chat.element.querySelector('.chat-messages-area');
        
        if (chat.messages.length === 0) {
            messagesArea.innerHTML = `
                <div class="chat-empty">
                    <i class="bi bi-chat-dots"></i>
                    <p>No messages yet. Start the conversation!</p>
                </div>
            `;
            return;
        }

        // Clear and render all messages
        messagesArea.innerHTML = '';
        
        // Messages are sorted oldest first
        // Append them in order: oldest at top, newest at bottom
        chat.messages.forEach((msg, index) => {
            this.addMessageToChat(chat, msg, true);
        });

        // Scroll to show newest messages (at bottom)
        this.scrollToBottom(chat);
    }

    addMessageToChat(chat, message, isHistory = false) {
        // Prevent duplicate messages by checking if message already exists
        const messageId = message.id || `${message.senderId}_${message.sentAt || message.createdAt}_${message.content}`;
        const isDuplicate = chat.messages.some(msg => {
            const existingId = msg.id || `${msg.senderId}_${msg.sentAt || msg.createdAt}_${msg.content}`;
            return existingId === messageId;
        });

        if (isDuplicate && !isHistory) {
            return; // Skip adding duplicate message
        }

        const messagesArea = chat.element.querySelector('.chat-messages-area');
        
        // Remove empty state message if exists
        const emptyState = messagesArea.querySelector('.chat-empty');
        if (emptyState) {
            emptyState.remove();
        }
        
        const isMe = message.senderId === this.currentUserId;

        const messageEl = document.createElement('div');
        messageEl.className = `chat-message ${isMe ? 'me' : 'them'}`;
        messageEl.dataset.messageId = messageId; // Track message ID in DOM

        // Build message content - handle attachments
        let messageContent = '';
        if (message.attachment && message.attachment.url) {
            const att = message.attachment;
            if (att.type === 'image') {
                messageContent = `<img src="${this.escapeHtml(att.url)}" alt="${this.escapeHtml(att.fileName || 'image')}" style="max-width:200px;border-radius:8px;cursor:pointer;" onclick="window.open('${this.escapeHtml(att.url)}','_blank')">`;
                if (message.content) {
                    messageContent += `<div style="margin-top:6px;">${this.escapeHtml(message.content)}</div>`;
                }
            } else if (att.type === 'video') {
                messageContent = `<video src="${this.escapeHtml(att.url)}" controls style="max-width:200px;border-radius:8px;"></video>`;
                if (message.content) {
                    messageContent += `<div style="margin-top:6px;">${this.escapeHtml(message.content)}</div>`;
                }
            } else if (att.type === 'audio') {
                // Voice message with waveform style - Messenger layout (single row)
                const audioId = `audio_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
                const barCount = 15;
                messageContent = `
                    <div class="voice-message" data-audio-id="${audioId}" data-bar-count="${barCount}">
                        <button type="button" class="voice-message-play" onclick="window.floatingChatManager.playVoiceMessage('${audioId}')">
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
                                <path d="M8 5v14l11-7z"/>
                            </svg>
                        </button>
                        <div class="voice-message-waveform">
                            ${this.generateWaveformBars(barCount)}
                        </div>
                        <span class="voice-message-duration">0:00</span>
                        <audio id="${audioId}" src="${this.escapeHtml(att.url)}" style="display:none" onloadedmetadata="window.floatingChatManager.updateAudioDuration('${audioId}')"></audio>
                    </div>
                `;
                if (message.content) {
                    messageContent += `<div style="margin-top:6px;">${this.escapeHtml(message.content)}</div>`;
                }
            } else {
                messageContent = `<a href="${this.escapeHtml(att.url)}" target="_blank" rel="noopener noreferrer">📎 ${this.escapeHtml(att.fileName || 'Download file')}</a>`;
                if (message.content) {
                    messageContent += `<div style="margin-top:6px;">${this.escapeHtml(message.content)}</div>`;
                }
            }
        } else {
            messageContent = `<span>${this.escapeHtml(message.content || '')}</span>`;
        }

        // Get timestamp for data attribute (for auto-update)
        const messageTimestamp = message.sentAt || message.createdAt;
        
        // Add avatar for messages from others
        if (!isMe) {
            messageEl.innerHTML = `
                <div class="message-avatar">${this.escapeHtml(chat.avatar)}</div>
                <div class="message-bubble">
                    ${messageContent}
                    <div class="message-time" data-timestamp="${this.escapeHtml(messageTimestamp)}">${this.formatTime(messageTimestamp)}</div>
                </div>
            `;
        } else {
            messageEl.innerHTML = `
                <div class="message-bubble">
                    ${messageContent}
                    <div class="message-time" data-timestamp="${this.escapeHtml(messageTimestamp)}">${this.formatTime(messageTimestamp)}</div>
                </div>
            `;
        }
        
        // Update chat's product context from incoming message if available (always update to latest product)
        // But respect explicitly set productContext (e.g., null from Report Management)
        if (message.productContext && !chat._productContextExplicitlySet) {
            chat.productContext = message.productContext;
            this.updateProductBanner(chat);
        }

        messagesArea.appendChild(messageEl);

        if (!isHistory) {
            chat.messages.push(message);
        }
    }

    async sendMessage(chat) {
        const input = chat.element.querySelector('.chat-input');
        const message = input.value.trim();

        if (!message || !this.signalRConnection || !chat.conversationId) return;

        try {
            // Use pending product context if available, otherwise use current context
            const productContextToSend = chat.pendingProductContext || chat.productContext;
            
            // Show message immediately
            const tempMessage = {
                content: message,
                senderId: this.currentUserId,
                sentAt: new Date().toISOString(),
                productContext: productContextToSend // Include product context in temp message
            };
            this.addMessageToChat(chat, tempMessage);
            this.scrollToBottom(chat);

            // Send via SignalR with productId if available
            const productId = productContextToSend?.id || null;
            await this.signalRConnection.invoke(
                "SendMessageAsync",
                chat.conversationId,
                chat.userId,
                message,
                productId
            );

            input.value = '';
            input.style.height = 'auto'; // Reset textarea height
            
            // Reset send/like button visibility
            const likeBtn = chat.element.querySelector('.chat-like-btn');
            const sendBtn = chat.element.querySelector('.chat-send-btn');
            if (likeBtn) likeBtn.style.display = 'flex';
            if (sendBtn) sendBtn.style.display = 'none';
            
            // After successful send: update product banner if pending product was sent
            if (chat.pendingProductContext) {
                chat.productContext = chat.pendingProductContext;
                chat._productContextExplicitlySet = true;
                this.updateProductBanner(chat);
                chat.pendingProductContext = null;
                
                // Remove the selected product indicator
                const indicator = chat.element.querySelector('.chat-selected-product');
                if (indicator) indicator.remove();
            }
        } catch (error) {
            console.error('Error sending message:', error);
            window.toastManager?.error('Failed to send message');
        }
    }
    
    // Update product banner in chat window
    updateProductBanner(chat) {
        if (!chat.element) return;
        this.updateProductBannerElement(chat.element, chat.productContext);
    }
    
    updateProductBannerElement(element, productContext) {
        const banner = element.querySelector('.chat-product-banner');
        if (!banner) return;
        
        if (productContext && productContext.id) {
            const thumb = banner.querySelector('.chat-product-thumb');
            const name = banner.querySelector('.chat-product-name');
            const link = banner.querySelector('.chat-product-view-link');
            
            if (thumb) thumb.src = productContext.imageUrl || '/images/placeholder.png';
            if (name) name.textContent = productContext.name || 'Product';
            if (link) link.href = `/products/detail/${productContext.id}`;
            
            banner.classList.remove('hidden');
        } else {
            banner.classList.add('hidden');
        }
    }

    updateUnreadBadge(chat) {
        let badge = chat.element.querySelector('.chat-unread-badge');
        
        if (chat.unreadCount > 0) {
            if (!badge) {
                badge = document.createElement('span');
                badge.className = 'chat-unread-badge';
                chat.element.querySelector('.floating-chat-header').appendChild(badge);
            }
            badge.textContent = chat.unreadCount > 99 ? '99+' : chat.unreadCount;
        } else if (badge) {
            badge.remove();
        }
    }

    scrollToBottom(chat) {
        const messagesArea = chat.element.querySelector('.chat-messages-area');
        setTimeout(() => {
            messagesArea.scrollTop = messagesArea.scrollHeight;
        }, 100);
    }

    showError(chat, message) {
        const messagesArea = chat.element.querySelector('.chat-messages-area');
        messagesArea.innerHTML = `
            <div class="chat-empty">
                <i class="bi bi-exclamation-triangle"></i>
                <p>${message}</p>
            </div>
        `;
    }

    getInitials(name) {
        if (!name) return '?';
        const names = name.trim().split(' ');
        if (names.length === 1) {
            return names[0].substring(0, 2).toUpperCase();
        }
        return (names[0].charAt(0) + names[names.length - 1].charAt(0)).toUpperCase();
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Voice message playback helpers
    playVoiceMessage(audioId) {
        const audio = document.getElementById(audioId);
        if (!audio) return;

        const voiceMessage = audio.closest('.voice-message');
        const playBtn = voiceMessage?.querySelector('.voice-message-play');
        const durationEl = voiceMessage?.querySelector('.voice-message-duration');
        const waveformBars = voiceMessage?.querySelectorAll('.waveform-bar');
        const barCount = parseInt(voiceMessage?.dataset.barCount) || 15;
        const totalDuration = audio.duration || 0;
        
        // Clear any existing interval for this audio
        if (audio._playbackInterval) {
            clearInterval(audio._playbackInterval);
            audio._playbackInterval = null;
        }
        
        const updatePlaybackUI = () => {
            if (audio && durationEl) {
                // Show remaining time (countdown)
                const remaining = Math.max(0, Math.ceil(totalDuration - audio.currentTime));
                durationEl.textContent = this.formatDuration(remaining);
                
                // Update waveform progress
                if (waveformBars && totalDuration > 0) {
                    const progress = audio.currentTime / totalDuration;
                    const activeBarCount = Math.floor(progress * barCount);
                    waveformBars.forEach((bar, index) => {
                        if (index < activeBarCount) {
                            bar.classList.add('active');
                        } else {
                            bar.classList.remove('active');
                        }
                    });
                }
            }
        };
        
        const resetUI = () => {
            if (durationEl && totalDuration) {
                durationEl.textContent = this.formatDuration(Math.floor(totalDuration));
            }
            if (waveformBars) {
                waveformBars.forEach(bar => bar.classList.remove('active'));
            }
            if (playBtn) {
                playBtn.innerHTML = `<svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor"><path d="M8 5v14l11-7z"/></svg>`;
            }
        };
        
        if (audio.paused) {
            // Stop all other playing audios
            document.querySelectorAll('.voice-message audio').forEach(a => {
                if (a.id !== audioId && !a.paused) {
                    a.pause();
                    a.currentTime = 0;
                    if (a._playbackInterval) {
                        clearInterval(a._playbackInterval);
                        a._playbackInterval = null;
                    }
                    const otherVm = a.closest('.voice-message');
                    const otherBtn = otherVm?.querySelector('.voice-message-play');
                    const otherDuration = otherVm?.querySelector('.voice-message-duration');
                    const otherBars = otherVm?.querySelectorAll('.waveform-bar');
                    if (otherBtn) {
                        otherBtn.innerHTML = `<svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor"><path d="M8 5v14l11-7z"/></svg>`;
                    }
                    if (otherDuration && a.duration) {
                        otherDuration.textContent = this.formatDuration(Math.floor(a.duration));
                    }
                    if (otherBars) {
                        otherBars.forEach(bar => bar.classList.remove('active'));
                    }
                }
            });

            audio.play();
            audio._playbackInterval = setInterval(updatePlaybackUI, 50);
            if (playBtn) {
                playBtn.innerHTML = `<svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor"><rect x="6" y="4" width="4" height="16"/><rect x="14" y="4" width="4" height="16"/></svg>`;
            }
        } else {
            audio.pause();
            if (audio._playbackInterval) {
                clearInterval(audio._playbackInterval);
                audio._playbackInterval = null;
            }
            if (playBtn) {
                playBtn.innerHTML = `<svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor"><path d="M8 5v14l11-7z"/></svg>`;
            }
        }

        audio.onended = () => {
            if (audio._playbackInterval) {
                clearInterval(audio._playbackInterval);
                audio._playbackInterval = null;
            }
            audio.currentTime = 0;
            resetUI();
        };
    }

    updateAudioDuration(audioId) {
        const audio = document.getElementById(audioId);
        if (!audio) return;

        const voiceMessage = audio.closest('.voice-message');
        const durationEl = voiceMessage?.querySelector('.voice-message-duration');
        
        if (durationEl && audio.duration && !isNaN(audio.duration)) {
            durationEl.textContent = this.formatDuration(Math.floor(audio.duration));
        }
    }

    formatTime(dateString) {
        const date = new Date(dateString);
        const now = new Date();
        const diff = now - date;
        
        // Less than 1 minute
        if (diff < 60000) return 'Just now';
        
        // Less than 1 hour
        if (diff < 3600000) {
            const minutes = Math.floor(diff / 60000);
            return `${minutes}m ago`;
        }
        
        // Less than 24 hours
        if (diff < 86400000) {
            return date.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit', timeZone: 'Asia/Ho_Chi_Minh' });
        }
        
        // Older
        return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', timeZone: 'Asia/Ho_Chi_Minh' });
    }

    // Public method to open chat from outside
    static openChatWith(userId, userName, avatar, role = null) {
        if (!window.floatingChatManager) {
            console.warn('FloatingChatManager not initialized');
            return;
        }
        window.floatingChatManager.openChat(userId, userName, avatar, false, role);
    }
}

// Initialize immediately - no waiting for DOM
// This ensures the manager is available when other scripts call it
window.floatingChatManager = new FloatingChatManager();

// Global helper function - simple and direct since manager initializes immediately
window.openFloatingChat = (userId, userName, avatar, role = null, productContext = null) => {
    if (window.floatingChatManager) {
        window.floatingChatManager.openChat(userId, userName, avatar, false, role, productContext);
    } else {
        console.error('FloatingChatManager not initialized');
    }
};

// Helper function to open chat with product context from Product Detail page
window.openFloatingChatWithProduct = (userId, userName, avatar, role, product) => {
    const productContext = product ? {
        id: product.id,
        name: product.name,
        imageUrl: product.imageUrl
    } : null;
    window.openFloatingChat(userId, userName, avatar, role, productContext);
};

} // End of guard against duplicate declaration
