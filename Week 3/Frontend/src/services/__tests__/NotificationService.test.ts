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

describe('NotificationService', () => {
  let notificationService: NotificationService;
  let mockAddEventListener: jest.Mock;
  let mockRemoveEventListener: jest.Mock;

  beforeEach(() => {
    jest.clearAllMocks();
    notificationService = new NotificationService();
    
    // Mock event listeners
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

  describe('constructor', () => {
    it('should initialize with default values', () => {
      expect(notificationService['cartItemCount']).toBe(0);
      expect(notificationService['isInitialized']).toBe(false);
      expect(notificationService['notificationTimeout']).toBeNull();
    });
  });

  describe('initialize', () => {
    it('should request notification permission and set up event listeners', async () => {
      mockNotification.requestPermission.mockResolvedValue('granted');
      
      await notificationService.initialize();
      
      expect(mockNotification.requestPermission).toHaveBeenCalled();
      expect(mockAddEventListener).toHaveBeenCalledWith('visibilitychange', expect.any(Function));
      expect(notificationService['isInitialized']).toBe(true);
    });

    it('should handle permission denied gracefully', async () => {
      mockNotification.requestPermission.mockResolvedValue('denied');
      
      await notificationService.initialize();
      
      expect(mockNotification.requestPermission).toHaveBeenCalled();
      expect(notificationService['isInitialized']).toBe(false);
    });

    it('should not initialize twice', async () => {
      mockNotification.requestPermission.mockResolvedValue('granted');
      
      await notificationService.initialize();
      await notificationService.initialize();
      
      expect(mockNotification.requestPermission).toHaveBeenCalledTimes(1);
    });
  });

  describe('addItemToCart', () => {
    beforeEach(async () => {
      mockNotification.requestPermission.mockResolvedValue('granted');
      await notificationService.initialize();
    });

    it('should increment cart item count', () => {
      notificationService.addItemToCart();
      
      expect(notificationService['cartItemCount']).toBe(1);
    });

    it('should increment cart item count multiple times', () => {
      notificationService.addItemToCart();
      notificationService.addItemToCart();
      notificationService.addItemToCart();
      
      expect(notificationService['cartItemCount']).toBe(3);
    });
  });

  describe('removeItemFromCart', () => {
    beforeEach(async () => {
      mockNotification.requestPermission.mockResolvedValue('granted');
      await notificationService.initialize();
      notificationService.addItemToCart();
      notificationService.addItemToCart();
    });

    it('should decrement cart item count', () => {
      notificationService.removeItemFromCart();
      
      expect(notificationService['cartItemCount']).toBe(1);
    });

    it('should not go below zero', () => {
      notificationService.removeItemFromCart();
      notificationService.removeItemFromCart();
      notificationService.removeItemFromCart();
      
      expect(notificationService['cartItemCount']).toBe(0);
    });
  });

  describe('clearCart', () => {
    beforeEach(async () => {
      mockNotification.requestPermission.mockResolvedValue('granted');
      await notificationService.initialize();
      notificationService.addItemToCart();
      notificationService.addItemFromCart();
    });

    it('should reset cart item count to zero', () => {
      notificationService.clearCart();
      
      expect(notificationService['cartItemCount']).toBe(0);
    });
  });

  describe('visibility change handling', () => {
    let visibilityChangeHandler: (event: Event) => void;

    beforeEach(async () => {
      mockNotification.requestPermission.mockResolvedValue('granted');
      await notificationService.initialize();
      
      // Capture the event handler
      visibilityChangeHandler = mockAddEventListener.mock.calls.find(
        call => call[0] === 'visibilitychange'
      )[1];
    });

    it('should show notification when tab becomes hidden with items in cart', () => {
      // Mock notification constructor
      const mockNotificationInstance = {
        close: jest.fn(),
      };
      const MockNotificationConstructor = jest.fn(() => mockNotificationInstance);
      Object.defineProperty(window, 'Notification', {
        value: MockNotificationConstructor,
        writable: true,
      });

      // Add items to cart
      notificationService.addItemToCart();
      
      // Simulate tab becoming hidden
      Object.defineProperty(document, 'hidden', { value: true });
      Object.defineProperty(document, 'visibilityState', { value: 'hidden' });
      
      visibilityChangeHandler(new Event('visibilitychange'));
      
      expect(MockNotificationConstructor).toHaveBeenCalledWith(
        'Shopping Cart Reminder',
        {
          body: 'Your items are waiting, please place order before item goes out of stock',
          icon: '/favicon.ico',
          tag: 'cart-reminder',
          requireInteraction: true,
        }
      );
    });

    it('should not show notification when tab becomes hidden with empty cart', () => {
      const MockNotificationConstructor = jest.fn();
      Object.defineProperty(window, 'Notification', {
        value: MockNotificationConstructor,
        writable: true,
      });

      // Simulate tab becoming hidden with empty cart
      Object.defineProperty(document, 'hidden', { value: true });
      Object.defineProperty(document, 'visibilityState', { value: 'hidden' });
      
      visibilityChangeHandler(new Event('visibilitychange'));
      
      expect(MockNotificationConstructor).not.toHaveBeenCalled();
    });

    it('should not show notification when tab becomes visible', () => {
      const MockNotificationConstructor = jest.fn();
      Object.defineProperty(window, 'Notification', {
        value: MockNotificationConstructor,
        writable: true,
      });

      notificationService.addItemToCart();
      
      // Simulate tab becoming visible
      Object.defineProperty(document, 'hidden', { value: false });
      Object.defineProperty(document, 'visibilityState', { value: 'visible' });
      
      visibilityChangeHandler(new Event('visibilitychange'));
      
      expect(MockNotificationConstructor).not.toHaveBeenCalled();
    });

    it('should not show notification if permission is denied', async () => {
      // Re-initialize with denied permission
      mockNotification.requestPermission.mockResolvedValue('denied');
      notificationService = new NotificationService();
      await notificationService.initialize();
      
      const visibilityChangeHandler = mockAddEventListener.mock.calls.find(
        call => call[0] === 'visibilitychange'
      )[1];

      const MockNotificationConstructor = jest.fn();
      Object.defineProperty(window, 'Notification', {
        value: MockNotificationConstructor,
        writable: true,
      });

      notificationService.addItemToCart();
      
      Object.defineProperty(document, 'hidden', { value: true });
      Object.defineProperty(document, 'visibilityState', { value: 'hidden' });
      
      visibilityChangeHandler(new Event('visibilitychange'));
      
      expect(MockNotificationConstructor).not.toHaveBeenCalled();
    });
  });

  describe('cleanup', () => {
    it('should remove event listeners on cleanup', async () => {
      mockNotification.requestPermission.mockResolvedValue('granted');
      await notificationService.initialize();
      
      notificationService.cleanup();
      
      expect(mockRemoveEventListener).toHaveBeenCalledWith('visibilitychange', expect.any(Function));
    });

    it('should clear notification timeout on cleanup', async () => {
      mockNotification.requestPermission.mockResolvedValue('granted');
      await notificationService.initialize();
      
      // Mock setTimeout to return a timeout ID
      const mockTimeoutId = 123;
      jest.spyOn(window, 'setTimeout').mockReturnValue(mockTimeoutId as any);
      jest.spyOn(window, 'clearTimeout');
      
      notificationService.cleanup();
      
      expect(window.clearTimeout).toHaveBeenCalledWith(mockTimeoutId);
    });
  });

  describe('getCartItemCount', () => {
    it('should return current cart item count', () => {
      expect(notificationService.getCartItemCount()).toBe(0);
      
      notificationService.addItemToCart();
      expect(notificationService.getCartItemCount()).toBe(1);
      
      notificationService.addItemToCart();
      expect(notificationService.getCartItemCount()).toBe(2);
    });
  });

  describe('isNotificationSupported', () => {
    it('should return true when notifications are supported', () => {
      expect(NotificationService.isNotificationSupported()).toBe(true);
    });

    it('should return false when notifications are not supported', () => {
      const originalNotification = window.Notification;
      delete (window as any).Notification;
      
      expect(NotificationService.isNotificationSupported()).toBe(false);
      
      window.Notification = originalNotification;
    });
  });
}); 