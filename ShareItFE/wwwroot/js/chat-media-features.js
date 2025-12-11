/**
 * Chat Media Features - Emoji, GIF, Voice for Messages Pages
 * Shared module for Admin, Customer, Provider, Staff message pages
 * Uses 'page-chat-' prefix to avoid conflicts with floating-chat
 */

class ChatMediaFeatures {
    constructor(options) {
        this.messageInput = options.messageInput;
        this.sendBtn = options.sendBtn;
        this.messageForm = options.messageForm;
        this.apiUrl = options.apiUrl;
        this.accessToken = options.accessToken;
        this.getSelectedConversation = options.getSelectedConversation;
        this.getSignalRConnection = options.getSignalRConnection;
        this.currentUserRole = options.currentUserRole || null;
        
        this.mediaRecorder = null;
        this.audioChunks = [];
        this.recordingStartTime = null;
        this.recordingTimer = null;
        this.recordedBlob = null;
        
        this.init();
    }

    init() {
        this.createMediaButtons();
        this.setupEventListeners();
    }

    createMediaButtons() {
        // Find existing attach button
        this.attachBtn = this.messageForm.querySelector('.attach-btn');
        
        // Create media actions container - Voice, Attach (moved), Emoji, GIF
        const mediaContainer = document.createElement('div');
        mediaContainer.className = 'page-chat-media-actions';
        
        // Voice button
        const voiceBtn = document.createElement('button');
        voiceBtn.type = 'button';
        voiceBtn.className = 'page-chat-media-btn';
        voiceBtn.dataset.action = 'voice';
        voiceBtn.title = 'Voice message';
        voiceBtn.innerHTML = `<svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
            <path d="M12 14c1.66 0 3-1.34 3-3V5c0-1.66-1.34-3-3-3S9 3.34 9 5v6c0 1.66 1.34 3 3 3zm-1-9c0-.55.45-1 1-1s1 .45 1 1v6c0 .55-.45 1-1 1s-1-.45-1-1V5z"/>
            <path d="M17 11c0 2.76-2.24 5-5 5s-5-2.24-5-5H5c0 3.53 2.61 6.43 6 6.92V21h2v-3.08c3.39-.49 6-3.39 6-6.92h-2z"/>
        </svg>`;
        mediaContainer.appendChild(voiceBtn);
        this.voiceBtn = voiceBtn;

        // Move attach button to media container (if exists)
        if (this.attachBtn) {
            const newAttachBtn = document.createElement('button');
            newAttachBtn.type = 'button';
            newAttachBtn.className = 'page-chat-media-btn';
            newAttachBtn.title = 'Attach file';
            newAttachBtn.innerHTML = `<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 1 1 5.66 5.66l-9.2 9.19a2 2 0 1 1-2.83-2.83l8.49-8.48"/>
            </svg>`;
            newAttachBtn.addEventListener('click', () => this.attachBtn.click());
            mediaContainer.appendChild(newAttachBtn);
            // Hide original attach button
            this.attachBtn.style.display = 'none';
        }

        // Emoji button
        const emojiBtn = document.createElement('button');
        emojiBtn.type = 'button';
        emojiBtn.className = 'page-chat-media-btn';
        emojiBtn.title = 'Emoji';
        emojiBtn.innerHTML = `<svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
            <path d="M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm3.5-9c.83 0 1.5-.67 1.5-1.5S16.33 8 15.5 8 14 8.67 14 9.5s.67 1.5 1.5 1.5zm-7 0c.83 0 1.5-.67 1.5-1.5S9.33 8 8.5 8 7 8.67 7 9.5 7.67 11 8.5 11zm3.5 6.5c2.33 0 4.31-1.46 5.11-3.5H6.89c.8 2.04 2.78 3.5 5.11 3.5z"/>
        </svg>`;
        mediaContainer.appendChild(emojiBtn);
        this.emojiBtn = emojiBtn;

        // GIF button
        const gifBtn = document.createElement('button');
        gifBtn.type = 'button';
        gifBtn.className = 'page-chat-media-btn';
        gifBtn.title = 'Send GIF';
        gifBtn.innerHTML = `<span class="page-chat-gif-text">GIF</span>`;
        mediaContainer.appendChild(gifBtn);
        this.gifBtn = gifBtn;

        // Product button
        const productBtn = document.createElement('button');
        productBtn.type = 'button';
        productBtn.className = 'page-chat-media-btn';
        productBtn.title = 'Attach product';
        productBtn.innerHTML = `<svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
            <path d="M18 6h-2c0-2.21-1.79-4-4-4S8 3.79 8 6H6c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm-6-2c1.1 0 2 .9 2 2h-4c0-1.1.9-2 2-2zm6 16H6V8h2v2c0 .55.45 1 1 1s1-.45 1-1V8h4v2c0 .55.45 1 1 1s1-.45 1-1V8h2v12z"/>
        </svg>`;
        mediaContainer.appendChild(productBtn);
        this.productBtn = productBtn;

        // Insert media container before the form
        this.messageForm.parentNode.insertBefore(mediaContainer, this.messageForm);
        this.mediaContainer = mediaContainer;

        // Create like button (emoji 👍) - insert after send button
        this.likeBtn = document.createElement('button');
        this.likeBtn.type = 'button';
        this.likeBtn.className = 'page-chat-like-btn';
        this.likeBtn.title = 'Like';
        this.likeBtn.textContent = '👍';
        this.messageForm.appendChild(this.likeBtn);

        // Create recording UI container
        this.recordingUI = document.createElement('div');
        this.recordingUI.className = 'page-chat-recording-ui hidden';
        this.messageForm.parentNode.insertBefore(this.recordingUI, this.messageForm.nextSibling);

        // Create preview UI container
        this.previewUI = document.createElement('div');
        this.previewUI.className = 'page-chat-preview-ui hidden';
        this.messageForm.parentNode.insertBefore(this.previewUI, this.recordingUI.nextSibling);
    }

    setupEventListeners() {
        // Media buttons
        this.voiceBtn.addEventListener('click', () => this.startVoiceRecording());
        this.emojiBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.toggleEmojiPicker();
        });
        this.gifBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.toggleGifPicker();
        });
        this.productBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.toggleProductPicker();
        });
        this.likeBtn.addEventListener('click', () => this.sendLikeEmoji());

        // Toggle send/like button based on input - ONLY show Send when typing text
        this.messageInput.addEventListener('input', () => {
            this.updateSendLikeButton();
        });

        // Listen for form submit to reset button state after message is sent
        this.messageForm.addEventListener('submit', () => {
            // Use setTimeout to ensure this runs after the form submit handler resets the input
            // The SignalR invoke is async, so we need to wait a bit longer
            setTimeout(() => {
                this.updateSendLikeButton();
            }, 500);
        });

        // Also poll for input changes since programmatic value changes don't trigger 'input' event
        this._lastInputValue = this.messageInput.value;
        setInterval(() => {
            if (this.messageInput.value !== this._lastInputValue) {
                this._lastInputValue = this.messageInput.value;
                this.updateSendLikeButton();
            }
        }, 200);

        // Initial state - show like, hide send
        this.updateSendLikeButton();
    }

    updateSendLikeButton() {
        // Only show Send button when there's actual text input
        if (this.messageInput.value.trim()) {
            this.likeBtn.style.display = 'none';
            this.sendBtn.style.display = 'inline-flex';
        } else {
            this.likeBtn.style.display = 'inline-flex';
            this.sendBtn.style.display = 'none';
        }
    }

    // ==================== EMOJI PICKER (Messenger-style with search & categories) ====================
    getEmojiCategories() {
        if (!this._emojiCategories) {
            this._emojiCategories = {
                recent: { name: 'Recent', icon: '🕐', emojis: [] },
                smileys: { name: 'Smileys', icon: '😀', emojis: ['😀', '😃', '😄', '😁', '😆', '😅', '🤣', '😂', '🙂', '😊', '😇', '🥰', '😍', '🤩', '😘', '😗', '😚', '😙', '🥲', '😋', '😛', '😜', '🤪', '😝', '🤑', '🤗', '🤭', '🤫', '🤔', '🤐', '🤨', '😐', '😑', '😶', '😏', '😒', '🙄', '😬', '🤥', '😌', '😔', '😪', '🤤', '😴', '😷', '🤒', '🤕', '🤢', '🤮', '🤧', '🥵', '🥶', '🥴', '😵', '🤯', '🤠', '🥳', '🥸', '😎', '🤓', '🧐', '😕', '😟', '🙁', '☹️', '😮', '😯', '😲', '😳', '🥺', '😦', '😧', '😨', '😰', '😥', '😢', '😭', '😱', '😖', '😣', '😞', '😓', '😩', '😫', '🥱', '😤', '😡', '😠', '🤬'] },
                gestures: { name: 'Gestures', icon: '👋', emojis: ['👋', '🤚', '🖐️', '✋', '🖖', '👌', '🤌', '🤏', '✌️', '🤞', '🤟', '🤘', '🤙', '👈', '👉', '👆', '🖕', '👇', '☝️', '👍', '👎', '✊', '👊', '🤛', '🤜', '👏', '🙌', '👐', '🤲', '🤝', '🙏', '✍️', '💅', '🤳', '💪', '🦾', '🦿', '🦵', '🦶', '👂', '🦻', '👃', '🧠', '🫀', '🫁', '🦷', '🦴', '👀', '👁️', '👅', '👄'] },
                hearts: { name: 'Hearts', icon: '❤️', emojis: ['❤️', '🧡', '💛', '💚', '💙', '💜', '🖤', '🤍', '🤎', '💔', '❣️', '💕', '💞', '💓', '💗', '💖', '💘', '💝', '💟', '♥️', '💌', '💋', '😻', '😽', '🫶', '🥰', '😍', '🤩', '😘', '💑', '👩‍❤️‍👨', '👨‍❤️‍👨', '👩‍❤️‍👩', '💏'] },
                animals: { name: 'Animals', icon: '🐶', emojis: ['🐶', '🐱', '🐭', '🐹', '🐰', '🦊', '🐻', '🐼', '🐻‍❄️', '🐨', '🐯', '🦁', '🐮', '🐷', '🐸', '🐵', '🙈', '🙉', '🙊', '🐔', '🐧', '🐦', '🐤', '🦆', '🦅', '🦉', '🦇', '🐺', '🐗', '🐴', '🦄', '🐝', '🪱', '🐛', '🦋', '🐌', '🐞', '🐜', '🪰', '🪲', '🪳', '🦟', '🦗', '🕷️', '🦂', '🐢', '🐍', '🦎', '🦖', '🦕', '🐙', '🦑', '🦐', '🦞', '🦀', '🐡', '🐠', '🐟', '🐬', '🐳', '🐋', '🦈', '🐊'] },
                food: { name: 'Food', icon: '🍔', emojis: ['🍎', '🍐', '🍊', '🍋', '🍌', '🍉', '🍇', '🍓', '🫐', '🍈', '🍒', '🍑', '🥭', '🍍', '🥥', '🥝', '🍅', '🍆', '🥑', '🥦', '🥬', '🥒', '🌶️', '🫑', '🌽', '🥕', '🫒', '🧄', '🧅', '🥔', '🍠', '🥐', '🥯', '🍞', '🥖', '🥨', '🧀', '🥚', '🍳', '🧈', '🥞', '🧇', '🥓', '🥩', '🍗', '🍖', '🦴', '🌭', '🍔', '🍟', '🍕', '🫓', '🥪', '🥙', '🧆', '🌮', '🌯', '🫔', '🥗', '🥘', '🫕', '🍝', '🍜', '🍲', '🍛', '🍣', '🍱', '🥟', '🦪', '🍤', '🍙', '🍚', '🍘', '🍥', '🥠', '🥮', '🍢', '🍡', '🍧', '🍨', '🍦', '🥧', '🧁', '🍰', '🎂', '🍮', '🍭', '🍬', '🍫', '🍿', '🍩', '🍪', '🌰', '🥜', '🍯', '🥛', '🍼', '🫖', '☕', '🍵', '🧃', '🥤', '🧋', '🍶', '🍺', '🍻', '🥂', '🍷', '🥃', '🍸', '🍹', '🧉', '🍾'] },
                activities: { name: 'Activities', icon: '⚽', emojis: ['⚽', '🏀', '🏈', '⚾', '🥎', '🎾', '🏐', '🏉', '🥏', '🎱', '🪀', '🏓', '🏸', '🏒', '🏑', '🥍', '🏏', '🪃', '🥅', '⛳', '🪁', '🏹', '🎣', '🤿', '🥊', '🥋', '🎽', '🛹', '🛼', '🛷', '⛸️', '🥌', '🎿', '⛷️', '🏂', '🪂', '🏋️', '🤼', '🤸', '⛹️', '🤺', '🤾', '🏌️', '🏇', '⛑️', '🧘', '🏄', '🏊', '🤽', '🚣', '🧗', '🚵', '🚴', '🏆', '🥇', '🥈', '🥉', '🏅', '🎖️', '🏵️', '🎗️', '🎫', '🎟️', '🎪', '🎭', '🎨', '🎬', '🎤', '🎧', '🎼', '🎹', '🥁', '🪘', '🎷', '🎺', '🪗', '🎸', '🪕', '🎻', '🎲', '♟️', '🎯', '🎳', '🎮', '🎰', '🧩'] },
                objects: { name: 'Objects', icon: '💡', emojis: ['⌚', '📱', '💻', '🖥️', '🖨️', '🖱️', '💽', '💾', '💿', '📷', '📹', '🎥', '📞', '📺', '📻', '⏰', '🔋', '🔌', '💡', '🔦', '💸', '💵', '💰', '💳', '💎', '🔧', '🔨', '🔩', '⚙️', '🔫', '💣', '🔪', '🛡️', '🔮', '💊', '💉', '🧬', '🔬', '🔭', '🧹', '🧺', '🧻', '🚽', '🛁', '🧼', '🔑', '🚪', '🛋️', '🛏️', '🧸', '🎁', '🎈', '🎀', '🎊', '🎉', '✉️', '📦', '📜', '📄', '📊', '📅', '📁', '📰', '📚', '📖', '🔖', '📎', '✂️', '📝', '✏️', '🔍', '🔒'] },
                symbols: { name: 'Symbols', icon: '💯', emojis: ['💯', '✅', '❌', '❓', '❗', '💢', '💥', '💫', '💦', '💨', '🔴', '🟠', '🟡', '🟢', '🔵', '🟣', '⚫', '⚪', '🔶', '🔷', '🔸', '🔹', '▶️', '⏸️', '⏹️', '⏺️', '⏭️', '⏮️', '🔀', '🔁', '🔂', '➕', '➖', '➗', '✖️', '♾️', '💲', '🔃', '🔄', '🔙', '🔚', '🔛', '🔜', '🔝', '🏁', '🚩', '🎌', '🏴', '🏳️'] }
            };
        }
        return this._emojiCategories;
    }

    loadRecentEmojis() {
        try {
            const recent = localStorage.getItem('pageChat_recentEmojis');
            if (recent) {
                this.getEmojiCategories().recent.emojis = JSON.parse(recent);
            }
        } catch (e) {
            console.error('Error loading recent emojis:', e);
        }
    }

    addRecentEmoji(emoji) {
        try {
            const categories = this.getEmojiCategories();
            let recent = categories.recent.emojis;
            recent = recent.filter(e => e !== emoji);
            recent.unshift(emoji);
            recent = recent.slice(0, 24);
            categories.recent.emojis = recent;
            localStorage.setItem('pageChat_recentEmojis', JSON.stringify(recent));
        } catch (e) {
            console.error('Error saving recent emoji:', e);
        }
    }

    toggleEmojiPicker() {
        const existingPicker = document.querySelector('.page-chat-emoji-picker');
        if (existingPicker) {
            existingPicker.remove();
            return;
        }

        this.closePickers();
        this.loadRecentEmojis();

        const categories = this.getEmojiCategories();
        
        const picker = document.createElement('div');
        picker.className = 'page-chat-emoji-picker';
        picker.innerHTML = `
            <div class="page-chat-emoji-search">
                <input type="text" class="page-chat-emoji-search-input" placeholder="Search emoji..." autocomplete="off">
            </div>
            <div class="page-chat-emoji-tabs">
                ${Object.keys(categories).map(key => 
                    `<button type="button" class="page-chat-emoji-tab ${key === 'smileys' ? 'active' : ''}" data-category="${key}" title="${categories[key].name}">${categories[key].icon}</button>`
                ).join('')}
            </div>
            <div class="page-chat-emoji-content">
                <div class="page-chat-emoji-grid"></div>
            </div>
        `;
        
        // Insert before message form
        this.messageForm.parentNode.insertBefore(picker, this.messageForm);

        const searchInput = picker.querySelector('.page-chat-emoji-search-input');
        const tabs = picker.querySelectorAll('.page-chat-emoji-tab');
        const grid = picker.querySelector('.page-chat-emoji-grid');
        let currentCategory = 'smileys';

        // Render emojis for current category
        const renderEmojis = (categoryKey, searchQuery = '') => {
            let emojis = categories[categoryKey]?.emojis || [];
            
            if (searchQuery) {
                // Search across all categories
                emojis = [];
                Object.values(categories).forEach(cat => {
                    emojis.push(...cat.emojis.filter(e => e.includes(searchQuery)));
                });
                emojis = [...new Set(emojis)];
            }
            
            if (emojis.length === 0) {
                grid.innerHTML = '<div class="page-chat-emoji-empty">No emoji found</div>';
            } else {
                grid.innerHTML = emojis.map(e => `<span class="page-chat-emoji-item">${e}</span>`).join('');
            }
        };

        // Initial render
        renderEmojis(currentCategory);

        // Tab click handler
        tabs.forEach(tab => {
            tab.addEventListener('click', (e) => {
                e.stopPropagation();
                tabs.forEach(t => t.classList.remove('active'));
                tab.classList.add('active');
                currentCategory = tab.dataset.category;
                searchInput.value = '';
                renderEmojis(currentCategory);
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
                    renderEmojis(null, query);
                } else {
                    tabs.forEach(t => t.classList.toggle('active', t.dataset.category === currentCategory));
                    renderEmojis(currentCategory);
                }
            }, 200);
        });

        searchInput.addEventListener('click', (e) => e.stopPropagation());

        // Emoji click handler
        grid.addEventListener('click', (e) => {
            if (e.target.classList.contains('page-chat-emoji-item')) {
                e.stopPropagation();
                const emoji = e.target.textContent;
                this.addRecentEmoji(emoji);
                const cursorPos = this.messageInput.selectionStart || this.messageInput.value.length;
                const textBefore = this.messageInput.value.substring(0, cursorPos);
                const textAfter = this.messageInput.value.substring(this.messageInput.selectionEnd || cursorPos);
                this.messageInput.value = textBefore + emoji + textAfter;
                this.messageInput.dispatchEvent(new Event('input'));
                this.messageInput.focus();
            }
        });

        // Focus search
        setTimeout(() => searchInput.focus(), 100);

        // Close picker when clicking outside
        const closePicker = (e) => {
            if (!picker.contains(e.target) && e.target !== this.emojiBtn && !this.emojiBtn.contains(e.target)) {
                picker.remove();
                document.removeEventListener('click', closePicker);
            }
        };
        setTimeout(() => document.addEventListener('click', closePicker), 10);
    }

    // ==================== GIF PICKER ====================
    toggleGifPicker() {
        const existingPicker = document.querySelector('.page-chat-gif-picker');
        if (existingPicker) {
            existingPicker.remove();
            return;
        }

        this.closePickers();

        const picker = document.createElement('div');
        picker.className = 'page-chat-gif-picker';
        picker.innerHTML = `
            <div class="page-chat-gif-search">
                <input type="text" class="page-chat-gif-input" placeholder="Search GIFs...">
            </div>
            <div class="page-chat-gif-container">
                <div class="page-chat-gif-grid"></div>
            </div>
        `;

        this.messageForm.parentNode.insertBefore(picker, this.messageForm);

        const searchInput = picker.querySelector('.page-chat-gif-input');
        const gridContainer = picker.querySelector('.page-chat-gif-grid');
        let searchTimeout;

        // Load trending GIFs initially
        this.loadGifs('trending', gridContainer);

        searchInput.addEventListener('input', () => {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(() => {
                const query = searchInput.value.trim();
                this.loadGifs(query || 'trending', gridContainer);
            }, 500);
        });

        gridContainer.addEventListener('click', async (e) => {
            if (e.target.classList.contains('page-chat-gif-item')) {
                const gifUrl = e.target.dataset.url || e.target.src;
                await this.sendGifAsAttachment(gifUrl);
                picker.remove();
            }
        });

        // Close picker when clicking outside
        const closePicker = (e) => {
            if (!picker.contains(e.target) && e.target !== this.gifBtn && !this.gifBtn.contains(e.target)) {
                picker.remove();
                document.removeEventListener('click', closePicker);
            }
        };
        setTimeout(() => document.addEventListener('click', closePicker), 10);
    }

    async loadGifs(query, container) {
        container.innerHTML = '<div class="page-chat-gif-loading"><svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 12a9 9 0 1 1-6.219-8.56"></path></svg>Loading...</div>';
        
        try {
            // Use Giphy API (same as floating-chat) for better compatibility
            const apiKey = 'Gc7131jiJuvI7IdN0HZ1D7nh0ow5BU6g'; // Giphy public beta key
            const limit = 20;
            let url;
            
            if (query === 'trending') {
                url = `https://api.giphy.com/v1/gifs/trending?api_key=${apiKey}&limit=${limit}&rating=g`;
            } else {
                url = `https://api.giphy.com/v1/gifs/search?api_key=${apiKey}&q=${encodeURIComponent(query)}&limit=${limit}&rating=g`;
            }
            
            const response = await fetch(url);
            const data = await response.json();
            
            if (data.data && data.data.length > 0) {
                container.innerHTML = data.data.map(gif => {
                    const previewUrl = gif.images.fixed_height_small?.url || gif.images.fixed_height?.url;
                    const fullUrl = gif.images.original?.url || gif.images.fixed_height?.url;
                    return `<img class="page-chat-gif-item" src="${previewUrl}" data-url="${fullUrl}" alt="${gif.title || 'GIF'}" loading="lazy">`;
                }).join('');
            } else {
                container.innerHTML = '<div class="page-chat-gif-empty">No GIFs found</div>';
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
                { url: 'https://media.giphy.com/media/3o7TKMt1VVNkHV2PaE/giphy.gif', alt: 'sad' }
            ];
            container.innerHTML = fallbackGifs.map(g => 
                `<img class="page-chat-gif-item" src="${g.url}" data-url="${g.url}" alt="${g.alt}" loading="lazy">`
            ).join('');
        }
    }

    async sendGifAsAttachment(gifUrl) {
        const conversation = this.getSelectedConversation();
        const connection = this.getSignalRConnection();
        
        if (!conversation) {
            alert('Please select a conversation first');
            return;
        }
        
        if (!connection || connection.state !== 'Connected') {
            alert('Connection lost. Please refresh the page and try again.');
            return;
        }

        try {
            // Send GIF URL directly (like floating-chat) - no upload to Cloudinary
            await connection.invoke('SendMessageWithAttachmentAsync',
                conversation.id,
                conversation.otherParticipant.userId,
                '',      // content
                null,    // productId
                gifUrl,  // url - direct GIF URL
                'image', // type
                null,    // publicId
                null,    // thumbnailUrl
                'image/gif', // mimeType
                'gif',   // fileName
                null     // fileSize
            );
            // Reset button state after sending GIF
            this.messageInput.value = '';
            this.updateSendLikeButton();
        } catch (error) {
            console.error('Error sending GIF:', error);
            alert('Failed to send GIF. Please check your connection.');
        }
    }


    // ==================== VOICE RECORDING ====================
    async startVoiceRecording() {
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            this.audioChunks = [];
            this.mediaRecorder = new MediaRecorder(stream);
            
            this.mediaRecorder.ondataavailable = (e) => {
                if (e.data.size > 0) {
                    this.audioChunks.push(e.data);
                }
            };

            this.mediaRecorder.onstop = () => {
                stream.getTracks().forEach(track => track.stop());
                this.recordedBlob = new Blob(this.audioChunks, { type: 'audio/webm' });
                this.showVoicePreview();
            };

            this.mediaRecorder.start();
            this.recordingStartTime = Date.now();
            this.showRecordingUI();
            
        } catch (error) {
            console.error('Error accessing microphone:', error);
            alert('Could not access microphone. Please check permissions.');
        }
    }

    showRecordingUI() {
        this.messageForm.classList.add('hidden');
        this.mediaContainer.classList.add('hidden');
        this.recordingUI.classList.remove('hidden');
        
        this.recordingUI.innerHTML = `
            <button type="button" class="page-chat-rec-cancel" title="Cancel">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <line x1="18" y1="6" x2="6" y2="18"></line>
                    <line x1="6" y1="6" x2="18" y2="18"></line>
                </svg>
            </button>
            <span class="page-chat-rec-timer">0:00</span>
            <button type="button" class="page-chat-rec-stop" title="Stop">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                    <rect x="6" y="6" width="12" height="12" rx="2"/>
                </svg>
            </button>
        `;

        this.recordingTimer = setInterval(() => {
            const elapsed = Math.floor((Date.now() - this.recordingStartTime) / 1000);
            const mins = Math.floor(elapsed / 60);
            const secs = elapsed % 60;
            this.recordingUI.querySelector('.page-chat-rec-timer').textContent = 
                `${mins}:${secs.toString().padStart(2, '0')}`;
        }, 1000);

        this.recordingUI.querySelector('.page-chat-rec-cancel').addEventListener('click', () => {
            this.cancelRecording();
        });

        this.recordingUI.querySelector('.page-chat-rec-stop').addEventListener('click', () => {
            this.stopRecording();
        });
    }

    cancelRecording() {
        if (this.mediaRecorder && this.mediaRecorder.state !== 'inactive') {
            this.mediaRecorder.stop();
        }
        clearInterval(this.recordingTimer);
        this.recordedBlob = null;
        this.hideRecordingUI();
    }

    stopRecording() {
        if (this.mediaRecorder && this.mediaRecorder.state !== 'inactive') {
            this.mediaRecorder.stop();
        }
        clearInterval(this.recordingTimer);
    }

    hideRecordingUI() {
        this.recordingUI.classList.add('hidden');
        this.previewUI.classList.add('hidden');
        this.messageForm.classList.remove('hidden');
        this.mediaContainer.classList.remove('hidden');
    }

    showVoicePreview() {
        this.recordingUI.classList.add('hidden');
        this.previewUI.classList.remove('hidden');

        const duration = Math.floor((Date.now() - this.recordingStartTime) / 1000);
        const mins = Math.floor(duration / 60);
        const secs = duration % 60;
        const barCount = 20;

        const bars = Array.from({ length: barCount }, () => 
            `<div class="page-chat-wave-bar" style="height: ${Math.random() * 60 + 20}%"></div>`
        ).join('');

        this.previewUI.innerHTML = `
            <button type="button" class="page-chat-prev-cancel" title="Cancel">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <line x1="18" y1="6" x2="6" y2="18"></line>
                    <line x1="6" y1="6" x2="18" y2="18"></line>
                </svg>
            </button>
            <button type="button" class="page-chat-prev-play" title="Play">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
                    <path d="M8 5v14l11-7z"/>
                </svg>
            </button>
            <div class="page-chat-waveform">
                <div class="page-chat-wave-bars">${bars}</div>
            </div>
            <span class="page-chat-prev-duration">${mins}:${secs.toString().padStart(2, '0')}</span>
            <button type="button" class="page-chat-prev-send" title="Send">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
                    <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/>
                </svg>
            </button>
        `;

        const audioUrl = URL.createObjectURL(this.recordedBlob);
        const audio = new Audio(audioUrl);
        const playBtn = this.previewUI.querySelector('.page-chat-prev-play');
        const durationEl = this.previewUI.querySelector('.page-chat-prev-duration');
        const waveformBars = this.previewUI.querySelectorAll('.page-chat-wave-bar');
        let playbackInterval = null;
        const totalDuration = duration; // Use recorded duration

        // Helper to format duration
        const formatDuration = (seconds) => {
            const m = Math.floor(seconds / 60);
            const s = Math.floor(seconds % 60);
            return `${m}:${s.toString().padStart(2, '0')}`;
        };

        // Helper to update playback UI
        const updatePlaybackUI = () => {
            if (audio && durationEl && audio.duration && !isNaN(audio.duration)) {
                // Show remaining time (countdown)
                const remaining = Math.max(0, Math.ceil(audio.duration - audio.currentTime));
                durationEl.textContent = formatDuration(remaining);
                
                // Update waveform progress
                const progress = audio.currentTime / audio.duration;
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

        // Helper to reset UI
        const resetUI = () => {
            durationEl.textContent = formatDuration(totalDuration);
            waveformBars.forEach(bar => bar.classList.remove('active'));
            playBtn.innerHTML = '<svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor"><path d="M8 5v14l11-7z"/></svg>';
        };

        this.previewUI.querySelector('.page-chat-prev-cancel').addEventListener('click', () => {
            if (playbackInterval) clearInterval(playbackInterval);
            audio.pause();
            this.recordedBlob = null;
            this.hideRecordingUI();
        });

        playBtn.addEventListener('click', () => {
            if (audio.paused) {
                audio.play();
                playBtn.innerHTML = '<svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor"><rect x="6" y="4" width="4" height="16"/><rect x="14" y="4" width="4" height="16"/></svg>';
                // Start interval to update waveform animation
                playbackInterval = setInterval(updatePlaybackUI, 50);
            } else {
                audio.pause();
                if (playbackInterval) clearInterval(playbackInterval);
                playBtn.innerHTML = '<svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor"><path d="M8 5v14l11-7z"/></svg>';
            }
        });

        audio.addEventListener('ended', () => {
            if (playbackInterval) clearInterval(playbackInterval);
            audio.currentTime = 0;
            resetUI();
        });

        this.previewUI.querySelector('.page-chat-prev-send').addEventListener('click', () => {
            if (playbackInterval) clearInterval(playbackInterval);
            audio.pause();
            this.sendVoiceMessage();
        });
    }

    async sendVoiceMessage() {
        if (!this.recordedBlob) return;

        const conversation = this.getSelectedConversation();
        const connection = this.getSignalRConnection();
        
        if (!conversation) {
            alert('Please select a conversation first');
            return;
        }
        
        if (!connection || connection.state !== 'Connected') {
            alert('Connection lost. Please refresh the page and try again.');
            return;
        }

        try {
            const formData = new FormData();
            formData.append('file', this.recordedBlob, 'voice-message.webm');

            const response = await fetch(`${this.apiUrl}/chat/attachments`, {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${this.accessToken}` },
                body: formData
            });

            if (!response.ok) throw new Error('Upload failed');

            const { data } = await response.json();

            // Force type to 'audio' for voice messages (Cloudinary returns 'video' for webm)
            await connection.invoke('SendMessageWithAttachmentAsync',
                conversation.id,
                conversation.otherParticipant.userId,
                null,
                null,
                data.url,
                'audio',  // Force audio type for voice messages
                data.publicId,
                data.thumbnailUrl,
                'audio/webm',  // Force audio mimeType
                'voice-message.webm',  // Clear filename
                data.fileSize
            );

            this.recordedBlob = null;
            this.hideRecordingUI();
            this.updateSendLikeButton();

        } catch (error) {
            console.error('Error sending voice message:', error);
            alert('Failed to send voice message. Please check your connection.');
        }
    }

    sendLikeEmoji() {
        // Send like emoji directly without changing input
        this.messageInput.value = '👍';
        this.messageForm.dispatchEvent(new Event('submit'));
        // Reset input and button state after sending
        this.messageInput.value = '';
        this.updateSendLikeButton();
    }

    closePickers() {
        document.querySelectorAll('.page-chat-emoji-picker, .page-chat-gif-picker, .page-chat-product-picker').forEach(p => p.remove());
    }

    // ==================== PRODUCT PICKER ====================
    toggleProductPicker() {
        const existingPicker = document.querySelector('.page-chat-product-picker');
        if (existingPicker) {
            existingPicker.remove();
            return;
        }

        this.closePickers();

        // Provider always has tabs to choose between own products and all products
        const conversation = this.getSelectedConversation();
        const currentRole = this.currentUserRole?.toLowerCase();
        const showTabs = currentRole === 'provider';

        const picker = document.createElement('div');
        picker.className = 'page-chat-product-picker';
        picker.innerHTML = `
            <div class="page-chat-product-header">
                <span class="page-chat-product-title">Select a Product</span>
                <button type="button" class="page-chat-product-close" title="Close">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <line x1="18" y1="6" x2="6" y2="18"></line>
                        <line x1="6" y1="6" x2="18" y2="18"></line>
                    </svg>
                </button>
            </div>
            ${showTabs ? `
            <div class="page-chat-product-tabs">
                <button type="button" class="page-chat-product-tab active" data-source="my">My Products</button>
                <button type="button" class="page-chat-product-tab" data-source="all">All Products</button>
            </div>
            ` : ''}
            <div class="page-chat-product-search">
                <input type="text" class="page-chat-product-input" placeholder="Search products..." autocomplete="off">
            </div>
            <div class="page-chat-product-content">
                <div class="page-chat-product-list">
                    <div class="page-chat-product-loading">
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M21 12a9 9 0 1 1-6.219-8.56"></path>
                        </svg>
                        <div>Loading products...</div>
                    </div>
                </div>
            </div>
        `;

        this.messageForm.parentNode.insertBefore(picker, this.messageForm);

        const searchInput = picker.querySelector('.page-chat-product-input');
        const closeBtn = picker.querySelector('.page-chat-product-close');
        const listContainer = picker.querySelector('.page-chat-product-list');
        const tabs = picker.querySelectorAll('.page-chat-product-tab');

        let currentPage = 1;
        let totalCount = 0;
        let isLoading = false;
        let searchTimeout = null;
        let currentSource = 'my'; // 'my' for own products, 'all' for browse products

        const loadProducts = async (searchTerm = '', page = 1, append = false) => {
            if (isLoading) return;
            isLoading = true;

            if (!append) {
                listContainer.innerHTML = `
                    <div class="page-chat-product-loading">
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
                const convo = this.getSelectedConversation();
                if (convo?.otherParticipant?.userId) {
                    params.append('recipientId', convo.otherParticipant.userId);
                }
                if (convo?.otherParticipant?.role) {
                    params.append('recipientRole', convo.otherParticipant.role);
                }
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
                    const loadMoreBtn = listContainer.querySelector('.page-chat-product-load-more');
                    if (loadMoreBtn) loadMoreBtn.remove();
                }

                if (data.items && data.items.length > 0) {
                    data.items.forEach(product => {
                        const item = document.createElement('div');
                        item.className = 'page-chat-product-item';
                        item.dataset.productId = product.id;
                        
                        // Build price display based on available options
                        let priceHtml = '';
                        if (product.pricePerDay && product.pricePerDay > 0) {
                            priceHtml += `<span class="page-chat-product-price">₫${product.pricePerDay.toLocaleString()}/day</span>`;
                        }
                        if (product.purchasePrice && product.purchasePrice > 0) {
                            priceHtml += `<span class="page-chat-product-price page-chat-product-buy-price">₫${product.purchasePrice.toLocaleString()}</span>`;
                        }
                        if (!priceHtml) {
                            priceHtml = '<span class="page-chat-product-price">Price not set</span>';
                        }
                        
                        item.innerHTML = `
                            <img class="page-chat-product-image" src="${this.escapeHtml(product.imageUrl || '/images/placeholder.png')}" alt="${this.escapeHtml(product.name)}" onerror="this.src='/images/placeholder.png'">
                            <div class="page-chat-product-info">
                                <div class="page-chat-product-name">${this.escapeHtml(product.name)}</div>
                                <div class="page-chat-product-details">
                                    ${priceHtml}
                                    ${product.category ? `<span class="page-chat-product-category">${this.escapeHtml(product.category)}</span>` : ''}
                                </div>
                            </div>
                        `;
                        item.addEventListener('click', () => {
                            this.selectProduct({
                                id: product.id,
                                name: product.name,
                                imageUrl: product.imageUrl
                            });
                            picker.remove();
                        });
                        listContainer.appendChild(item);
                    });

                    const loadedCount = page * 10;
                    if (loadedCount < totalCount) {
                        const loadMoreBtn = document.createElement('button');
                        loadMoreBtn.className = 'page-chat-product-load-more';
                        loadMoreBtn.textContent = `Load more (${totalCount - loadedCount} remaining)`;
                        loadMoreBtn.addEventListener('click', () => {
                            loadProducts(searchInput.value.trim(), currentPage + 1, true);
                        });
                        listContainer.appendChild(loadMoreBtn);
                    }
                } else if (!append) {
                    listContainer.innerHTML = `
                        <div class="page-chat-product-empty">
                            No products found. Try a different search.
                        </div>
                    `;
                }
            } catch (error) {
                console.error('Error loading products:', error);
                if (!append) {
                    listContainer.innerHTML = `
                        <div class="page-chat-product-empty">
                            Failed to load products. Please try again.
                        </div>
                    `;
                }
            } finally {
                isLoading = false;
            }
        };

        loadProducts();

        // Tab handlers (for Provider chatting with Staff/Admin)
        if (tabs.length > 0) {
            tabs.forEach(tab => {
                tab.addEventListener('click', (e) => {
                    e.stopPropagation();
                    const source = tab.dataset.source;
                    if (source === currentSource) return;
                    
                    // Update active tab
                    tabs.forEach(t => t.classList.remove('active'));
                    tab.classList.add('active');
                    
                    // Update source and reload
                    currentSource = source;
                    searchInput.value = '';
                    loadProducts('', 1, false);
                });
            });
        }

        searchInput.addEventListener('input', (e) => {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(() => {
                loadProducts(e.target.value.trim(), 1, false);
            }, 300);
        });

        searchInput.addEventListener('click', (e) => e.stopPropagation());

        closeBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            picker.remove();
        });

        setTimeout(() => searchInput.focus(), 100);

        const closePicker = (e) => {
            if (!picker.contains(e.target) && e.target !== this.productBtn && !this.productBtn.contains(e.target)) {
                picker.remove();
                document.removeEventListener('click', closePicker);
            }
        };
        setTimeout(() => document.addEventListener('click', closePicker), 10);
    }

    selectProduct(product) {
        this.selectedProduct = product;
        this.showSelectedProductIndicator(product);
    }

    showSelectedProductIndicator(product) {
        // Remove existing indicator
        const existingIndicator = document.querySelector('.page-chat-selected-product');
        if (existingIndicator) existingIndicator.remove();

        const indicator = document.createElement('div');
        indicator.className = 'page-chat-selected-product';
        indicator.innerHTML = `
            <img class="page-chat-selected-image" src="${this.escapeHtml(product.imageUrl || '/images/placeholder.png')}" alt="${this.escapeHtml(product.name)}" onerror="this.src='/images/placeholder.png'">
            <div class="page-chat-selected-info">
                <div class="page-chat-selected-label">Attaching product:</div>
                <div class="page-chat-selected-name">${this.escapeHtml(product.name)}</div>
            </div>
            <button type="button" class="page-chat-selected-remove" title="Remove">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <line x1="18" y1="6" x2="6" y2="18"></line>
                    <line x1="6" y1="6" x2="18" y2="18"></line>
                </svg>
            </button>
        `;

        this.mediaContainer.parentNode.insertBefore(indicator, this.mediaContainer);

        indicator.querySelector('.page-chat-selected-remove').addEventListener('click', () => {
            this.selectedProduct = null;
            indicator.remove();
        });
    }

    getSelectedProduct() {
        return this.selectedProduct;
    }

    clearSelectedProduct() {
        this.selectedProduct = null;
        const indicator = document.querySelector('.page-chat-selected-product');
        if (indicator) indicator.remove();
    }

    escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}

// Export for use in pages
window.ChatMediaFeatures = ChatMediaFeatures;
