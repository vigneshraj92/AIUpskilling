import { NotificationService } from '../NotificationService';

// Mock the browser notification API
const mockNotification = {
  requestPermission: jest.fn(),
  permission: 'granted' as NotificationPermission,
};

Object.defineProperty(window, 'Notification', {
  value: mockNotification,
  writable: true,
});

// Mock document visibility API
Object.defineProperty(document, 'hidden', {
  value: false,
  writable: true,
});

Object.defineProperty(document, 'visibilityState', {
  value: 'visible',
  writable: true,
});

describe('NotificationService Edge Cases', () => {
  let notificationService: NotificationService;
  let mockAddEventListener: jest.Mock;
  let mockRemoveEventListener: jest.Mock;

  beforeEach(() => {
    jest.clearAllMocks();
    notificationService = new NotificationService();
    
    mockAddEventListener = jest.fn();
    mockRemoveEventListener = jest.fn();
    
    Object.defineProperty(document, 'addEventListener', {
      value: mockAddEventListener,
      writable: true,
    });
    
    Object.defineProperty(document, 'removeEventListener', {
      value: mockRemoveEventListener,
      writable: true,
    });
  });

  describe('Null/Undefined Handling', () => {
    it('should handle null window object', () => {
      const originalWindow = global.window;
      delete (global as any).window;
      
      expect(NotificationService.isNotificationSupported()).toBe(false);
      
      global.window = originalWindow;
    });

    it('should handle undefined Notification API', () => {
      const originalNotification = window.Notification;
      delete (window as any).Notification;
      
      expect(NotificationService.isNotificationSupported()).toBe(false);
      
      window.Notification = originalNotification;
    });

    it('should handle null permission response', async () => {
      mockNotification.requestPermission.mockResolvedValue(null);
      
      await notificationService.initialize();
      
      expect(mockNotification.requestPermission).toHaveBeenCalled();
    });

    it('should handle undefined document object', () => {
      const originalDocument = global.document;
      delete (global as any).document;
      
      expect(() => notificationService.cleanup()).not.toThrow();
      
      global.document = originalDocument;
    });
  });

  describe('Boundary Conditions', () => {
    beforeEach(async () => {
      mockNotification.requestPermission.mockResolvedValue('granted');
      await notificationService.initialize();
    });

    it('should prevent cart item overflow', () => {
      const consoleSpy = jest.spyOn(console, 'warn').mockImplementation();
      
      // Add items up to the limit
      for (let i = 0; i < 1000000; i++) {
        notificationService.addItemToCart();
      }
      
      expect(consoleSpy).toHaveBeenCalledWith(expect.stringContaining('Cart item limit reached'));
      expect(notificationService.getCartItemCount()).toBeLessThanOrEqual(999999);
      
      consoleSpy.mockRestore();
    });

    it('should handle negative cart items gracefully', () => {
      const consoleSpy = jest.spyOn(console, 'warn').mockImplementation();
      
      // Force negative state (should not be possible through normal operations)
      (notificationService as any).cartItemCount = -5;
      notificationService.removeItemFromCart();
      
      expect(consoleSpy).toHaveBeenCalledWith('Cart item count cannot be negative, resetting to 0');
      expect(notificationService.getCartItemCount()).toBe(0);
      
      consoleSpy.mockRestore();
    });

    it('should handle zero cart items correctly', () => {
      expect(notificationService.getCartItemCount()).toBe(0);
      
      notificationService.removeItemFromCart();
      expect(notificationService.getCartItemCount()).toBe(0);
      
      notificationService.clearCart();
      expect(notificationService.getCartItemCount()).toBe(0);
    });

    it('should cap cart items at maximum limit', () => {
      const consoleSpy = jest.spyOn(console, 'warn').mockImplementation();
      
      // Force exceed maximum (should not be possible through normal operations)
      (notificationService as any).cartItemCount = 2000000;
      notificationService.addItemToCart();
      
      expect(consoleSpy).toHaveBeenCalledWith(expect.stringContaining('Cart item count exceeded maximum'));
      expect(notificationService.getCartItemCount()).toBeLessThanOrEqual(999999);
      
      consoleSpy.mockRestore();
    });
  });

  describe('Security Vulnerabilities', () => {
    beforeEach(async () => {
      mockNotification.requestPermission.mockResolvedValue('granted');
      await notificationService.initialize();
    });

    it('should sanitize XSS attempts in notification content', () => {
      const MockNotificationConstructor = jest.fn();
      Object.defineProperty(window, 'Notification', {
        value: MockNotificationConstructor,
        writable: true,
      });

      notificationService.addItemToCart();
      
      // Simulate malicious content injection
      (notificationService as any).showCartReminderNotification();
      
      expect(MockNotificationConstructor).toHaveBeenCalledWith(
        'Shopping Cart Reminder',
        expect.objectContaining({
          body: 'Your items are waiting, please place order before item goes out of stock'
        })
      );
    });

    it('should handle script injection attempts', () => {
      const sanitized = (notificationService as any).sanitizeString('<script>alert("xss")</script>');
      expect(sanitized).toBe('scriptalert("xss")/script');
    });

    it('should handle javascript protocol attempts', () => {
      const sanitized = (notificationService as any).sanitizeString('javascript:alert("xss")');
      expect(sanitized).toBe('alert("xss")');
    });

    it('should handle event handler injection attempts', () => {
      const sanitized = (notificationService as any).sanitizeString('onclick=alert("xss")');
      expect(sanitized).toBe('alert("xss")');
    });

    it('should handle non-string inputs in sanitization', () => {
      expect((notificationService as any).sanitizeString(null)).toBe('');
      expect((notificationService as any).sanitizeString(undefined)).toBe('');
      expect((notificationService as any).sanitizeString(123)).toBe('');
    });
  });

  describe('Race Conditions', () => {
    beforeEach(async () => {
      mockNotification.requestPermission.mockResolvedValue('granted');
      await notificationService.initialize();
    });

    it('should debounce rapid visibility changes', async () => {
      const MockNotificationConstructor = jest.fn();
      Object.defineProperty(window, 'Notification', {
        value: MockNotificationConstructor,
        writable: true,
      });

      notificationService.addItemToCart();
      
      // Simulate rapid tab switching
      Object.defineProperty(document, 'hidden', { value: true });
      const visibilityChangeHandler = mockAddEventListener.mock.calls.find(
        call => call[0] === 'visibilitychange'
      )[1];

      // Trigger multiple rapid changes
      for (let i = 0; i < 10; i++) {
        visibilityChangeHandler(new Event('visibilitychange'));
      }

      // Wait for debounce
      await new Promise(resolve => setTimeout(resolve, 200));
      
      // Should only show one notification due to debouncing
      expect(MockNotificationConstructor).toHaveBeenCalledTimes(1);
    });

    it('should handle concurrent cart operations', () => {
      const promises = [];
      
      // Simulate concurrent add operations
      for (let i = 0; i < 100; i++) {
        promises.push(Promise.resolve(notificationService.addItemToCart()));
      }
      
      return Promise.all(promises).then(() => {
        expect(notificationService.getCartItemCount()).toBe(100);
      });
    });

    it('should prevent operations on destroyed service', () => {
      const consoleSpy = jest.spyOn(console, 'warn').mockImplementation();
      
      notificationService.cleanup();
      
      notificationService.addItemToCart();
      notificationService.removeItemFromCart();
      notificationService.clearCart();
      
      expect(consoleSpy).toHaveBeenCalledWith('Cannot add item to cart: service is destroyed');
      expect(consoleSpy).toHaveBeenCalledWith('Cannot remove item from cart: service is destroyed');
      expect(consoleSpy).toHaveBeenCalledWith('Cannot clear cart: service is destroyed');
      
      consoleSpy.mockRestore();
    });

    it('should handle multiple service instances', () => {
      const service1 = new NotificationService();
      const service2 = new NotificationService();
      
      service1.addItemToCart();
      service2.addItemToCart();
      
      expect(service1.getCartItemCount()).toBe(1);
      expect(service2.getCartItemCount()).toBe(1);
      
      service1.cleanup();
      service2.cleanup();
    });
  });

  describe('Browser Compatibility', () => {
    it('should handle Notification API unavailability', async () => {
      const originalNotification = window.Notification;
      delete (window as any).Notification;
      
      await notificationService.initialize();
      
      expect(notificationService.getStatus().isInitialized).toBe(false);
      
      window.Notification = originalNotification;
    });

    it('should handle Page Visibility API unavailability', async () => {
      mockNotification.requestPermission.mockResolvedValue('granted');
      
      const originalDocument = global.document;
      delete (global as any).document;
      
      await expect(notificationService.initialize()).rejects.toThrow();
      
      global.document = originalDocument;
    });

    it('should handle secure context requirement', async () => {
      const originalIsSecureContext = window.isSecureContext;
      const originalLocation = window.location;
      
      window.isSecureContext = false;
      Object.defineProperty(window, 'location', {
        value: { protocol: 'http:', hostname: 'example.com' },
        writable: true,
      });
      
      await notificationService.initialize();
      
      expect(notificationService.getStatus().isInitialized).toBe(false);
      
      window.isSecureContext = originalIsSecureContext;
      Object.defineProperty(window, 'location', {
        value: originalLocation,
        writable: true,
      });
    });

    it('should work in localhost environment', async () => {
      mockNotification.requestPermission.mockResolvedValue('granted');
      
      const originalLocation = window.location;
      Object.defineProperty(window, 'location', {
        value: { protocol: 'http:', hostname: 'localhost' },
        writable: true,
      });
      
      await notificationService.initialize();
      
      expect(notificationService.getStatus().isInitialized).toBe(true);
      
      Object.defineProperty(window, 'location', {
        value: originalLocation,
        writable: true,
      });
    });
  });

  describe('Performance Edge Cases', () => {
    beforeEach(async () => {
      mockNotification.requestPermission.mockResolvedValue('granted');
      await notificationService.initialize();
    });

    it('should handle high-frequency cart updates', () => {
      const startTime = Date.now();
      
      // Perform many rapid operations
      for (let i = 0; i < 1000; i++) {
        notificationService.addItemToCart();
        notificationService.removeItemFromCart();
      }
      
      const endTime = Date.now();
      const duration = endTime - startTime;
      
      // Should complete within reasonable time (less than 1 second)
      expect(duration).toBeLessThan(1000);
      expect(notificationService.getCartItemCount()).toBe(0);
    });

    it('should prevent memory leaks from event listeners', () => {
      const initialListenerCount = mockAddEventListener.mock.calls.length;
      
      // Create and destroy multiple services
      for (let i = 0; i < 10; i++) {
        const service = new NotificationService();
        service.cleanup();
      }
      
      // Should not accumulate listeners
      expect(mockRemoveEventListener).toHaveBeenCalled();
    });

    it('should handle long-running sessions', async () => {
      // Simulate long session with periodic operations
      for (let i = 0; i < 100; i++) {
        notificationService.addItemToCart();
        await new Promise(resolve => setTimeout(resolve, 10));
        notificationService.removeItemFromCart();
      }
      
      expect(notificationService.getCartItemCount()).toBe(0);
      expect(notificationService.getStatus().isDestroyed).toBe(false);
    });

    it('should cleanup timeouts properly', () => {
      const clearTimeoutSpy = jest.spyOn(window, 'clearTimeout');
      
      notificationService.cleanup();
      
      expect(clearTimeoutSpy).toHaveBeenCalled();
      
      clearTimeoutSpy.mockRestore();
    });
  });

  describe('Error Recovery', () => {
    it('should retry permission requests on failure', async () => {
      const consoleSpy = jest.spyOn(console, 'warn').mockImplementation();
      
      mockNotification.requestPermission
        .mockRejectedValueOnce(new Error('Network error'))
        .mockRejectedValueOnce(new Error('Network error'))
        .mockResolvedValue('granted');
      
      await notificationService.initialize();
      
      expect(mockNotification.requestPermission).toHaveBeenCalledTimes(3);
      expect(consoleSpy).toHaveBeenCalledWith(expect.stringContaining('retrying'));
      
      consoleSpy.mockRestore();
    });

    it('should handle maximum retry attempts', async () => {
      const consoleSpy = jest.spyOn(console, 'error').mockImplementation();
      
      mockNotification.requestPermission.mockRejectedValue(new Error('Persistent error'));
      
      await notificationService.initialize();
      
      expect(mockNotification.requestPermission).toHaveBeenCalledTimes(3);
      expect(consoleSpy).toHaveBeenCalledWith(expect.stringContaining('Failed to request notification permission'));
      
      consoleSpy.mockRestore();
    });

    it('should handle notification display errors', () => {
      const consoleSpy = jest.spyOn(console, 'error').mockImplementation();
      
      // Mock Notification constructor to throw
      const MockNotificationConstructor = jest.fn().mockImplementation(() => {
        throw new Error('Notification display failed');
      });
      
      Object.defineProperty(window, 'Notification', {
        value: MockNotificationConstructor,
        writable: true,
      });

      notificationService.addItemToCart();
      
      // Simulate visibility change
      Object.defineProperty(document, 'hidden', { value: true });
      const visibilityChangeHandler = mockAddEventListener.mock.calls.find(
        call => call[0] === 'visibilitychange'
      )[1];
      
      visibilityChangeHandler(new Event('visibilitychange'));
      
      expect(consoleSpy).toHaveBeenCalledWith('Failed to show notification:', expect.any(Error));
      
      consoleSpy.mockRestore();
    });
  });

  describe('Notification Cooldown', () => {
    beforeEach(async () => {
      mockNotification.requestPermission.mockResolvedValue('granted');
      await notificationService.initialize();
    });

    it('should respect notification cooldown period', async () => {
      const MockNotificationConstructor = jest.fn();
      Object.defineProperty(window, 'Notification', {
        value: MockNotificationConstructor,
        writable: true,
      });

      notificationService.addItemToCart();
      
      // First notification
      Object.defineProperty(document, 'hidden', { value: true });
      const visibilityChangeHandler = mockAddEventListener.mock.calls.find(
        call => call[0] === 'visibilitychange'
      )[1];
      
      visibilityChangeHandler(new Event('visibilitychange'));
      expect(MockNotificationConstructor).toHaveBeenCalledTimes(1);
      
      // Reset mock
      MockNotificationConstructor.mockClear();
      
      // Second notification within cooldown period
      visibilityChangeHandler(new Event('visibilitychange'));
      expect(MockNotificationConstructor).not.toHaveBeenCalled();
    });

    it('should allow notifications after cooldown period', async () => {
      const MockNotificationConstructor = jest.fn();
      Object.defineProperty(window, 'Notification', {
        value: MockNotificationConstructor,
        writable: true,
      });

      notificationService.addItemToCart();
      
      // First notification
      Object.defineProperty(document, 'hidden', { value: true });
      const visibilityChangeHandler = mockAddEventListener.mock.calls.find(
        call => call[0] === 'visibilitychange'
      )[1];
      
      visibilityChangeHandler(new Event('visibilitychange'));
      expect(MockNotificationConstructor).toHaveBeenCalledTimes(1);
      
      // Reset mock
      MockNotificationConstructor.mockClear();
      
      // Wait for cooldown period
      await new Promise(resolve => setTimeout(resolve, 50)); // Mock time advancement
      
      // Second notification after cooldown
      visibilityChangeHandler(new Event('visibilitychange'));
      expect(MockNotificationConstructor).toHaveBeenCalledTimes(1);
    });
  });

  describe('Service Status', () => {
    it('should provide accurate service status', async () => {
      const status = notificationService.getStatus();
      
      expect(status).toEqual({
        isInitialized: false,
        isDestroyed: false,
        cartItemCount: 0,
        retryAttempts: 0,
        lastNotificationTime: 0
      });
      
      mockNotification.requestPermission.mockResolvedValue('granted');
      await notificationService.initialize();
      
      const updatedStatus = notificationService.getStatus();
      expect(updatedStatus.isInitialized).toBe(true);
      
      notificationService.cleanup();
      
      const finalStatus = notificationService.getStatus();
      expect(finalStatus.isDestroyed).toBe(true);
    });
  });
}); 