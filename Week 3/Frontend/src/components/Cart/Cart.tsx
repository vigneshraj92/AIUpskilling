import React from 'react';
import {
  Box,
  Card,
  CardContent,
  Typography,
  Button,
  List,
  ListItem,
  ListItemText,
  ListItemSecondaryAction,
  IconButton,
  Chip,
  Alert,
} from '@mui/material';
import { Add, Remove, Delete, ShoppingCart } from '@mui/icons-material';
import { useNotification } from '../../hooks/useNotification';

interface CartItem {
  id: number;
  name: string;
  price: number;
  quantity: number;
}

const Cart: React.FC = () => {
  const {
    addItemToCart,
    removeItemFromCart,
    clearCart,
    cartItemCount,
    isNotificationSupported,
  } = useNotification();

  const [items, setItems] = React.useState<CartItem[]>([
    { id: 1, name: 'Laptop', price: 999.99, quantity: 0 },
    { id: 2, name: 'Mouse', price: 29.99, quantity: 0 },
    { id: 3, name: 'Keyboard', price: 79.99, quantity: 0 },
    { id: 4, name: 'Monitor', price: 299.99, quantity: 0 },
  ]);

  const handleAddItem = (itemId: number) => {
    setItems(prevItems =>
      prevItems.map(item =>
        item.id === itemId
          ? { ...item, quantity: item.quantity + 1 }
          : item
      )
    );
    addItemToCart();
  };

  const handleRemoveItem = (itemId: number) => {
    setItems(prevItems =>
      prevItems.map(item =>
        item.id === itemId && item.quantity > 0
          ? { ...item, quantity: item.quantity - 1 }
          : item
      )
    );
    removeItemFromCart();
  };

  const handleClearCart = () => {
    setItems(prevItems =>
      prevItems.map(item => ({ ...item, quantity: 0 }))
    );
    clearCart();
  };

  const totalItems = items.reduce((sum, item) => sum + item.quantity, 0);
  const totalPrice = items.reduce((sum, item) => sum + (item.price * item.quantity), 0);

  return (
    <Box sx={{ maxWidth: 600, mx: 'auto', p: 2 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        Shopping Cart
      </Typography>

      {!isNotificationSupported && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          Browser notifications are not supported in your browser.
        </Alert>
      )}

      <Card sx={{ mb: 2 }}>
        <CardContent>
          <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
            <ShoppingCart sx={{ mr: 1 }} />
            <Typography variant="h6">
              Cart Items: {cartItemCount}
            </Typography>
            <Chip
              label={`Total: $${totalPrice.toFixed(2)}`}
              color="primary"
              sx={{ ml: 'auto' }}
            />
          </Box>

          {totalItems === 0 ? (
            <Typography variant="body1" color="text.secondary">
              Your cart is empty. Add some items to see the notification feature in action!
            </Typography>
          ) : (
            <Button
              variant="outlined"
              color="error"
              onClick={handleClearCart}
              startIcon={<Delete />}
            >
              Clear Cart
            </Button>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardContent>
          <Typography variant="h6" gutterBottom>
            Available Products
          </Typography>
          
          <List>
            {items.map((item) => (
              <ListItem key={item.id} divider>
                <ListItemText
                  primary={item.name}
                  secondary={`$${item.price.toFixed(2)}`}
                />
                <ListItemSecondaryAction>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <IconButton
                      size="small"
                      onClick={() => handleRemoveItem(item.id)}
                      disabled={item.quantity === 0}
                    >
                      <Remove />
                    </IconButton>
                    <Typography variant="body2" sx={{ minWidth: 30, textAlign: 'center' }}>
                      {item.quantity}
                    </Typography>
                    <IconButton
                      size="small"
                      onClick={() => handleAddItem(item.id)}
                    >
                      <Add />
                    </IconButton>
                  </Box>
                </ListItemSecondaryAction>
              </ListItem>
            ))}
          </List>
        </CardContent>
      </Card>

      {totalItems > 0 && (
        <Alert severity="info" sx={{ mt: 2 }}>
          <Typography variant="body2">
            <strong>Try this:</strong> Add items to your cart, then switch to another browser tab.
            You should receive a notification reminding you to complete your order!
          </Typography>
        </Alert>
      )}
    </Box>
  );
};

export default Cart; 