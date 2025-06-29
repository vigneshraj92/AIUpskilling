import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import Cart from '../Cart';
import { NotificationService } from '../../../services/NotificationService';

// Mock the NotificationService
jest.mock('../../../services/NotificationService');

const MockNotificationService = NotificationService as jest.MockedClass<typeof NotificationService>;

describe('Cart Component', () => {
  let mockNotificationService: jest.Mocked<NotificationService>;

  beforeEach(() => {
    jest.clearAllMocks();
    
    mockNotificationService = {
      initialize: jest.fn().mockResolvedValue(undefined),
      addItemToCart: jest.fn(),
      removeItemFromCart: jest.fn(),
      clearCart: jest.fn(),
      cleanup: jest.fn(),
      getCartItemCount: jest.fn().mockReturnValue(0),
    } as any;

    MockNotificationService.mockImplementation(() => mockNotificationService);
  });

  describe('rendering', () => {
    it('should render cart title and empty state', () => {
      render(<Cart />);
      
      expect(screen.getByText('Shopping Cart')).toBeInTheDocument();
      expect(screen.getByText(/Your cart is empty/)).toBeInTheDocument();
    });

    it('should render available products list', () => {
      render(<Cart />);
      
      expect(screen.getByText('Available Products')).toBeInTheDocument();
      expect(screen.getByText('Laptop')).toBeInTheDocument();
      expect(screen.getByText('Mouse')).toBeInTheDocument();
      expect(screen.getByText('Keyboard')).toBeInTheDocument();
      expect(screen.getByText('Monitor')).toBeInTheDocument();
    });

    it('should show warning when notifications are not supported', () => {
      const mockIsSupported = jest.spyOn(NotificationService, 'isNotificationSupported');
      mockIsSupported.mockReturnValue(false);
      
      render(<Cart />);
      
      expect(screen.getByText(/Browser notifications are not supported/)).toBeInTheDocument();
    });
  });

  describe('cart operations', () => {
    it('should add item to cart when add button is clicked', async () => {
      const user = userEvent.setup();
      render(<Cart />);
      
      const addButtons = screen.getAllByTestId('AddIcon');
      await user.click(addButtons[0]); // Click first add button
      
      expect(mockNotificationService.addItemToCart).toHaveBeenCalled();
    });

    it('should remove item from cart when remove button is clicked', async () => {
      const user = userEvent.setup();
      render(<Cart />);
      
      // First add an item
      const addButtons = screen.getAllByTestId('AddIcon');
      await user.click(addButtons[0]);
      
      // Then remove it
      const removeButtons = screen.getAllByTestId('RemoveIcon');
      await user.click(removeButtons[0]);
      
      expect(mockNotificationService.removeItemFromCart).toHaveBeenCalled();
    });

    it('should clear cart when clear button is clicked', async () => {
      const user = userEvent.setup();
      render(<Cart />);
      
      // Add some items first
      const addButtons = screen.getAllByTestId('AddIcon');
      await user.click(addButtons[0]);
      await user.click(addButtons[1]);
      
      // Clear cart
      const clearButton = screen.getByText('Clear Cart');
      await user.click(clearButton);
      
      expect(mockNotificationService.clearCart).toHaveBeenCalled();
    });

    it('should disable remove button when quantity is 0', () => {
      render(<Cart />);
      
      const removeButtons = screen.getAllByTestId('RemoveIcon');
      removeButtons.forEach(button => {
        expect(button).toBeDisabled();
      });
    });

    it('should enable remove button after adding items', async () => {
      const user = userEvent.setup();
      render(<Cart />);
      
      const addButtons = screen.getAllByTestId('AddIcon');
      await user.click(addButtons[0]);
      
      const removeButtons = screen.getAllByTestId('RemoveIcon');
      expect(removeButtons[0]).not.toBeDisabled();
    });
  });

  describe('cart state updates', () => {
    it('should update cart item count display', async () => {
      mockNotificationService.getCartItemCount
        .mockReturnValueOnce(0)
        .mockReturnValueOnce(1)
        .mockReturnValueOnce(2);
      
      const user = userEvent.setup();
      render(<Cart />);
      
      expect(screen.getByText('Cart Items: 0')).toBeInTheDocument();
      
      const addButtons = screen.getAllByTestId('AddIcon');
      await user.click(addButtons[0]);
      
      await waitFor(() => {
        expect(screen.getByText('Cart Items: 1')).toBeInTheDocument();
      });
      
      await user.click(addButtons[0]);
      
      await waitFor(() => {
        expect(screen.getByText('Cart Items: 2')).toBeInTheDocument();
      });
    });

    it('should update total price when items are added', async () => {
      const user = userEvent.setup();
      render(<Cart />);
      
      // Initial total should be $0.00
      expect(screen.getByText('Total: $0.00')).toBeInTheDocument();
      
      // Add laptop ($999.99)
      const addButtons = screen.getAllByTestId('AddIcon');
      await user.click(addButtons[0]);
      
      await waitFor(() => {
        expect(screen.getByText('Total: $999.99')).toBeInTheDocument();
      });
      
      // Add mouse ($29.99)
      await user.click(addButtons[1]);
      
      await waitFor(() => {
        expect(screen.getByText('Total: $1029.98')).toBeInTheDocument();
      });
    });
  });

  describe('notification instructions', () => {
    it('should show notification instructions when items are in cart', async () => {
      const user = userEvent.setup();
      render(<Cart />);
      
      // Initially no instructions
      expect(screen.queryByText(/Try this:/)).not.toBeInTheDocument();
      
      // Add an item
      const addButtons = screen.getAllByTestId('AddIcon');
      await user.click(addButtons[0]);
      
      // Instructions should appear
      expect(screen.getByText(/Try this:/)).toBeInTheDocument();
      expect(screen.getByText(/switch to another browser tab/)).toBeInTheDocument();
    });

    it('should hide notification instructions when cart is cleared', async () => {
      const user = userEvent.setup();
      render(<Cart />);
      
      // Add an item
      const addButtons = screen.getAllByTestId('AddIcon');
      await user.click(addButtons[0]);
      
      // Instructions should be visible
      expect(screen.getByText(/Try this:/)).toBeInTheDocument();
      
      // Clear cart
      const clearButton = screen.getByText('Clear Cart');
      await user.click(clearButton);
      
      // Instructions should be hidden
      expect(screen.queryByText(/Try this:/)).not.toBeInTheDocument();
    });
  });

  describe('accessibility', () => {
    it('should have proper ARIA labels for buttons', () => {
      render(<Cart />);
      
      const addButtons = screen.getAllByTestId('AddIcon');
      const removeButtons = screen.getAllByTestId('RemoveIcon');
      
      addButtons.forEach(button => {
        expect(button).toHaveAttribute('aria-label');
      });
      
      removeButtons.forEach(button => {
        expect(button).toHaveAttribute('aria-label');
      });
    });

    it('should have proper heading structure', () => {
      render(<Cart />);
      
      const mainHeading = screen.getByRole('heading', { level: 1 });
      expect(mainHeading).toHaveTextContent('Shopping Cart');
      
      const subHeadings = screen.getAllByRole('heading', { level: 6 });
      expect(subHeadings).toHaveLength(2); // Cart Items and Available Products
    });
  });
}); 