import { renderHook, act } from '@testing-library/react';
import { useNotification } from '../useNotification';
import { NotificationService } from '../../services/NotificationService';

// Mock the NotificationService
jest.mock('../../services/NotificationService');

const MockNotificationService = NotificationService as jest.MockedClass<typeof NotificationService>;

describe('useNotification', () => {
  let mockNotificationService: jest.Mocked<NotificationService>;

  beforeEach(() => {
    jest.clearAllMocks();
    
    mockNotificationService = {
      initialize: jest.fn(),
      addItemToCart: jest.fn(),
      removeItemFromCart: jest.fn(),
      clearCart: jest.fn(),
      cleanup: jest.fn(),
      getCartItemCount: jest.fn(),
    } as any;

    MockNotificationService.mockImplementation(() => mockNotificationService);
  });

  describe('initialization', () => {
    it('should initialize notification service on mount', () => {
      renderHook(() => useNotification());
      
      expect(mockNotificationService.initialize).toHaveBeenCalled();
    });

    it('should cleanup notification service on unmount', () => {
      const { unmount } = renderHook(() => useNotification());
      
      unmount();
      
      expect(mockNotificationService.cleanup).toHaveBeenCalled();
    });
  });

  describe('cart operations', () => {
    it('should provide addItemToCart function', () => {
      const { result } = renderHook(() => useNotification());
      
      act(() => {
        result.current.addItemToCart();
      });
      
      expect(mockNotificationService.addItemToCart).toHaveBeenCalled();
    });

    it('should provide removeItemFromCart function', () => {
      const { result } = renderHook(() => useNotification());
      
      act(() => {
        result.current.removeItemFromCart();
      });
      
      expect(mockNotificationService.removeItemFromCart).toHaveBeenCalled();
    });

    it('should provide clearCart function', () => {
      const { result } = renderHook(() => useNotification());
      
      act(() => {
        result.current.clearCart();
      });
      
      expect(mockNotificationService.clearCart).toHaveBeenCalled();
    });
  });

  describe('cart item count', () => {
    it('should return cart item count from service', () => {
      mockNotificationService.getCartItemCount.mockReturnValue(3);
      
      const { result } = renderHook(() => useNotification());
      
      expect(result.current.cartItemCount).toBe(3);
      expect(mockNotificationService.getCartItemCount).toHaveBeenCalled();
    });

    it('should update cart item count when operations are performed', () => {
      mockNotificationService.getCartItemCount
        .mockReturnValueOnce(0)
        .mockReturnValueOnce(1)
        .mockReturnValueOnce(2);
      
      const { result } = renderHook(() => useNotification());
      
      expect(result.current.cartItemCount).toBe(0);
      
      act(() => {
        result.current.addItemToCart();
      });
      
      expect(result.current.cartItemCount).toBe(1);
      
      act(() => {
        result.current.addItemToCart();
      });
      
      expect(result.current.cartItemCount).toBe(2);
    });
  });

  describe('notification support', () => {
    it('should return notification support status', () => {
      const mockIsSupported = jest.spyOn(NotificationService, 'isNotificationSupported');
      mockIsSupported.mockReturnValue(true);
      
      const { result } = renderHook(() => useNotification());
      
      expect(result.current.isNotificationSupported).toBe(true);
      expect(mockIsSupported).toHaveBeenCalled();
    });
  });

  describe('error handling', () => {
    it('should handle initialization errors gracefully', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation();
      mockNotificationService.initialize.mockRejectedValue(new Error('Permission denied'));
      
      renderHook(() => useNotification());
      
      // Wait for the async initialization
      await new Promise(resolve => setTimeout(resolve, 0));
      
      expect(consoleErrorSpy).toHaveBeenCalled();
      consoleErrorSpy.mockRestore();
    });
  });

  describe('multiple instances', () => {
    it('should create separate notification service instances for different hooks', () => {
      const { result: result1 } = renderHook(() => useNotification());
      const { result: result2 } = renderHook(() => useNotification());
      
      expect(MockNotificationService).toHaveBeenCalledTimes(2);
      expect(result1.current).not.toBe(result2.current);
    });
  });
}); 