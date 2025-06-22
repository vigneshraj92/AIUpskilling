# FileStorageService Documentation

## Overview

The `FileStorageService` class provides secure, high-performance file and folder management operations for the ClairTourTiny application. It handles file storage, retrieval, sharing, and organization with enterprise-grade security measures and optimized performance.

## Class Signature

```csharp
public class FileStorageService : IFileStorageService
```

## Dependencies

- `ClairTourTinyContext` - Database context for data persistence
- `IMapper` - AutoMapper for object mapping
- `ILogger<FileStorageService>` - Structured logging
- `IFileStorageSecurityService` - Security validation and path sanitization
- `IFileStorageConfigurationService` - Configuration management

## Security Constants

```csharp
private const int MaxFileSizeBytes = 100 * 1024 * 1024; // 100MB
private const int MaxFileNameLength = 255;
private const int MaxPathLength = 260;
private static readonly string[] AllowedExtensions = { ".txt", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".gif", ".csv" };
```

## Public Methods

### GetFileStorageDetailsAsync

**Purpose**: Retrieves a hierarchical tree structure of file storage folders for a given entity, combining physical directory structure with database attachment types.

**Signature**:
```csharp
public async Task<FileStorageResponse> GetFileStorageDetailsAsync(string entityNo)
```

**Parameters**:
- `entityNo` (string): The entity identifier. Must match pattern `^[A-Za-z0-9\-_]{1,50}$`

**Return Value**:
- `FileStorageResponse`: Contains a list of `FolderModel` objects representing the folder hierarchy

**Usage Example**:
```csharp
var fileStorageService = serviceProvider.GetService<IFileStorageService>();
var response = await fileStorageService.GetFileStorageDetailsAsync("PROJ-001");
foreach (var folder in response.Folders)
{
    Console.WriteLine($"Folder: {folder.Name}, Type: {folder.Type}");
    foreach (var child in folder.Children)
    {
        Console.WriteLine($"  Child: {child.Name}");
    }
}
```

**Error Conditions**:
- `ArgumentException`: Invalid entity number format
- `SecurityException`: Invalid path detected
- `FileStorageException`: General file storage operation failure

**Performance Notes**:
- Uses parallel data loading for optimal performance
- Implements batch loading of attachment types to avoid N+1 queries
- Async file system operations prevent thread blocking
- Optimized sorting with O(n log n) complexity

**Security Features**:
- Input validation for entity number
- Path traversal prevention
- Permission-based folder filtering

---

### AddFileAsync

**Purpose**: Securely uploads a file to the specified path within the entity's file storage structure with comprehensive validation and security checks.

**Signature**:
```csharp
public async Task<FileItem> AddFileAsync(string entityNo, AddFileRequest request)
```

**Parameters**:
- `entityNo` (string): The entity identifier. Must match pattern `^[A-Za-z0-9\-_]{1,50}$`
- `request` (AddFileRequest): Contains file data and metadata
  - `File` (IFormFile): The file to upload (required, non-empty)
  - `Path` (string): Target directory path (required)
  - `FileName` (string): Name for the uploaded file (required, max 255 chars)

**Return Value**:
- `FileItem`: Contains file metadata including name, type, size, and creation details

**Usage Example**:
```csharp
var request = new AddFileRequest
{
    File = formFile, // IFormFile from HTTP request
    Path = "Documents/Reports",
    FileName = "Q4_Report.pdf"
};

var fileItem = await fileStorageService.AddFileAsync("PROJ-001", request);
Console.WriteLine($"Uploaded: {fileItem.Name}, Size: {fileItem.Size} bytes");
```

**Error Conditions**:
- `ArgumentException`: Invalid input parameters, file size exceeds limit, unsupported file type
- `DirectoryNotFoundException`: Root folder not found
- `SecurityException`: Path traversal attempt detected
- `FileStorageException`: General file storage operation failure

**Performance Notes**:
- Uses buffered streaming for efficient large file handling
- Async file operations prevent thread blocking
- Automatic directory creation if needed
- Memory-efficient file copying

**Security Features**:
- File size validation (max 100MB)
- File type restriction (whitelist approach)
- Path traversal prevention
- File name length validation
- Input sanitization

---

### DeleteItemAsync

**Purpose**: Securely deletes a file or folder from the file storage system with proper cleanup of associated database records.

**Signature**:
```csharp
public async Task DeleteItemAsync(string entityNo, string path, bool isFolder)
```

**Parameters**:
- `entityNo` (string): The entity identifier. Must match pattern `^[A-Za-z0-9\-_]{1,50}$`
- `path` (string): Path to the item to delete (required)
- `isFolder` (bool): True if deleting a folder, false for file

**Return Value**:
- `Task`: Asynchronous operation completion

**Usage Example**:
```csharp
// Delete a file
await fileStorageService.DeleteItemAsync("PROJ-001", "Documents/old_report.pdf", false);

// Delete a folder and all contents
await fileStorageService.DeleteItemAsync("PROJ-001", "Documents/Temp", true);
```

**Error Conditions**:
- `ArgumentException`: Invalid input parameters
- `DirectoryNotFoundException`: Root folder not found
- `FileNotFoundException`: Item not found at specified path
- `SecurityException`: Path traversal attempt or system directory deletion attempt
- `FileStorageException`: General file storage operation failure

**Performance Notes**:
- Batch database operations for folder deletion
- Async database operations
- Efficient file system operations

**Security Features**:
- Path traversal prevention
- System directory protection
- Input validation
- Permission-based access control

---

### DownloadFileAsync

**Purpose**: Securely downloads a file from the file storage system with memory safety checks and proper content type detection.

**Signature**:
```csharp
public async Task<(byte[] FileContents, string ContentType, string FileName)> DownloadFileAsync(string entityNo, string filePath)
```

**Parameters**:
- `entityNo` (string): The entity identifier. Must match pattern `^[A-Za-z0-9\-_]{1,50}$`
- `filePath` (string): Path to the file to download (required)

**Return Value**:
- `(byte[] FileContents, string ContentType, string FileName)`: Tuple containing file data, MIME type, and filename

**Usage Example**:
```csharp
var (fileContents, contentType, fileName) = await fileStorageService.DownloadFileAsync("PROJ-001", "Documents/report.pdf");

// Return file in HTTP response
return File(fileContents, contentType, fileName);
```

**Error Conditions**:
- `ArgumentException`: Invalid input parameters
- `DirectoryNotFoundException`: Root folder not found
- `FileNotFoundException`: File not found at specified path
- `SecurityException`: Path traversal attempt detected
- `InvalidOperationException`: File size exceeds memory safety limit
- `FileStorageException`: General file storage operation failure

**Performance Notes**:
- Memory safety checks prevent OutOfMemoryException
- Buffered reading for large files
- Efficient content type detection
- Async file operations

**Security Features**:
- Path traversal prevention
- File size validation for memory safety
- Input validation
- Secure file access

---

### ShareFolderAsync

**Purpose**: Shares a folder with specified users by creating database entries and executing sharing procedures with comprehensive validation.

**Signature**:
```csharp
public async Task ShareFolderAsync(string entityNo, string fullPath, string attachmentType, List<ShareRequest> requests)
```

**Parameters**:
- `entityNo` (string): The entity identifier. Must match pattern `^[A-Za-z0-9\-_]{1,50}$`
- `fullPath` (string): Full path to the folder to share (required)
- `attachmentType` (string): Type of attachment for the folder
- `requests` (List<ShareRequest>): List of sharing requests with email addresses and notes

**Return Value**:
- `Task`: Asynchronous operation completion

**Usage Example**:
```csharp
var shareRequests = new List<ShareRequest>
{
    new ShareRequest { Email = "user1@company.com", Note = "Project collaboration" },
    new ShareRequest { Email = "user2@company.com", Note = "Review access" }
};

await fileStorageService.ShareFolderAsync("PROJ-001", "Documents/Shared", "Documents", shareRequests);
```

**Error Conditions**:
- `ArgumentException`: Invalid input parameters or email addresses
- `SecurityException`: Path traversal attempt detected
- `InvalidOperationException`: Sharing not supported for this folder type
- `FileStorageException`: General file storage operation failure

**Performance Notes**:
- Batch database operations
- Parallel processing of multiple share requests
- Efficient database parameter handling

**Security Features**:
- Email address validation
- Path traversal prevention
- Input sanitization
- Permission-based access control

## Private Helper Methods

### GetAttachmentTypesByDescriptionsAsync

**Purpose**: Efficiently retrieves attachment types for multiple descriptions in a single database query to avoid N+1 query problems.

**Signature**:
```csharp
private async Task<List<FolderTypeDto>> GetAttachmentTypesByDescriptionsAsync(List<string> descriptions)
```

**Performance Impact**: Reduces database queries from O(n) to O(1) for attachment type loading.

### AddSubDirectoriesToTreeAsync

**Purpose**: Recursively builds folder tree structure with async file system operations for optimal performance.

**Signature**:
```csharp
private async Task AddSubDirectoriesToTreeAsync(DirectoryInfo parentDirectory, FolderModel parentFolder, List<FolderTypeDto> attachmentTypes)
```

**Performance Impact**: Uses async file operations to prevent thread blocking during directory traversal.

### GetOwnerNameAsync

**Purpose**: Retrieves file owner information asynchronously with proper error handling for cross-platform compatibility.

**Signature**:
```csharp
private async Task<string> GetOwnerNameAsync(string filePath)
```

**Platform Support**: Windows-only for owner retrieval, graceful fallback for other platforms.

## Error Handling Strategy

The service implements a comprehensive error handling strategy with:

1. **Input Validation**: All public methods validate inputs before processing
2. **Security Exceptions**: Specific exceptions for security violations
3. **Structured Logging**: Detailed error logging with context
4. **Graceful Degradation**: Service continues operation despite individual item failures
5. **Custom Exceptions**: `FileStorageException` for general file storage errors

## Performance Optimizations

1. **Parallel Data Loading**: Concurrent execution of independent operations
2. **Batch Database Operations**: Reduced database round-trips
3. **Async File Operations**: Non-blocking file system access
4. **Memory Management**: Buffered streaming for large files
5. **Efficient Queries**: Optimized SQL with proper parameterization

## Security Features

1. **Path Traversal Prevention**: Regex-based detection and validation
2. **Input Sanitization**: Comprehensive validation of all inputs
3. **File Type Restrictions**: Whitelist approach for allowed extensions
4. **Size Limits**: Configurable file size restrictions
5. **Permission Validation**: Role-based access control
6. **System Protection**: Prevention of system directory access

## Configuration

The service uses dependency injection for configuration:

```csharp
services.AddScoped<IFileStorageService, FileStorageService>();
services.AddScoped<IFileStorageSecurityService, FileStorageSecurityService>();
services.AddScoped<IFileStorageConfigurationService, FileStorageConfigurationService>();
```

## Best Practices

1. **Always validate inputs** before calling service methods
2. **Handle exceptions appropriately** based on error type
3. **Use async/await** for all service method calls
4. **Monitor performance** for large file operations
5. **Implement proper logging** for debugging and monitoring
6. **Regular security audits** of file storage operations

## Monitoring and Logging

The service provides comprehensive logging for:
- Security violations and attempts
- Performance metrics
- Error conditions with context
- File operation success/failure
- Database operation results

Use structured logging to monitor service health and performance:

```csharp
_logger.LogInformation("File uploaded successfully: {EntityNo}, {FileName}, {Size}", entityNo, fileName, fileSize);
_logger.LogWarning("Security violation detected: {EntityNo}, {Path}", entityNo, path);
_logger.LogError(ex, "File operation failed: {EntityNo}, {Operation}", entityNo, operation);
``` 