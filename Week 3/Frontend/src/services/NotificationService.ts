export class NotificationService {
  private cartItemCount: number = 0;
  private isInitialized: boolean = false;
  private notificationTimeout: number | null = null;
  private visibilityChangeHandler: (event: Event) => void;
  private debounceTimeout: number | null = null;
  private maxCartItems: number = 999999; // Prevent integer overflow
  private notificationCooldown: number = 30000; // 30 seconds between notifications
  private lastNotificationTime: number = 0;
  private retryAttempts: number = 0;
  private maxRetryAttempts: number = 3;
  private isDestroyed: boolean = false;

  constructor() {
    this.visibilityChangeHandler = this.handleVisibilityChange.bind(this);
  }

  /**
   * Initialize the notification service with enhanced error handling
   */
  async initialize(): Promise<void> {
    if (this.isInitialized || this.isDestroyed) {
      return;
    }

    try {
      if (!NotificationService.isNotificationSupported()) {
        console.warn('Browser notifications are not supported');
        return;
      }

      // Check if we're in a secure context (HTTPS or localhost)
      if (!this.isSecureContext()) {
        console.warn('Notifications require secure context (HTTPS or localhost)');
        return;
      }

      const permission = await this.requestPermissionWithRetry();
      
      if (permission === 'granted') {
        this.setupEventListeners();
        this.isInitialized = true;
        this.retryAttempts = 0; // Reset retry attempts on success
      } else if (permission === 'denied') {
        console.warn('Notification permission denied by user');
        this.handlePermissionDenied();
      } else {
        console.warn('Notification permission request was dismissed');
      }
    } catch (error) {
      console.error('Failed to initialize notification service:', error);
      this.handleInitializationError(error);
    }
  }

  /**
   * Add an item to the cart with boundary checking
   */
  addItemToCart(): void {
    if (this.isDestroyed) {
      console.warn('Cannot add item to cart: service is destroyed');
      return;
    }

    if (this.cartItemCount >= this.maxCartItems) {
      console.warn(`Cart item limit reached (${this.maxCartItems})`);
      return;
    }

    this.cartItemCount++;
    this.validateCartState();
  }

  /**
   * Remove an item from the cart with boundary checking
   */
  removeItemFromCart(): void {
    if (this.isDestroyed) {
      console.warn('Cannot remove item from cart: service is destroyed');
      return;
    }

    this.cartItemCount = Math.max(0, this.cartItemCount - 1);
    this.validateCartState();
  }

  /**
   * Clear all items from the cart
   */
  clearCart(): void {
    if (this.isDestroyed) {
      console.warn('Cannot clear cart: service is destroyed');
      return;
    }

    this.cartItemCount = 0;
    this.validateCartState();
  }

  /**
   * Get the current number of items in the cart
   */
  getCartItemCount(): number {
    return this.cartItemCount;
  }

  /**
   * Clean up event listeners and timeouts with enhanced cleanup
   */
  cleanup(): void {
    this.isDestroyed = true;
    this.isInitialized = false;

    // Clear all timeouts
    if (this.notificationTimeout) {
      clearTimeout(this.notificationTimeout);
      this.notificationTimeout = null;
    }

    if (this.debounceTimeout) {
      clearTimeout(this.debounceTimeout);
      this.debounceTimeout = null;
    }

    // Remove event listeners
    try {
      document.removeEventListener('visibilitychange', this.visibilityChangeHandler);
    } catch (error) {
      console.warn('Error removing visibility change listener:', error);
    }

    // Reset state
    this.cartItemCount = 0;
    this.lastNotificationTime = 0;
    this.retryAttempts = 0;
  }

  /**
   * Check if browser notifications are supported
   */
  static isNotificationSupported(): boolean {
    return typeof window !== 'undefined' && 
           'Notification' in window && 
           typeof Notification !== 'undefined';
  }

  /**
   * Check if we're in a secure context
   */
  private isSecureContext(): boolean {
    return typeof window !== 'undefined' && 
           (window.isSecureContext || 
            window.location.protocol === 'https:' || 
            window.location.hostname === 'localhost' ||
            window.location.hostname === '127.0.0.1');
  }

  /**
   * Request permission with retry mechanism
   */
  private async requestPermissionWithRetry(): Promise<NotificationPermission> {
    if (!NotificationService.isNotificationSupported()) {
      throw new Error('Notification API not supported');
    }

    try {
      return await Notification.requestPermission();
    } catch (error) {
      this.retryAttempts++;
      
      if (this.retryAttempts < this.maxRetryAttempts) {
        console.warn(`Permission request failed, retrying... (${this.retryAttempts}/${this.maxRetryAttempts})`);
        await this.delay(1000 * this.retryAttempts); // Exponential backoff
        return this.requestPermissionWithRetry();
      } else {
        throw new Error(`Failed to request notification permission after ${this.maxRetryAttempts} attempts`);
      }
    }
  }

  /**
   * Set up event listeners with error handling
   */
  private setupEventListeners(): void {
    try {
      if (typeof document !== 'undefined' && document.addEventListener) {
        document.addEventListener('visibilitychange', this.visibilityChangeHandler);
      } else {
        throw new Error('Document API not available');
      }
    } catch (error) {
      console.error('Failed to setup event listeners:', error);
      throw error;
    }
  }

  /**
   * Handle visibility change events with debouncing
   */
  private handleVisibilityChange(event: Event): void {
    if (this.isDestroyed || !this.isInitialized) {
      return;
    }

    // Debounce rapid visibility changes
    if (this.debounceTimeout) {
      clearTimeout(this.debounceTimeout);
    }

    this.debounceTimeout = window.setTimeout(() => {
      this.processVisibilityChange();
    }, 100); // 100ms debounce
  }

  /**
   * Process visibility change with cooldown
   */
  private processVisibilityChange(): void {
    if (this.isDestroyed || !this.isInitialized) {
      return;
    }

    const now = Date.now();
    
    // Check cooldown period
    if (now - this.lastNotificationTime < this.notificationCooldown) {
      return;
    }

    if (document.hidden && this.cartItemCount > 0) {
      this.showCartReminderNotification();
      this.lastNotificationTime = now;
    }
  }

  /**
   * Show the cart reminder notification with security measures
   */
  private showCartReminderNotification(): void {
    if (!this.isInitialized || this.cartItemCount === 0 || this.isDestroyed) {
      return;
    }

    try {
      // Sanitize notification content to prevent XSS
      const title = this.sanitizeString('Shopping Cart Reminder');
      const body = this.sanitizeString('Your items are waiting, please place order before item goes out of stock');
      
      const notification = new Notification(title, {
        body: body,
        icon: '/favicon.ico',
        tag: 'cart-reminder',
        requireInteraction: true,
        silent: false,
        badge: '/favicon.ico',
        data: {
          timestamp: Date.now(),
          cartItemCount: this.cartItemCount
        }
      });

      // Handle notification events
      notification.onclick = () => {
        window.focus();
        notification.close();
      };

      notification.onclose = () => {
        // Cleanup notification reference
      };

      notification.onerror = (error) => {
        console.error('Notification error:', error);
      };

    } catch (error) {
      console.error('Failed to show notification:', error);
      this.handleNotificationError(error);
    }
  }

  /**
   * Sanitize string to prevent XSS
   */
  private sanitizeString(str: string): string {
    if (typeof str !== 'string') {
      return '';
    }
    
    // Remove potentially dangerous characters
    return str
      .replace(/[<>]/g, '') // Remove < and >
      .replace(/javascript:/gi, '') // Remove javascript: protocol
      .replace(/on\w+=/gi, '') // Remove event handlers
      .trim();
  }

  /**
   * Validate cart state for consistency
   */
  private validateCartState(): void {
    if (this.cartItemCount < 0) {
      console.warn('Cart item count cannot be negative, resetting to 0');
      this.cartItemCount = 0;
    }

    if (this.cartItemCount > this.maxCartItems) {
      console.warn(`Cart item count exceeded maximum, capping at ${this.maxCartItems}`);
      this.cartItemCount = this.maxCartItems;
    }
  }

  /**
   * Handle permission denied scenario
   */
  private handlePermissionDenied(): void {
    // Could implement alternative notification methods here
    // e.g., in-app notifications, email reminders, etc.
    console.info('Notification permission denied - consider implementing fallback notification methods');
  }

  /**
   * Handle initialization errors
   */
  private handleInitializationError(error: any): void {
    console.error('Notification service initialization failed:', error);
    
    // Could implement fallback notification system here
    // e.g., localStorage-based reminders, periodic checks, etc.
  }

  /**
   * Handle notification errors
   */
  private handleNotificationError(error: any): void {
    console.error('Notification display failed:', error);
    
    // Could implement alternative notification methods here
    // e.g., console warnings, DOM-based notifications, etc.
  }

  /**
   * Utility function for delays
   */
  private delay(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }

  /**
   * Get service status for debugging
   */
  getStatus(): {
    isInitialized: boolean;
    isDestroyed: boolean;
    cartItemCount: number;
    retryAttempts: number;
    lastNotificationTime: number;
  } {
    return {
      isInitialized: this.isInitialized,
      isDestroyed: this.isDestroyed,
      cartItemCount: this.cartItemCount,
      retryAttempts: this.retryAttempts,
      lastNotificationTime: this.lastNotificationTime
    };
  }
} 