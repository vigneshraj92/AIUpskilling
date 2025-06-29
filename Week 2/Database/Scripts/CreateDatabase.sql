-- Shopping Cart Database Schema
-- This script creates the complete database schema for the shopping cart application

USE master;
GO

-- Create the database if it doesn't exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ShoppingCartDB')
BEGIN
    CREATE DATABASE ShoppingCartDB;
END
GO

USE ShoppingCartDB;
GO

-- Create Users table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE Users (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        Email NVARCHAR(255) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(255) NOT NULL,
        PhoneNumber NVARCHAR(20) NULL,
        Address NVARCHAR(255) NULL,
        Role NVARCHAR(50) NOT NULL DEFAULT 'Customer',
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END
GO

-- Create Categories table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Categories')
BEGIN
    CREATE TABLE Categories (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NULL,
        ImageUrl NVARCHAR(500) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END
GO

-- Create Products table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Products')
BEGIN
    CREATE TABLE Products (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        Description NVARCHAR(1000) NULL,
        Price DECIMAL(18,2) NOT NULL,
        StockQuantity INT NOT NULL DEFAULT 0,
        Brand NVARCHAR(100) NULL,
        SKU NVARCHAR(50) NULL,
        ImageUrl NVARCHAR(500) NULL,
        CategoryId INT NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_Products_Categories FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
    );
END
GO

-- Create Orders table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Orders')
BEGIN
    CREATE TABLE Orders (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        OrderNumber NVARCHAR(50) NOT NULL UNIQUE,
        UserId INT NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        TotalAmount DECIMAL(18,2) NOT NULL,
        ShippingAddress NVARCHAR(255) NULL,
        BillingAddress NVARCHAR(255) NULL,
        PaymentMethod NVARCHAR(100) NULL,
        PaymentStatus NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        OrderDate DATETIME2 NULL,
        ShippedDate DATETIME2 NULL,
        DeliveredDate DATETIME2 NULL,
        Notes NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_Orders_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
    );
END
GO

-- Create OrderItems table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OrderItems')
BEGIN
    CREATE TABLE OrderItems (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        OrderId INT NOT NULL,
        ProductId INT NOT NULL,
        Quantity INT NOT NULL,
        UnitPrice DECIMAL(18,2) NOT NULL,
        TotalPrice DECIMAL(18,2) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES Orders(Id),
        CONSTRAINT FK_OrderItems_Products FOREIGN KEY (ProductId) REFERENCES Products(Id)
    );
END
GO

-- Create Payments table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Payments')
BEGIN
    CREATE TABLE Payments (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        OrderId INT NOT NULL,
        PaymentMethod NVARCHAR(100) NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        Amount DECIMAL(18,2) NOT NULL,
        TransactionId NVARCHAR(255) NULL,
        Description NVARCHAR(500) NULL,
        ProcessedAt DATETIME2 NULL,
        ErrorMessage NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_Payments_Orders FOREIGN KEY (OrderId) REFERENCES Orders(Id)
    );
END
GO

-- Create Notifications table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Notifications')
BEGIN
    CREATE TABLE Notifications (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Message NVARCHAR(1000) NOT NULL,
        Type NVARCHAR(50) NOT NULL DEFAULT 'Info',
        Status NVARCHAR(50) NOT NULL DEFAULT 'Unread',
        RelatedEntityType NVARCHAR(100) NULL,
        RelatedEntityId INT NULL,
        ReadAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
    );
END
GO

-- Create indexes for better performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Users_Email')
BEGIN
    CREATE INDEX IX_Users_Email ON Users(Email);
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Products_CategoryId')
BEGIN
    CREATE INDEX IX_Products_CategoryId ON Products(CategoryId);
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Products_SKU')
BEGIN
    CREATE INDEX IX_Products_SKU ON Products(SKU);
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Orders_UserId')
BEGIN
    CREATE INDEX IX_Orders_UserId ON Orders(UserId);
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Orders_OrderNumber')
BEGIN
    CREATE INDEX IX_Orders_OrderNumber ON Orders(OrderNumber);
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OrderItems_OrderId')
BEGIN
    CREATE INDEX IX_OrderItems_OrderId ON OrderItems(OrderId);
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payments_OrderId')
BEGIN
    CREATE INDEX IX_Payments_OrderId ON Payments(OrderId);
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Notifications_UserId')
BEGIN
    CREATE INDEX IX_Notifications_UserId ON Notifications(UserId);
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Notifications_Status')
BEGIN
    CREATE INDEX IX_Notifications_Status ON Notifications(Status);
END

-- Insert sample data
IF NOT EXISTS (SELECT * FROM Categories WHERE Name = 'Electronics')
BEGIN
    INSERT INTO Categories (Name, Description) VALUES 
    ('Electronics', 'Electronic devices and accessories'),
    ('Clothing', 'Fashion and apparel'),
    ('Books', 'Books and publications'),
    ('Home & Garden', 'Home improvement and garden supplies');
END

IF NOT EXISTS (SELECT * FROM Products WHERE Name = 'Smartphone')
BEGIN
    INSERT INTO Products (Name, Description, Price, StockQuantity, Brand, SKU, CategoryId) VALUES 
    ('Smartphone', 'Latest smartphone with advanced features', 599.99, 50, 'TechBrand', 'PHONE-001', 1),
    ('Laptop', 'High-performance laptop for work and gaming', 1299.99, 25, 'TechBrand', 'LAPTOP-001', 1),
    ('T-Shirt', 'Comfortable cotton t-shirt', 19.99, 100, 'FashionBrand', 'TSHIRT-001', 2),
    ('Programming Book', 'Learn modern programming techniques', 49.99, 75, 'BookPublisher', 'BOOK-001', 3);
END

PRINT 'Database schema created successfully!'; 