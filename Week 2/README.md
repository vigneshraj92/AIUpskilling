# Shopping Cart Application

A modern e-commerce shopping cart application built with React.js frontend and .NET 8 Web API backend using microservices architecture.

## 🏗️ Architecture Overview

### Frontend (React.js)
- Modern React with TypeScript
- Redux Toolkit for state management
- Material-UI for responsive design
- JWT authentication
- Shopping cart functionality

### Backend (.NET 8 Web API)
- Microservices architecture
- JWT-based authentication
- SQL Server database
- Design patterns implementation
- Comprehensive error handling
- Unit tests

## 🚀 Getting Started

### Prerequisites
- .NET 8 SDK
- Node.js 18+
- SQL Server
- Visual Studio 2022 or VS Code

### Backend Setup
1. Navigate to the backend directory
2. Update connection strings in `appsettings.json`
3. Run database migrations
4. Start the API

### Frontend Setup
1. Navigate to the frontend directory
2. Install dependencies: `npm install`
3. Start the development server: `npm start`

## 📁 Project Structure

```
ShoppingCart/
├── Backend/
│   ├── ShoppingCart.API/           # Main API Gateway
│   ├── ShoppingCart.Auth/          # Authentication Service
│   ├── ShoppingCart.Products/      # Product Management Service
│   ├── ShoppingCart.Orders/        # Order Management Service
│   ├── ShoppingCart.Payments/      # Payment Processing Service
│   └── ShoppingCart.Notifications/ # Notification Service
├── Frontend/
│   ├── src/
│   │   ├── components/
│   │   ├── pages/
│   │   ├── services/
│   │   └── store/
│   └── public/
└── Database/
    └── Scripts/
```

## 🔧 Design Patterns Implemented

### 1. Strategy Pattern
- **Location**: Payment processing service
- **Purpose**: Different payment methods (Credit Card, PayPal, Crypto)
- **Implementation**: `IPaymentStrategy` interface with concrete implementations

### 2. Observer Pattern
- **Location**: Notification service
- **Purpose**: Event-driven notifications for order updates
- **Implementation**: `INotificationObserver` with email, SMS, and push notification observers

### 3. Factory Pattern
- **Location**: Database connection management
- **Purpose**: Creating different database connections based on configuration
- **Implementation**: `IDatabaseFactory` with SQL Server and PostgreSQL implementations

## 🛡️ Security Features

- JWT-based authentication
- Role-based authorization
- Input validation and sanitization
- SQL injection prevention
- CORS configuration

## 🧪 Testing

- Unit tests for all services
- Integration tests for API endpoints
- Mock implementations for external dependencies

## 📊 Database Schema

The application uses a normalized database schema with the following main entities:
- Users
- Products
- Categories
- Orders
- OrderItems
- Payments
- Notifications

## 🔄 API Endpoints

### Authentication
- POST /api/auth/login
- POST /api/auth/register
- POST /api/auth/refresh

### Products
- GET /api/products
- GET /api/products/{id}
- POST /api/products
- PUT /api/products/{id}
- DELETE /api/products/{id}

### Orders
- GET /api/orders
- GET /api/orders/{id}
- POST /api/orders
- PUT /api/orders/{id}

### Payments
- POST /api/payments/process
- GET /api/payments/{id}

## 🚀 Deployment

### Backend
- Docker containerization
- Azure/AWS deployment ready
- Environment-specific configurations

### Frontend
- Build optimization
- CDN deployment
- Progressive Web App features

## 📝 License

This project is licensed under the MIT License. 