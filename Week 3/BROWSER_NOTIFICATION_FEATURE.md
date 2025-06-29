# Browser Notification Feature Implementation

## Overview

This document outlines the implementation of a browser notification feature for the shopping cart application. The feature automatically shows browser notifications when users switch to other tabs while having items in their shopping cart, reminding them to complete their order.

## Feature Description

When a user adds items to their shopping cart and switches to another browser tab, the application automatically displays a browser notification with the message: **"Your items are waiting, please place order before item goes out of stock"**

## Architecture

The implementation follows a layered architecture with clear separation of concerns:

```
Frontend/
├── src/
│   ├── services/
│   │   ├── NotificationService.ts          # Core notification logic
│   │   └── __tests__/
│   │       └── NotificationService.test.ts # Service unit tests
│   ├── hooks/
│   │   ├── useNotification.ts              # React hook interface
│   │   └── __tests__/
│   │       └── useNotification.test.ts     # Hook unit tests
│   ├── components/
│   │   └── Cart/
│   │       ├── Cart.tsx                    # Demo component
│   │       └── __tests__/
│   │           └── Cart.test.tsx           # Component integration tests
│   └── setupTests.ts                       # Jest test configuration
```

## Implementation Details

### 1. NotificationService Class

**File:** `Frontend/src/services/NotificationService.ts`

**Purpose:** Core service handling browser notifications and tab visibility detection.

**Key Features:**
- Browser notification permission management
- Page Visibility API integration
- Cart item tracking
- Event listener management
- Graceful error handling

**Public Methods:**
- `initialize()` - Request permission and setup event listeners
- `addItemToCart()` - Increment cart item count
- `removeItemFromCart()` - Decrement cart item count
- `clearCart()` - Reset cart to empty state
- `getCartItemCount()` - Get current item count
- `cleanup()` - Remove event listeners and cleanup resources
- `isNotificationSupported()` - Check browser compatibility

**Private Methods:**
- `setupEventListeners()` - Configure visibility change detection
- `handleVisibilityChange()` - Process tab visibility changes
- `showCartReminderNotification()` - Display browser notification

### 2. useNotification React Hook

**File:** `Frontend/src/hooks/useNotification.ts`

**Purpose:** React hook providing a clean interface to the NotificationService.

**Return Interface:**
```typescript
interface UseNotificationReturn {
  addItemToCart: () => void;
  removeItemFromCart: () => void;
  clearCart: () => void;
  cartItemCount: number;
  isNotificationSupported: boolean;
}
```

**Features:**
- Automatic service initialization on mount
- Cleanup on component unmount
- Real-time cart count synchronization
- Error handling for initialization failures
- Support for multiple hook instances

### 3. Cart Component

**File:** `Frontend/src/components/Cart/Cart.tsx`

**Purpose:** Demo component showcasing the notification feature functionality.

**Features:**
- Interactive product list with add/remove functionality
- Real-time cart total calculation
- Clear cart functionality
- Notification support detection
- User instructions for testing the feature
- Material-UI integration for modern UI

**Component State:**
- Product list with quantities
- Cart operations integration
- Notification status display

## Technical Implementation

### Browser APIs Used

1. **Notification API**
   - `Notification.requestPermission()` - Request notification permission
   - `new Notification()` - Create and display notifications
   - Permission status checking

2. **Page Visibility API**
   - `document.hidden` - Check if page is hidden
   - `document.visibilityState` - Get current visibility state
   - `visibilitychange` event listener

3. **Event Management**
   - `addEventListener()` / `removeEventListener()` for cleanup
   - Proper event handler binding to prevent memory leaks

### Error Handling

- **Permission Denied:** Graceful fallback with console warnings
- **Unsupported Browser:** Feature detection with user notification
- **API Failures:** Try-catch blocks with error logging
- **Initialization Errors:** Non-blocking error handling

### Performance Considerations

- **Event Listener Cleanup:** Proper removal on component unmount
- **Memory Management:** No memory leaks from event listeners
- **Efficient State Updates:** Minimal re-renders with useCallback
- **Interval Management:** Cleanup of polling intervals

## Testing Strategy

### Test Coverage Areas

1. **Unit Tests (NotificationService)**
   - Service initialization and permission handling
   - Cart operations (add, remove, clear)
   - Visibility change detection
   - Notification display logic
   - Error handling scenarios
   - Browser compatibility checks

2. **Unit Tests (useNotification Hook)**
   - Hook initialization and cleanup
   - Cart operation delegation
   - State synchronization
   - Error handling
   - Multiple instance support

3. **Integration Tests (Cart Component)**
   - Component rendering
   - User interactions (add/remove items)
   - State updates and UI synchronization
   - Notification instruction display
   - Accessibility features

### Test Environment Setup

**File:** `Frontend/src/setupTests.ts`

**Mocked APIs:**
- Browser Notification API
- Document visibility properties
- Local and session storage
- Console methods for error testing

## Usage Examples

### Basic Hook Usage

```typescript
import { useNotification } from '../hooks/useNotification';

function MyComponent() {
  const { addItemToCart, removeItemFromCart, cartItemCount } = useNotification();
  
  return (
    <div>
      <p>Items in cart: {cartItemCount}</p>
      <button onClick={addItemToCart}>Add Item</button>
      <button onClick={removeItemFromCart}>Remove Item</button>
    </div>
  );
}
```

### Service Direct Usage

```typescript
import { NotificationService } from '../services/NotificationService';

const notificationService = new NotificationService();
await notificationService.initialize();
notificationService.addItemToCart();
```

## Browser Compatibility

### Supported Browsers
- Chrome 22+
- Firefox 22+
- Safari 7+
- Edge 14+

### Feature Detection
The implementation includes automatic feature detection and graceful degradation for unsupported browsers.

## Security Considerations

1. **Permission-Based:** Notifications only work with explicit user permission
2. **No Data Collection:** No personal data is transmitted or stored
3. **Local Only:** All functionality runs client-side
4. **User Control:** Users can revoke permissions at any time

## Future Enhancements

### Potential Improvements
1. **Custom Notification Sounds:** Audio feedback for notifications
2. **Rich Notifications:** Include product images and details
3. **Notification Actions:** Direct "Complete Order" buttons
4. **Timing Controls:** Configurable notification delays
5. **Analytics Integration:** Track notification effectiveness

### Scalability Considerations
1. **Multiple Cart Types:** Support for different cart contexts
2. **User Preferences:** Allow users to customize notification behavior
3. **Cross-Tab Synchronization:** Real-time cart state across tabs
4. **Offline Support:** Queue notifications for when online

## Dependencies

### Required Dependencies
- React 18.2.0+
- TypeScript 4.9.5+
- Material-UI 5.14.20+

### Development Dependencies
- Jest 27.5.2+
- @testing-library/react 13.4.0+
- @testing-library/user-event 13.5.0+

## Installation and Setup

1. **Install Dependencies:**
   ```bash
   cd Frontend
   npm install
   ```

2. **Run Tests:**
   ```bash
   npm test
   ```

3. **Start Development Server:**
   ```bash
   npm start
   ```

## Troubleshooting

### Common Issues

1. **Notifications Not Showing**
   - Check browser permission settings
   - Verify browser supports Notification API
   - Ensure page is served over HTTPS (required for notifications)

2. **Permission Denied**
   - User must manually enable notifications in browser settings
   - Check browser's notification settings for the domain

3. **Tests Failing**
   - Ensure all dependencies are installed
   - Check Jest configuration in setupTests.ts
   - Verify browser API mocks are properly configured

## Contributing

When contributing to this feature:

1. **Follow TDD:** Write tests before implementing features
2. **Maintain Coverage:** Ensure test coverage remains high
3. **Document Changes:** Update this document for any modifications
4. **Browser Testing:** Test across multiple browsers
5. **Accessibility:** Ensure features work with screen readers

## License

This feature is part of the shopping cart application and follows the same licensing terms as the main project. 