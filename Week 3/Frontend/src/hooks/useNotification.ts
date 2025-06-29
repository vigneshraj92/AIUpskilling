import { useEffect, useState, useCallback } from 'react';
import { NotificationService } from '../services/NotificationService';

export interface UseNotificationReturn {
  addItemToCart: () => void;
  removeItemFromCart: () => void;
  clearCart: () => void;
  cartItemCount: number;
  isNotificationSupported: boolean;
}

/**
 * React hook for managing shopping cart notifications
 * Provides functions to add/remove items and track cart count
 * Automatically shows browser notifications when user switches tabs with items in cart
 */
export const useNotification = (): UseNotificationReturn => {
  const [notificationService] = useState(() => new NotificationService());
  const [cartItemCount, setCartItemCount] = useState(0);
  const [isNotificationSupported] = useState(() => NotificationService.isNotificationSupported());

  // Initialize notification service on mount
  useEffect(() => {
    const initializeService = async () => {
      try {
        await notificationService.initialize();
      } catch (error) {
        console.error('Failed to initialize notification service:', error);
      }
    };

    initializeService();

    // Cleanup on unmount
    return () => {
      notificationService.cleanup();
    };
  }, [notificationService]);

  // Update cart item count when service state changes
  useEffect(() => {
    const updateCartCount = () => {
      setCartItemCount(notificationService.getCartItemCount());
    };

    // Initial count
    updateCartCount();

    // Set up interval to sync with service state
    const interval = setInterval(updateCartCount, 100);

    return () => clearInterval(interval);
  }, [notificationService]);

  const addItemToCart = useCallback(() => {
    notificationService.addItemToCart();
    setCartItemCount(notificationService.getCartItemCount());
  }, [notificationService]);

  const removeItemFromCart = useCallback(() => {
    notificationService.removeItemFromCart();
    setCartItemCount(notificationService.getCartItemCount());
  }, [notificationService]);

  const clearCart = useCallback(() => {
    notificationService.clearCart();
    setCartItemCount(notificationService.getCartItemCount());
  }, [notificationService]);

  return {
    addItemToCart,
    removeItemFromCart,
    clearCart,
    cartItemCount,
    isNotificationSupported,
  };
}; 