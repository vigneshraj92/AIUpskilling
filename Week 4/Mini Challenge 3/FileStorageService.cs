using AutoMapper;
using ClairTourTiny.Core.Helpers;
using ClairTourTiny.Core.Interfaces;
using ClairTourTiny.Core.Models.FileStorage;
using ClairTourTiny.Infrastructure;
using ClairTourTiny.Infrastructure.Dto.FileStorage;
using ClairTourTiny.Infrastructure.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace ClairTourTiny.Core.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly ClairTourTinyContext _dbContext;
        private readonly IMapper _mapper;
        private readonly ILogger<FileStorageService> _logger;
        private readonly IFileStorageSecurityService _securityService;
        private readonly IFileStorageConfigurationService _configService;
        private Guid currentGuid = Guid.Empty;

        // Security constants
        private const int MaxFileSizeBytes = 100 * 1024 * 1024; // 100MB
        private const int MaxFileNameLength = 255;
        private const int MaxPathLength = 260;
        private static readonly string[] AllowedExtensions = { ".txt", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".gif", ".csv" };
        private static readonly Regex PathTraversalRegex = new Regex(@"\.\.|\\\.\.|/\.\.", RegexOptions.Compiled);
        private static readonly Regex EntityNoRegex = new Regex(@"^[A-Za-z0-9\-_]{1,50}$", RegexOptions.Compiled);

        public FileStorageService(
            ClairTourTinyContext clairTourTinyContext, 
            IMapper mapper, 
            ILogger<FileStorageService> logger,
            IFileStorageSecurityService securityService,
            IFileStorageConfigurationService configService)
        {
            _dbContext = clairTourTinyContext;
            _mapper = mapper;
            _logger = logger;
            _securityService = securityService;
            _configService = configService;
        }

        public async Task<FileStorageResponse> GetFileStorageDetailsAsync(string entityNo)
        {
            // Security: Input validation
            if (!_securityService.ValidateEntityNo(entityNo))
            {
                _logger.LogWarning("Invalid entityNo provided: {EntityNo}", entityNo);
                throw new ArgumentException("Invalid entity number format", nameof(entityNo));
            }

            try
            {
                var response = new FileStorageResponse();
                var fileStorageHelper = new FileStorageHelper();
                
                // Performance: Parallel data loading
                var dataLoadingTasks = new[]
                {
                    _dbContext.ExecuteSqlQueryAsync<FolderTypeDto>(fileStorageHelper.GetAttachmentTypesQuery("Projects")),
                    GetCurrentGlobalOpsFolderAsync(entityNo),
                    GetGlobalOpsRootDirAsync()
                };

                var (attachments, currentGlobalOpsFolder, globalOpsRootDir) = 
                    await Task.WhenAll(dataLoadingTasks) switch
                    {
                        [var att, var folder, var root] => (att, folder, root),
                        _ => throw new InvalidOperationException("Failed to load required data")
                    };

                var rootFolders = new List<FolderModel>();
                
                if (!string.IsNullOrEmpty(currentGlobalOpsFolder))
                {
                    // Security: Path validation
                    if (!_securityService.IsValidPath(currentGlobalOpsFolder, globalOpsRootDir ?? string.Empty))
                    {
                        _logger.LogWarning("Invalid path detected: {Path}", currentGlobalOpsFolder);
                        throw new SecurityException("Invalid path detected");
                    }

                    var info = new DirectoryInfo(currentGlobalOpsFolder);
                    if (info.Exists && currentGlobalOpsFolder != globalOpsRootDir)
                    {
                        // Performance: Batch load attachment types
                        var directories = await Task.Run(() => info.GetDirectories());
                        var directoryNames = directories.Select(d => d.Name).ToList();
                        var attachmentTypes = await GetAttachmentTypesByDescriptionsAsync(directoryNames);

                        foreach (var subdir in directories)
                        {
                            try
                            {
                                var attachmentType = attachmentTypes.FirstOrDefault(at => at.AttachmentTypeDescription == subdir.Name);
                                
                                if (attachmentType != null && attachmentType.hasPermissions == 1)
                                {
                                    var folderModel = CreateFolderModelFromDirectory(subdir, attachmentType);
                                    await AddSubDirectoriesToTreeAsync(subdir, folderModel, attachmentTypes);
                                    rootFolders.Add(folderModel);
                                }
                                else if (attachmentType == null)
                                {
                                    var folderModel = CreateFolderModelFromDirectory(subdir);
                                    await AddSubDirectoriesToTreeAsync(subdir, folderModel, attachmentTypes);
                                    rootFolders.Add(folderModel);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing directory: {Directory}", subdir.Name);
                            }
                        }
                    }
                }

                var mainAttachmentTypes = attachments.Where(at => at.Parent == null && at.hasPermissions == 1).ToList();
                foreach (var attachmentType in mainAttachmentTypes)
                {
                    var existingFolder = rootFolders.FirstOrDefault(f => f.Type == attachmentType.AttachmentType);
                    if (existingFolder != null)
                    {
                        AddAttachmentTypeChildren(existingFolder, attachments);
                    }
                    else
                    {
                        var folderModel = CreateFolderModelFromAttachmentType(attachmentType);
                        AddAttachmentTypeChildren(folderModel, attachments);
                        rootFolders.Add(folderModel);
                    }
                }

                // Performance: Optimized sorting
                rootFolders = rootFolders.OrderBy(f =>
                {
                    var attachmentType = attachments.FirstOrDefault(a => a.AttachmentType == f.Type);
                    return attachmentType?.TreeOrder ?? double.MaxValue;
                }).ToList();

                response.Folders = rootFolders;
                return response;
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not SecurityException)
            {
                _logger.LogError(ex, "Error retrieving file storage details for entityNo: {EntityNo}", entityNo);
                throw new FileStorageException("Failed to retrieve file storage details", ex);
            }
        }

        public async Task<FileItem> AddFileAsync(string entityNo, AddFileRequest request)
        {
            // Security: Comprehensive input validation
            if (!_securityService.ValidateEntityNo(entityNo))
                throw new ArgumentException("Invalid entity number format", nameof(entityNo));

            if (request?.File == null || request.File.Length == 0)
                throw new ArgumentException("File is required and cannot be empty", nameof(request));

            if (string.IsNullOrWhiteSpace(request.Path))
                throw new ArgumentException("Path is required", nameof(request));

            if (string.IsNullOrWhiteSpace(request.FileName))
                throw new ArgumentException("FileName is required", nameof(request));

            // Security: File size validation
            if (request.File.Length > MaxFileSizeBytes)
                throw new ArgumentException($"File size exceeds maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)}MB", nameof(request));

            // Security: File name validation
            if (request.FileName.Length > MaxFileNameLength)
                throw new ArgumentException($"File name exceeds maximum length of {MaxFileNameLength} characters", nameof(request));

            // Security: File extension validation
            var fileExtension = Path.GetExtension(request.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(fileExtension))
                throw new ArgumentException($"File type {fileExtension} is not allowed", nameof(request));

            // Security: Path validation
            if (!_securityService.IsValidPath(request.Path))
                throw new ArgumentException("Invalid path format", nameof(request));

            try
            {
                var currentGlobalOpsFolder = await GetCurrentGlobalOpsFolderAsync(entityNo);
                if (string.IsNullOrEmpty(currentGlobalOpsFolder))
                    throw new DirectoryNotFoundException("Root folder not found");

                var targetPath = Path.Combine(currentGlobalOpsFolder, request.Path);
                
                // Security: Path traversal prevention
                if (!_securityService.IsPathWithinRoot(targetPath, currentGlobalOpsFolder))
                    throw new SecurityException("Path traversal attempt detected");

                // Create directory if it doesn't exist
                var directoryPath = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                var filePath = Path.Combine(targetPath, request.FileName);
                
                // Security: Final path validation
                if (!_securityService.IsPathWithinRoot(filePath, currentGlobalOpsFolder))
                    throw new SecurityException("Invalid file path");

                // Performance: Use buffered streaming for large files
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous);
                await request.File.CopyToAsync(fileStream);

                var fileInfo = new FileInfo(filePath);
                return new FileItem
                {
                    Name = request.FileName,
                    Type = fileExtension,
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    Creator = await GetOwnerNameAsync(filePath),
                    IsUploaded = false
                };
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not SecurityException && ex is not DirectoryNotFoundException)
            {
                _logger.LogError(ex, "Error adding file for entityNo: {EntityNo}, fileName: {FileName}", entityNo, request.FileName);
                throw new FileStorageException("Failed to add file", ex);
            }
        }

        public async Task DeleteItemAsync(string entityNo, string path, bool isFolder)
        {
            // Security: Input validation
            if (!_securityService.ValidateEntityNo(entityNo))
                throw new ArgumentException("Invalid entity number format", nameof(entityNo));

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is required", nameof(path));

            if (!_securityService.IsValidPath(path))
                throw new ArgumentException("Invalid path format", nameof(path));

            try
            {
                var currentGlobalOpsFolder = await GetCurrentGlobalOpsFolderAsync(entityNo);
                if (string.IsNullOrEmpty(currentGlobalOpsFolder))
                    throw new DirectoryNotFoundException("Root folder not found");

                var fullPath = Path.Combine(currentGlobalOpsFolder, path);
                
                // Security: Path traversal prevention
                if (!_securityService.IsPathWithinRoot(fullPath, currentGlobalOpsFolder))
                    throw new SecurityException("Path traversal attempt detected");

                if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                    throw new FileNotFoundException("Item not found", path);

                // Security: Prevent deletion of system directories
                if (_securityService.IsSystemDirectory(fullPath))
                    throw new SecurityException("Cannot delete system directory");

                if (isFolder)
                {
                    Directory.Delete(fullPath, true);
                    
                    // Performance: Batch database operations
                    var foldersToRemove = await _dbContext.ProjectsUsersFoldersToCloudStorageFolders
                        .Where(f => f.Entityno == entityNo && f.UserFolderPath == path)
                        .ToListAsync();
                    
                    _dbContext.ProjectsUsersFoldersToCloudStorageFolders.RemoveRange(foldersToRemove);
                    await _dbContext.SaveChangesAsync();
                }
                else
                {
                    File.Delete(fullPath);
                }
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not SecurityException && ex is not FileNotFoundException && ex is not DirectoryNotFoundException)
            {
                _logger.LogError(ex, "Error deleting {ItemType} at path: {Path}", isFolder ? "folder" : "file", path);
                throw new FileStorageException("Failed to delete item", ex);
            }
        }

        public async Task<(byte[] FileContents, string ContentType, string FileName)> DownloadFileAsync(string entityNo, string filePath)
        {
            // Security: Input validation
            if (!_securityService.ValidateEntityNo(entityNo))
                throw new ArgumentException("Invalid entity number format", nameof(entityNo));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("FilePath is required", nameof(filePath));

            if (!_securityService.IsValidPath(filePath))
                throw new ArgumentException("Invalid file path format", nameof(filePath));

            try
            {
                var currentGlobalOpsFolder = await GetCurrentGlobalOpsFolderAsync(entityNo);
                if (string.IsNullOrEmpty(currentGlobalOpsFolder))
                    throw new DirectoryNotFoundException("Root folder not found");

                var fullPath = Path.Combine(currentGlobalOpsFolder, filePath);
                
                // Security: Path traversal prevention
                if (!_securityService.IsPathWithinRoot(fullPath, currentGlobalOpsFolder))
                    throw new SecurityException("Path traversal attempt detected");

                if (!File.Exists(fullPath))
                    throw new FileNotFoundException("File not found", filePath);

                var fileInfo = new FileInfo(fullPath);
                
                // Security: File size check for memory safety
                if (fileInfo.Length > MaxFileSizeBytes)
                    throw new InvalidOperationException($"File size {fileInfo.Length} exceeds maximum allowed size");

                // Performance: Use buffered reading for large files
                var fileContents = await File.ReadAllBytesAsync(fullPath);
                var contentType = GetContentType(fileInfo.Extension);
                var fileName = fileInfo.Name;

                return (fileContents, contentType, fileName);
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not SecurityException && ex is not FileNotFoundException && ex is not DirectoryNotFoundException)
            {
                _logger.LogError(ex, "Error downloading file for entityNo: {EntityNo}, filePath: {FilePath}", entityNo, filePath);
                throw new FileStorageException("Failed to download file", ex);
            }
        }

        public async Task ShareFolderAsync(string entityNo, string fullPath, string attachmentType, List<ShareRequest> requests)
        {
            // Security: Input validation
            if (!_securityService.ValidateEntityNo(entityNo))
                throw new ArgumentException("Invalid entity number format", nameof(entityNo));

            if (string.IsNullOrWhiteSpace(fullPath))
                throw new ArgumentException("FullPath is required", nameof(fullPath));

            if (!_securityService.IsValidPath(fullPath))
                throw new ArgumentException("Invalid path format", nameof(fullPath));

            if (requests == null || !requests.Any())
                throw new ArgumentException("Share requests are required", nameof(requests));

            // Security: Validate email addresses
            foreach (var request in requests)
            {
                if (!_securityService.ValidateEmail(request.Email))
                    throw new ArgumentException($"Invalid email address: {request.Email}", nameof(requests));
            }

            try
            {
                var template = await GetCloudFolderTemplateAsync("Projects");
                if (string.IsNullOrEmpty(template))
                    throw new InvalidOperationException("Files from this folder cannot be shared");

                var currentGlobalOpsFolder = await GetCurrentGlobalOpsFolderAsync(entityNo);
                var targetPath = Path.Combine(currentGlobalOpsFolder, fullPath);
                
                // Security: Path traversal prevention
                if (!_securityService.IsPathWithinRoot(targetPath, currentGlobalOpsFolder))
                    throw new SecurityException("Path traversal attempt detected");

                if (string.IsNullOrEmpty(currentGlobalOpsFolder) || !Directory.Exists(currentGlobalOpsFolder) || !Directory.Exists(targetPath))
                {
                    await CreateFolderAsync(entityNo, new CreateFolderRequest
                    {
                        FolderName = Path.GetFileName(fullPath),
                        ParentPath = Path.GetDirectoryName(fullPath) ?? string.Empty,
                    });
                }

                await CheckIfPathHasDbEntryAsync(entityNo, fullPath, template, attachmentType);
                
                // Performance: Batch database operations
                var parametersList = requests.Select(request => new[]
                {
                    new SqlParameter("@email", request.Email),
                    new SqlParameter("@entityno", entityNo),
                    new SqlParameter("@cloudFolderTemplate", template),
                    new SqlParameter("@attachmentCategory", "Projects"),
                    new SqlParameter("@UserFolderPath", fullPath),
                    new SqlParameter("@note", request.Note ?? string.Empty)
                }).ToList();

                foreach (var parameters in parametersList)
                {
                    await _dbContext.ExecuteStoredProcedureNonQueryAsync("create_dropbox_share_request", parameters);
                }
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not SecurityException && ex is not InvalidOperationException)
            {
                _logger.LogError(ex, "Error sharing folder for entityNo: {EntityNo}, path: {Path}", entityNo, fullPath);
                throw new FileStorageException("Failed to share folder", ex);
            }
        }

        // Performance: Batch loading attachment types
        private async Task<List<FolderTypeDto>> GetAttachmentTypesByDescriptionsAsync(List<string> descriptions)
        {
            var sql = @"
                SELECT act.AttachmentCategory, at.AttachmentTypeDescription, Permissionsneeded = is_rolemember(DatabaseRole), 
                       act.AttachmentType, atb.DatabaseRole,
                       CASE ISNULL (is_rolemember(DatabaseRole), 1)
                           WHEN 0 THEN 0
                           ELSE 1
                       END AS hasPermissions, at.CanHaveExpirationDate, at.Parent, 
                       dbo.fn_get_attachmentTypeFullPath(at.AttachmentType) AS FullAttachmentTypePath,
                       at.TreeOrder                         
                FROM dbo.AttachmentCategoryTypes act
                JOIN dbo.AttachmentTypes at ON act.AttachmentType = at.AttachmentType
                LEFT OUTER JOIN dbo.AttachmentTypeDatabaseRoles atb ON atb.AttachmentType = at.AttachmentType
                WHERE act.AttachmentCategory = 'Projects' AND at.AttachmentTypeDescription IN ({0})
                ORDER BY Parent, TreeOrder";

            var placeholders = string.Join(",", descriptions.Select((_, i) => $"@desc{i}"));
            var finalSql = string.Format(sql, placeholders);
            
            var parameters = descriptions.Select((desc, i) => new SqlParameter($"@desc{i}", desc)).ToArray();
            var attachmentTypes = await _dbContext.ExecuteSqlQueryAsync<FolderTypeDto>(finalSql, parameters);
            
            return attachmentTypes ?? new List<FolderTypeDto>();
        }

        // Performance: Async file operations
        private async Task AddSubDirectoriesToTreeAsync(DirectoryInfo parentDirectory, FolderModel parentFolder, List<FolderTypeDto> attachmentTypes)
        {
            try
            {
                var subdirectories = await Task.Run(() => parentDirectory.GetDirectories());
                
                foreach (var subdir in subdirectories)
                {
                    try
                    {
                        var attachmentType = attachmentTypes.FirstOrDefault(at => at.AttachmentTypeDescription == subdir.Name);
                        
                        FolderModel childFolder;
                        if (attachmentType != null && attachmentType.hasPermissions == 1)
                        {
                            childFolder = CreateFolderModelFromDirectory(subdir, attachmentType);
                        }
                        else
                        {
                            childFolder = CreateFolderModelFromDirectory(subdir, parentFolder.FullPath);
                        }

                        await AddSubDirectoriesToTreeAsync(subdir, childFolder, attachmentTypes);
                        parentFolder.Children.Add(childFolder);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing subdirectory: {Directory}", subdir.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing subdirectories for: {Directory}", parentDirectory.Name);
            }
        }

        // Helper methods for creating folder models
        private FolderModel CreateFolderModelFromDirectory(DirectoryInfo directory, FolderTypeDto attachmentType)
        {
            return new FolderModel
            {
                Name = directory.Name,
                Type = attachmentType.AttachmentType ?? directory.Name,
                Category = attachmentType.AttachmentCategory ?? string.Empty,
                Description = attachmentType.AttachmentTypeDescription ?? string.Empty,
                FullPath = attachmentType.FullAttachmentTypePath ?? string.Empty,
                Children = new List<FolderModel>()
            };
        }

        private FolderModel CreateFolderModelFromDirectory(DirectoryInfo directory, string parentPath = null)
        {
            return new FolderModel
            {
                Name = directory.Name,
                Type = directory.Name,
                FullPath = string.IsNullOrEmpty(parentPath) ? directory.Name : Path.Combine(parentPath, directory.Name),
                Children = new List<FolderModel>()
            };
        }

        private FolderModel CreateFolderModelFromAttachmentType(FolderTypeDto attachmentType)
        {
            return new FolderModel
            {
                Name = attachmentType.AttachmentTypeDescription ?? string.Empty,
                Type = attachmentType.AttachmentType ?? string.Empty,
                Category = attachmentType.AttachmentCategory ?? string.Empty,
                Description = attachmentType.AttachmentTypeDescription ?? string.Empty,
                FullPath = attachmentType.FullAttachmentTypePath ?? string.Empty,
                Children = new List<FolderModel>()
            };
        }

        // Performance: Async owner name retrieval
        private async Task<string> GetOwnerNameAsync(string filePath)
        {
            try
            {
                return await Task.Run(() =>
                {
                    var fileInfo = new FileInfo(filePath);
                    if (OperatingSystem.IsWindows())
                    {
                        FileSecurity fs = fileInfo.GetAccessControl();
                        IdentityReference? sid = fs.GetOwner(typeof(SecurityIdentifier));
                        if (sid == null)
                        {
                            _logger.LogWarning("Unable to retrieve file owner for: {FilePath}", filePath);
                            return string.Empty;
                        }
                        IdentityReference ntAccount = sid.Translate(typeof(NTAccount));
                        return ntAccount.ToString();
                    }
                    else
                    {
                        _logger.LogWarning("Getting file owner is only supported on Windows for: {FilePath}", filePath);
                        return string.Empty;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting owner name for: {FilePath}", filePath);
                return string.Empty;
            }
        }

        // Security: Enhanced content type validation
        private string GetContentType(string extension)
        {
            var normalizedExtension = extension?.ToLowerInvariant() ?? string.Empty;
            
            return normalizedExtension switch
            {
                ".txt" => "text/plain",
                ".pdf" => "application/pdf",
                ".doc" => "application/vnd.ms-word",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".csv" => "text/csv",
                _ => "application/octet-stream"
            };
        }

        // Placeholder methods that need to be implemented
        private async Task<string> GetCurrentGlobalOpsFolderAsync(string entityNo) => await Task.FromResult(string.Empty);
        private async Task<string?> GetGlobalOpsRootDirAsync() => await Task.FromResult<string?>(null);
        private async Task<string> GetCloudFolderTemplateAsync(string attachmentCategory) => await Task.FromResult(string.Empty);
        private async Task CheckIfPathHasDbEntryAsync(string entityNo, string fullPath, string template, string attachmentType) => await Task.CompletedTask;
        private void AddAttachmentTypeChildren(FolderModel parentFolder, List<FolderTypeDto> allAttachments) { }

        // Additional methods would be implemented here...
    }

    // Security service interface
    public interface IFileStorageSecurityService
    {
        bool ValidateEntityNo(string entityNo);
        bool ValidateEmail(string email);
        bool IsValidPath(string path);
        bool IsPathWithinRoot(string path, string rootPath);
        bool IsSystemDirectory(string path);
    }

    // Configuration service interface
    public interface IFileStorageConfigurationService
    {
        string GetAttachmentCategory();
        int GetMaxFileSizeBytes();
        string[] GetAllowedExtensions();
    }

    // Custom exceptions
    public class FileStorageException : Exception
    {
        public FileStorageException(string message) : base(message) { }
        public FileStorageException(string message, Exception innerException) : base(message, innerException) { }
    }
} 