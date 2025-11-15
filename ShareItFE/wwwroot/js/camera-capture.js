/**
 * Camera Capture Component for CCCD Verification
 * Provides camera access and photo capture functionality
 */

class CameraCapture {
    constructor(videoElementId, canvasElementId) {
        this.videoElement = document.getElementById(videoElementId);
        this.canvasElement = document.getElementById(canvasElementId);
        this.stream = null;
        this.capturedBlob = null;
    }

    /**
     * Open camera with constraints
     */
    async openCamera(facingMode = 'user') {
        try {
            // Request camera permission
            this.stream = await navigator.mediaDevices.getUserMedia({
                video: {
                    facingMode: facingMode, // 'user' for front camera, 'environment' for back
                    width: { ideal: 1280 },
                    height: { ideal: 720 }
                },
                audio: false
            });

            this.videoElement.srcObject = this.stream;
            console.log('✅ Camera opened successfully');
            return true;

        } catch (error) {
            console.error('❌ Camera error:', error);
            
            let errorMessage = 'Cannot access camera. ';
            if (error.name === 'NotAllowedError') {
                errorMessage += 'Please allow camera permission in your browser.';
            } else if (error.name === 'NotFoundError') {
                errorMessage += 'No camera found on this device.';
            } else {
                errorMessage += 'Error: ' + error.message;
            }
            
            alert(errorMessage);
            return false;
        }
    }

    /**
     * Capture photo from video stream
     */
    capturePhoto() {
        if (!this.stream) {
            alert('Camera not opened yet!');
            return null;
        }

        const context = this.canvasElement.getContext('2d');
        
        // Set canvas size to match video
        this.canvasElement.width = this.videoElement.videoWidth;
        this.canvasElement.height = this.videoElement.videoHeight;
        
        // Flip horizontally to un-mirror the video (since video is mirrored for UX)
        context.translate(this.canvasElement.width, 0);
        context.scale(-1, 1);
        
        // Draw current video frame to canvas
        context.drawImage(
            this.videoElement, 
            0, 0, 
            this.canvasElement.width, 
            this.canvasElement.height
        );
        
        // Reset transformation
        context.setTransform(1, 0, 0, 1, 0, 0);
        
        console.log('📸 Photo captured:', this.canvasElement.width, 'x', this.canvasElement.height);
        
        return this.canvasElement;
    }

    /**
     * Convert canvas to blob
     */
    async canvasToBlob(canvas, quality = 0.92) {
        return new Promise((resolve) => {
            canvas.toBlob((blob) => {
                this.capturedBlob = blob;
                console.log('💾 Blob created:', blob.size, 'bytes');
                resolve(blob);
            }, 'image/jpeg', quality);
        });
    }

    /**
     * Get data URL from canvas
     */
    getDataUrl() {
        return this.canvasElement.toDataURL('image/jpeg', 0.92);
    }

    /**
     * Stop camera stream
     */
    stopCamera() {
        if (this.stream) {
            this.stream.getTracks().forEach(track => {
                track.stop();
                console.log('🛑 Camera track stopped');
            });
            this.stream = null;
            this.videoElement.srcObject = null;
        }
    }

    /**
     * Check if camera is available
     */
    static async isCameraAvailable() {
        try {
            const devices = await navigator.mediaDevices.enumerateDevices();
            const cameras = devices.filter(device => device.kind === 'videoinput');
            return cameras.length > 0;
        } catch (error) {
            console.error('Error checking camera:', error);
            return false;
        }
    }
}

// Export for use in other scripts
window.CameraCapture = CameraCapture;

