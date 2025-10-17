/**
 * Floating Chat Manager - Messenger Style
 * Manages multiple floating chat windows that persist across page navigation
 */

class FloatingChatManager {
    constructor() {
        this.chats = new Map();
        this.signalRConnection = null;
        this.currentUserId = null;
        this.accessToken = null;
        this.apiUrl = null;
        this.signalRUrl = null;
        this.container = null;
        
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
    }

    loadConfig() {
        // Try to get from adminChatConfig first (server-side rendered)
        if (window.adminChatConfig) {
            this.currentUserId = window.adminChatConfig.currentUserId;
            this.accessToken = window.adminChatConfig.accessToken;
            this.apiUrl = window.adminChatConfig.apiBaseUrl;
            this.signalRUrl = window.adminChatConfig.signalRRootUrl;
        }
        
        // Fallback to cookies if not available from config
        if (!this.currentUserId) {
            this.currentUserId = this.getCookie('UserId');
        }
        if (!this.accessToken) {
            this.accessToken = this.getCookie('AccessToken');
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
    }

    loadSavedChats() {
        try {
            const saved = localStorage.getItem('floatingChats');
            if (!saved) return;

            const chatStates = JSON.parse(saved);
            chatStates.forEach(state => {
                this.openChat(state.userId, state.userName, state.avatar, state.minimized);
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
                    minimized: chat.isMinimized
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
            // Add message to existing chat
            this.addMessageToChat(chat, message, false);
            
            // Show unread badge if minimized
            if (chat.isMinimized) {
                chat.unreadCount++;
                this.updateUnreadBadge(chat);
            } else {
                this.scrollToBottom(chat);
            }
        } else {
            // Auto-open new chat window for incoming message
            console.log('New message from:', message);
            
            // Get sender info from message or fetch from API
            await this.openChatForIncomingMessage(message);
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
                const avatar = this.getInitials(senderName);
                
                // Open chat with proper name
                await this.openChat(message.senderId, senderName, avatar, false);
                
                // Add the message
                const chat = this.chats.get(message.senderId);
                if (chat) {
                    this.addMessageToChat(chat, message, false);
                    this.scrollToBottom(chat);
                }
            }
        } catch (error) {
            console.error('Error opening chat for incoming message:', error);
            // Fallback: open with basic info
            const senderName = 'User';
            const avatar = this.getInitials(senderName);
            await this.openChat(message.senderId, senderName, avatar, false);
        }
    }

    async openChat(userId, userName, avatar = null, minimized = false) {
        // Check if chat already exists
        if (this.chats.has(userId)) {
            const chat = this.chats.get(userId);
            if (chat.isMinimized) {
                this.toggleMinimize(userId);
            }
            return;
        }

        // Create chat window
        const chat = {
            userId: userId,
            userName: userName,
            avatar: avatar || this.getInitials(userName),
            conversationId: null,
            messages: [],
            isMinimized: minimized,
            unreadCount: 0,
            element: null
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

        // Load conversation
        if (!minimized) {
            await this.loadConversation(chat);
        }

        // Save state
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
                    <div class="chat-user-name">${this.escapeHtml(chat.userName)}</div>
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
                <div class="chat-messages-area">
                    <div class="chat-loading">
                        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="animate-spin">
                            <path d="M21 12a9 9 0 1 1-6.219-8.56"></path>
                        </svg>
                        Loading messages...
                    </div>
                </div>
                <div class="chat-input-area">
                    <form class="chat-input-form">
                        <input type="text" class="chat-input" placeholder="Type a message..." autocomplete="off">
                        <button type="submit" class="chat-send-btn" title="Send">
                            <span class="send-icon">➤</span>
                        </button>
                    </form>
                </div>
            </div>
        `;

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

        return window;
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
            if (!chat.conversationId) {
                this.loadConversation(chat);
            } else {
                this.scrollToBottom(chat);
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

            // Load messages
            await this.loadMessages(chat);
        } catch (error) {
            console.error('Error loading conversation:', error);
            this.showError(chat, 'Failed to load conversation');
        }
    }

    async loadMessages(chat) {
        try {
            const response = await fetch(
                `${this.apiUrl}/conversations/${chat.conversationId}/messages?page=1&pageSize=50`,
                {
                    headers: { 'Authorization': `Bearer ${this.accessToken}` }
                }
            );

            if (!response.ok) throw new Error('Failed to load messages');

            const messages = await response.json();
            chat.messages = messages.reverse();

            this.renderMessages(chat);
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

        messagesArea.innerHTML = '';
        chat.messages.forEach(msg => {
            this.addMessageToChat(chat, msg, true);
        });

        this.scrollToBottom(chat);
    }

    addMessageToChat(chat, message, isHistory = false) {
        const messagesArea = chat.element.querySelector('.chat-messages-area');
        const isMe = message.senderId === this.currentUserId;

        const messageEl = document.createElement('div');
        messageEl.className = `chat-message ${isMe ? 'me' : 'them'}`;
        
        // Add avatar for messages from others
        if (!isMe) {
            messageEl.innerHTML = `
                <div class="message-avatar">${this.escapeHtml(chat.avatar)}</div>
                <div class="message-bubble">
                    <div>${this.escapeHtml(message.content)}</div>
                    <div class="message-time">${this.formatTime(message.sentAt || message.createdAt)}</div>
                </div>
            `;
        } else {
            messageEl.innerHTML = `
                <div class="message-bubble">
                    <div>${this.escapeHtml(message.content)}</div>
                    <div class="message-time">${this.formatTime(message.sentAt || message.createdAt)}</div>
                </div>
            `;
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
            // Show message immediately
            const tempMessage = {
                content: message,
                senderId: this.currentUserId,
                sentAt: new Date().toISOString()
            };
            this.addMessageToChat(chat, tempMessage);
            this.scrollToBottom(chat);

            // Send via SignalR
            await this.signalRConnection.invoke(
                "SendMessageAsync",
                chat.conversationId,
                chat.userId,
                message,
                null // productId
            );

            input.value = '';
        } catch (error) {
            console.error('Error sending message:', error);
            window.toastManager?.error('Failed to send message');
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
            return date.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' });
        }
        
        // Older
        return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    }

    // Public method to open chat from outside
    static openChatWith(userId, userName, avatar) {
        if (!window.floatingChatManager) {
            console.warn('FloatingChatManager not initialized');
            return;
        }
        window.floatingChatManager.openChat(userId, userName, avatar);
    }
}

// Initialize on DOM ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        window.floatingChatManager = new FloatingChatManager();
    });
} else {
    window.floatingChatManager = new FloatingChatManager();
}

// Global helper function
window.openFloatingChat = (userId, userName, avatar) => {
    if (window.floatingChatManager) {
        window.floatingChatManager.openChat(userId, userName, avatar);
    } else {
        console.error('FloatingChatManager not initialized');
    }
};

