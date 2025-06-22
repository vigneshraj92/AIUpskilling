using AutoMapper;
using Azure.Core;
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

namespace ClairTourTiny.Core.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly ClairTourTinyContext _dbContext;
        private readonly IMapper _mapper;
        private readonly ILogger<FileStorageService> _logger;
        private Guid currentGuid = Guid.Empty;
        public FileStorageService(ClairTourTinyContext clairTourTinyContext, IMapper mapper, ILogger<FileStorageService> logger)
        {
            _dbContext = clairTourTinyContext;
            _mapper = mapper;
            _logger = logger;
        }

        private void AddAttachmentTypeChildren(FolderModel parentFolder, List<FolderTypeDto> allAttachments)
        {
            var childAttachments = allAttachments
                .Where(x => x.Parent == parentFolder.Type)
                .ToList();

            foreach (var childAttachment in childAttachments)
            {
                var existingChild = parentFolder.Children.FirstOrDefault(c => c.Type == childAttachment.AttachmentType);
                if (existingChild != null)
                {
                    AddAttachmentTypeChildren(existingChild, allAttachments);
                    continue;
                }
                var childFolder = new FolderModel
                {
                    Category = childAttachment.AttachmentCategory ?? string.Empty,
                    Description = childAttachment.AttachmentTypeDescription ?? string.Empty,
                    Type = childAttachment.AttachmentType ?? string.Empty,
                    FullPath = childAttachment.FullAttachmentTypePath ?? string.Empty
                };
                var hasChildren = allAttachments.Any(x => x.Parent == childFolder.Type);
                if (hasChildren)
                {
                    AddAttachmentTypeChildren(childFolder, allAttachments);
                }
                if (!parentFolder.Children.Any(c => c.Type == childFolder.Type))
                {
                    parentFolder.Children.Add(childFolder);
                }
            }
        }

        public async Task<FileStorageResponse> GetFileStorageDetailsAsync(string entityNo)
        {
            var response = new FileStorageResponse();
            var fileStorageHelper = new FileStorageHelper();
            var attachments = await _dbContext.ExecuteSqlQueryAsync<FolderTypeDto>(fileStorageHelper.GetAttachmentTypesQuery("Projects"));
            var currentGlobalOpsFolder = await GetCurrentGlobalOpsFolder(entityNo);
            var globalOpsRootDir = await GetGlobalOpsRootDir();
            var rootFolders = new List<FolderModel>();
            if (!string.IsNullOrEmpty(currentGlobalOpsFolder))
            {
                var info = new DirectoryInfo(currentGlobalOpsFolder);
                if (info.Exists && currentGlobalOpsFolder != globalOpsRootDir)
                {
                    foreach (var subdir in info.GetDirectories())
                    {
                        try
                        {
                            var attachmentType = await GetAttachmentTypeByDescription(subdir.Name);
                            var hasFiles = subdir.GetFiles().Length > 0;
                            if (attachmentType != null)
                            {
                                if (attachmentType.hasPermissions == 1)
                                {
                                    var folderModel = new FolderModel
                                    {
                                        Name = subdir.Name,
                                        Type = attachmentType.AttachmentType ?? subdir.Name,
                                        Category = attachmentType.AttachmentCategory ?? string.Empty,
                                        Description = attachmentType.AttachmentTypeDescription ?? string.Empty,
                                        FullPath = attachmentType.FullAttachmentTypePath ?? string.Empty
                                    };
                                    await AddSubDirectoriesToTree(subdir.GetDirectories(), folderModel);
                                    rootFolders.Add(folderModel);
                                }
                            }
                            else
                            {
                                var folderModel = new FolderModel
                                {
                                    Name = subdir.Name,
                                    Type = subdir.Name,
                                    FullPath = subdir.Name
                                };
                                await AddSubDirectoriesToTree(subdir.GetDirectories(), folderModel);
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
                    var folderModel = new FolderModel
                    {
                        Name = attachmentType.AttachmentTypeDescription ?? string.Empty,
                        Type = attachmentType.AttachmentType ?? string.Empty,
                        Category = attachmentType.AttachmentCategory ?? string.Empty,
                        Description = attachmentType.AttachmentTypeDescription ?? string.Empty,
                        FullPath = attachmentType.FullAttachmentTypePath ?? string.Empty
                    };

                    AddAttachmentTypeChildren(folderModel, attachments);
                    rootFolders.Add(folderModel);
                }
            }
            rootFolders = rootFolders.OrderBy(f =>
            {
                var attachmentType = attachments.FirstOrDefault(a => a.AttachmentType == f.Type);
                return attachmentType?.TreeOrder ?? int.MaxValue;
            }).ToList();
            response.Folders = rootFolders;
            return response;
        }

        private async Task<FolderTypeDto?> GetAttachmentTypeByDescription(string description)
        {
            var sql = @"
                SELECT act.AttachmentCategory, at.AttachmentTypeDescription, Permissionsneeded = is_rolemember(DatabaseRole), act.AttachmentType, atb.DatabaseRole,
						 CASE ISNULL (is_rolemember(DatabaseRole), 1)
						 WHEN 0 THEN 0
						 ELSE 1
						 END AS hasPermissions, at.CanHaveExpirationDate, at.Parent, dbo.fn_get_attachmentTypeFullPath(at.AttachmentType) AS FullAttachmentTypePath
                        ,at.TreeOrder                         
                        FROM dbo.AttachmentCategoryTypes act
                         JOIN dbo.AttachmentTypes at ON act.AttachmentType = at.AttachmentType
						 LEFT OUTER JOIN dbo.AttachmentTypeDatabaseRoles atb ON atb.AttachmentType = at.AttachmentType
                         WHERE act.AttachmentCategory =  'Projects'
                            ORDER BY Parent, TreeOrder ";
            var attachmentTypes = await _dbContext.ExecuteSqlQueryAsync<FolderTypeDto>(sql);
            return attachmentTypes?.Where(x => x.AttachmentTypeDescription == description)?.FirstOrDefault();
        }

        private async Task AddSubDirectoriesToTree(DirectoryInfo[] subdirs, FolderModel parentFolder)
        {
            foreach (var subdir in subdirs)
            {
                try
                {
                    var attachmentType = await GetAttachmentTypeByDescription(subdir.Name);
                    var hasFiles = subdir.GetFiles().Length > 0;

                    if (attachmentType != null)
                    {
                        if (attachmentType.hasPermissions == 1)
                        {
                            var childFolder = new FolderModel
                            {
                                Name = subdir.Name,
                                Type = attachmentType.AttachmentType ?? subdir.Name,
                                Category = attachmentType.AttachmentCategory ?? string.Empty,
                                Description = attachmentType.AttachmentTypeDescription ?? string.Empty,
                                FullPath = attachmentType.FullAttachmentTypePath ?? string.Empty
                            };

                            await AddSubDirectoriesToTree(subdir.GetDirectories(), childFolder);
                            parentFolder.Children.Add(childFolder);
                        }
                    }
                    else
                    {
                        var childFolder = new FolderModel
                        {
                            Name = subdir.Name,
                            Type = subdir.Name,
                            FullPath = string.IsNullOrEmpty(parentFolder.FullPath) ?
                                subdir.Name :
                                Path.Combine(parentFolder.FullPath, subdir.Name)
                        };

                        await AddSubDirectoriesToTree(subdir.GetDirectories(), childFolder);
                        parentFolder.Children.Add(childFolder);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing subdirectory: {Directory}", subdir.Name);
                }
            }
        }

        private async Task<string> GetCloudFolderTemplateAsync(string attachmentCategory)
        {
            var sql = @"
                SELECT TOP 1 cfsgtat.CloudFolderTemplate
                FROM dbo.CloudFileStorageGroupsToAttachmentTypes AS cfsgtat
                JOIN dbo.CloudFileStoragePermissionFolderTemplate AS cfspft 
                    ON cfspft.CloudFolderTemplate = cfsgtat.CloudFolderTemplate
                JOIN dbo.AttachmentTypes AS at 
                    ON at.AttachmentType = cfsgtat.AttachmentType
                JOIN dbo.AttachmentCategoryTypes AS act 
                    ON act.AttachmentType = cfsgtat.AttachmentType
                WHERE act.AttachmentCategory = @attachmentCategory";
            var sqlParams = new SqlParameter("@attachmentCategory", attachmentCategory);
            var templates = await _dbContext.ExecuteSqlQueryAsync<CloudFolderTemplateResultDto>(sql, sqlParams);
            return templates?.FirstOrDefault()?.CloudFolderTemplate ?? string.Empty;
        }

        public async Task<FileStorageUserResponse> GetFileStorageUsersAsync(string entityNo, string attachmentType, string folderName)
        {
            var response = new FileStorageUserResponse();
            var folderTemplate = await GetCloudFolderTemplateAsync("Projects");
            var idLevelsSql = @"
                SELECT CloudFolderTemplate, id_Level 
                FROM dbo.CloudFileStorageGroupsToPermissionFolders 
                WHERE CloudFolderTemplate = @template";

            var parameters = new[] { new SqlParameter("@template", folderTemplate) };
            var idLevels = await _dbContext.ExecuteSqlQueryAsync<CloudFolderTemplateDto>(idLevelsSql, parameters) ?? new();

            var availableUserDtos = await GetAvailableUserDto(entityNo, idLevels);
            response.AvailableUsers = _mapper.Map<List<UserModel>>(availableUserDtos);

            var mappedUserDtos = await GetMappedUsersDto(entityNo, folderTemplate, folderName);
            response.MappedUsers = _mapper.Map<List<UserModel>>(mappedUserDtos);

            return response;
        }

        private async Task<List<AvailableUserDto>> GetAvailableUserDto(string entityNo, List<CloudFolderTemplateDto> idLevels)
        {
            var parameters = new[] { new SqlParameter("@entityNo", entityNo) };
            var userDtos = await _dbContext.ExecuteStoredProcedureAsync<AvailableUserDto>("get_fileExplorer_Dropbox_Invitees", parameters);
            var idLevelIds = idLevels.Select(x => x.id_Level).ToList();
            return userDtos?.Where(x => x.LevelId != null && idLevelIds.Contains(x.LevelId.Value)).ToList() ?? new();
        }

        private string ConvertToWindowsPath(string dropboxPath)
        {
            return dropboxPath.Replace("/", "\\");
        }

        private string ConvertToDropboxPath(string windowsPath)
        {
            return windowsPath.Replace("\\", "/");
        }

        private async Task<string> GetDropboxFolderId(string folderName, string entityNo)
        {
            var userFolders = await GetUserFolders(entityNo);
            if (userFolders == null || !userFolders.Any())
                return string.Empty;
            var windowsPath = ConvertToWindowsPath(folderName);
            var dropboxPath = ConvertToDropboxPath(folderName);
            var matchingFolder = userFolders.FirstOrDefault(f => f.UserFolderPath == windowsPath || f.UserFolderPath == dropboxPath);
            return matchingFolder?.DropboxFolderId ?? string.Empty;
        }

        private async Task<List<UserFolderDto>> GetUserFolders(string entityNo)
        {
            var sql = @"
                SELECT puftcsf.entityno, 
                       puftcsf.UserFolderPath, 
                       puftcsf.dropboxFolderID, 
                       puftcsf.CloudFolderTemplate, 
                       puftcsf.AttachmentType, 
                       puftcsf.id_Level, 
                       cfspft.DropboxFilePathSuffix 
                FROM dbo.ProjectsUsersFoldersToCloudStorageFolders AS puftcsf
                JOIN dbo.CloudFileStoragePermissionFolderTemplate AS cfspft 
                    ON cfspft.CloudFolderTemplate = puftcsf.CloudFolderTemplate
                WHERE puftcsf.entityno = @entityNo";

            var parameters = new[] { new SqlParameter("@entityNo", entityNo) };
            return await _dbContext.ExecuteSqlQueryAsync<UserFolderDto>(sql, parameters);
        }

        private async Task<List<MappedUserDto>> GetMappedUsersDto(string entityNo, string folderTemplate, string folderName)
        {
            var mappedUsersSql = @"
                with available as (
                    select c.contactname as Name, c.email, cccfsg.id_Level  
                    FROM dbo.ProjectClientContacts pcc  
                    JOIN dbo.ContactCategory cc on pcc.id_ContactCategory = cc.id_ContactCategory  
                    JOIN dbo.contacts c on pcc.ContactNo = c.contactno
                    JOIN dbo.ContactCategoriesCloudFileStorageGroups AS cccfsg ON cccfsg.id_ContactCategory = cc.id_ContactCategory  
                    WHERE (pcc.entityno = @entityNo or pcc.entityno like @entityNo + '-%')
                    union
                    select Name = e.firstname + ' ' + e.lastname, e.email, id_level = 3   --all confirmed crew are an ID level of 3   
                    FROM dbo.pjempassign a  
                    JOIN dbo.pejob j on a.jobtype = j.jobtype and j.is_qualification = 1  
                    JOIN dbo.glentities g on a.entityno = g.entityno and g.engactivecd <> 'I' 
                    JOIN dbo.peemployee e on a.empno = e.empno  
                    WHERE a.StatusCode = 'A' AND (a.entityno = @entityNo or a.entityno like @entityNo + '-%')
                    union
                    select TeamMemberName as Name, email, id_Level
                    from dbo.fn_GetProjectCoreTeam(@entityNo)
                    join dbo.CloudFileStoragGroupsToCoreTeam on CoreTeamMemberDesc = TeamMemberRole
                )
                select 
                    a.name as Name, 
                    c.email as Email,
                    a.id_Level,
                    c.CloudFolderTemplate,
                    c.isRemoveFolderMember,
                    c.isAddFolderMember
                from available a
                right outer join dbo.CloudFileStorageShareRequests c on c.email = a.email
                where c.entityno = @entityNo;";

            var parameters = new[] { new SqlParameter("@entityNo", entityNo) };

            var mappedUsersDtos = await _dbContext.ExecuteSqlQueryAsync<MappedUserDto>(mappedUsersSql, parameters);
            if (mappedUsersDtos == null)
                return new List<MappedUserDto>();
            var dropboxFolderId = await GetDropboxFolderId(folderName, entityNo);
            if (string.IsNullOrEmpty(dropboxFolderId))
                return new List<MappedUserDto>();
            return mappedUsersDtos
                .Where(x => x.isRemoveFolderMember.HasValue
                    && !x.isRemoveFolderMember.Value
                    && x.CloudFolderTemplate == folderTemplate)
                .ToList();
        }

        public async Task<FileExplorerResponse> GetFileExplorerContentsAsync(string entityNo, string attachmentType, string fullPath)
        {
            var response = new FileExplorerResponse();
            var currentGlobalOpsFolder = Path.Combine(await GetCurrentGlobalOpsFolder(entityNo), fullPath);

            if (string.IsNullOrEmpty(currentGlobalOpsFolder) || !Directory.Exists(currentGlobalOpsFolder))
            {
                return response;
            }
            var uploadedFiles = await GetUploadedFiles(entityNo);
            try
            {
                var files = Directory.GetFiles(currentGlobalOpsFolder)
                    .Where(f => !File.GetAttributes(f).HasFlag(FileAttributes.Hidden))
                    .Select(f => new FileItem
                    {
                        Name = Path.GetFileName(f),
                        Size = new FileInfo(f).Length,
                        LastModified = File.GetLastWriteTime(f),
                        Type = Path.GetExtension(f),
                        Creator = GetOwnerName(f),
                        IsUploaded = uploadedFiles.Any(uf =>
                            uf.FileName == Path.GetFileName(f) &&
                            uf.UserFolderPath == fullPath)
                    }).ToList();
                response.Files = files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting files from folder: {Folder}", currentGlobalOpsFolder);
            }
            try
            {
                var folders = Directory.GetDirectories(currentGlobalOpsFolder)
                    .Where(d => !d.Contains("- Archive"))
                    .Select(d => new FolderItem
                    {
                        Name = Path.GetFileName(d),
                        Type = "Folder",
                        Creator = GetOwnerName(d),
                        LastModified = Directory.GetLastWriteTime(d),
                    })
                    .ToList();

                response.Folders = folders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting folders from folder: {Folder}", currentGlobalOpsFolder);
            }

            return response;
        }
        private string GetOwnerName(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            if (OperatingSystem.IsWindows())
            {
                FileSecurity fs = fileInfo.GetAccessControl();
                IdentityReference? sid = fs.GetOwner(typeof(SecurityIdentifier));
                if (sid == null)
                {
                    _logger.LogError("Unable to retrieve file owner.");
                    return string.Empty;
                }
                IdentityReference ntAccount = sid.Translate(typeof(NTAccount));
                return ntAccount.ToString();
            }
            else
            {
                _logger.LogError("Getting file owner is only supported on Windows.");
                return string.Empty;
            }
        }

        private async Task<string> GetCurrentGlobalOpsFolder(string entityNo)
        {
            var rootPath = GetRootFolderPath(entityNo);
            if (string.IsNullOrEmpty(rootPath))
            {
                return await GetRootDefaultFolderPath(entityNo);
            }
            return rootPath;
        }

        private async Task<string?> GetGlobalOpsRootDir()
        {
            var sql = @"
                    SELECT ac.AttachmentCategory, 
                           ac.attachmentsCanBeInKnowledgeBase, 
                           is_rolemember('KnowledgeBaseEditors') as CanEditKnowledgeBase, 
                           ac.defaultRootFolderPath
                    FROM dbo.AttachmentCategory AS ac
                    WHERE AttachmentCategory = 'Projects'";
            var categoryDetails = await _dbContext.ExecuteSqlQueryAsync<AttachmentCategoryDto>(sql);
            return categoryDetails?.FirstOrDefault()?.DefaultRootFolderPath;
        }

        private string GetRootFolderPath(string entityNo)
        {
            currentGuid = _dbContext.Glentities.Where(e => e.Entityno == entityNo)?.FirstOrDefault()?.Guid ?? Guid.Empty;
            return _dbContext.FileStoragePaths.Where(fsp => fsp.FileStorageGuid == currentGuid)?.FirstOrDefault()?.FileStoragePath1 ?? string.Empty;
        }

        private async Task<string> GetRootDefaultFolderPath(string entityNo)
        {
            if (currentGuid == Guid.Empty)
            {
                currentGuid = _dbContext.Glentities.Where(e => e.Entityno == entityNo).FirstOrDefault()?.Guid ?? throw new ArgumentException("Invalid entity number", nameof(entityNo));
            }
            var parameters = new[]
            {
                new SqlParameter("@GUID", currentGuid),
                new SqlParameter("@AttachmentType", "Projects"),
                new SqlParameter
                {
                    ParameterName = "@path",
                    SqlDbType = SqlDbType.VarChar,
                    Size = 255,
                    Direction = ParameterDirection.Output
                }
            };
            await _dbContext.ExecuteStoredProcedureNonQueryOutputParamAsync("get_default_file_Storage_path", parameters);
            return parameters[2].Value?.ToString() ?? string.Empty;
        }

        private async Task<List<CloudFileStorageUploadedFile>> GetUploadedFiles(string entityNo)
        {
            return await _dbContext.CloudFileStorageUploadedFiles.Where(c => c.Entityno == entityNo).ToListAsync();
        }

        public async Task<FolderModel> CreateFolderAsync(string entityNo, CreateFolderRequest request)
        {
            var currentGlobalOpsFolder = await GetCurrentGlobalOpsFolder(entityNo);
            if (string.IsNullOrEmpty(currentGlobalOpsFolder) || !Directory.Exists(currentGlobalOpsFolder))
            {
                await CreateFileStoragePathEntry(currentGlobalOpsFolder);
            }
            var newFolderPath = Path.Combine(currentGlobalOpsFolder, request.ParentPath ?? string.Empty, request.FolderName);
            Directory.CreateDirectory(newFolderPath);
            var template = "PRODUCTION";
            if (!string.IsNullOrEmpty(request.ParentPath))
            {
                var parentFolder = await GetParentFolderInfo(entityNo, request.ParentPath);
                if (parentFolder != null)
                {
                    template = parentFolder.CloudFolderTemplate ?? template;
                }
            }
            var idLevel = GetIdLevelFromTemplateType(template);
            try
            {
                _dbContext.ProjectsUsersFoldersToCloudStorageFolders.Add(new ProjectsUsersFoldersToCloudStorageFolder()
                {
                    Entityno = entityNo,
                    UserFolderPath = Path.Combine(request.ParentPath ?? string.Empty, request.FolderName),
                    CloudFolderTemplate = template,
                    IdLevel = idLevel
                });
                _dbContext.SaveChanges();
                return new FolderModel
                {
                    Name = request.FolderName,
                    Type = "Folder",
                    FullPath = Path.Combine(request.ParentPath ?? string.Empty, request.FolderName)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating folder in database for entityNo: {entityNo}, folderName: {folderName}", entityNo, request.FolderName);
                throw;
            }
        }

        private async Task CreateFileStoragePathEntry(string folderPath)
        {
            var parameters = new[]
            {
                new SqlParameter("@GUID", SqlDbType.UniqueIdentifier) { Value = currentGuid },
                new SqlParameter("@folderPath", folderPath)
            };
            await _dbContext.ExecuteStoredProcedureNonQueryAsync("createFileStoragePathEntry", parameters);
        }

        private async Task<UserFolderDto?> GetParentFolderInfo(string entityNo, string parentPath)
        {
            var sql = @"
                SELECT entityno, UserFolderPath, dropboxFolderID, CloudFolderTemplate, id_Level,AttachmentType,'' as DropboxFilePathSuffix
                FROM dbo.ProjectsUsersFoldersToCloudStorageFolders
                WHERE entityno = @entityNo AND UserFolderPath = @parentPath";
            var parameters = new[]
            {
                new SqlParameter("@entityNo", entityNo),
                new SqlParameter("@parentPath", parentPath)
            };
            var result = await _dbContext.ExecuteSqlQueryAsync<UserFolderDto>(sql, parameters);
            return result?.FirstOrDefault();
        }

        private int GetIdLevelFromTemplateType(string template)
        {
            var cloudPermissionTemplate = _dbContext.CloudFileStoragePermissionFolderTemplates
                .Where(c => c.CloudFolderTemplate == template)
                .FirstOrDefault();
            return cloudPermissionTemplate?.IdLevels?.FirstOrDefault()?.IdLevel ?? 1;
        }

        public async Task<FileItem> AddFileAsync(string entityNo, AddFileRequest request)
        {
            if (request.File == null || request.File.Length == 0 || string.IsNullOrEmpty(request.Path))
            {
                throw new ArgumentException("File and Path is required", nameof(request.File));
            }
            var currentGlobalOpsFolder = await GetCurrentGlobalOpsFolder(entityNo);
            var targetPath = Path.Combine(currentGlobalOpsFolder, request.Path);
            if (string.IsNullOrEmpty(currentGlobalOpsFolder) || !Directory.Exists(currentGlobalOpsFolder) || !Directory.Exists(targetPath))
            {
                var fullPath = request.Path ?? string.Empty;
                fullPath = fullPath.Replace(@"\\", Path.DirectorySeparatorChar.ToString());
                await CreateFolderAsync(entityNo, new CreateFolderRequest()
                {
                    FolderName = Path.GetFileName(fullPath),
                    ParentPath = Path.GetDirectoryName(fullPath) ?? string.Empty,
                });
            }
            var directoryPath = Path.GetDirectoryName(targetPath);
            var filePath = Path.Combine(targetPath, request.FileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }
            var fileInfo = new FileInfo(filePath);
            return new FileItem
            {
                Name = request.FileName,
                Type = Path.GetExtension(request.FileName),
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                Creator = GetOwnerName(filePath),
                IsUploaded = false
            };
        }

        public async Task DeleteItemAsync(string entityNo, string path, bool isFolder)
        {
            var currentGlobalOpsFolder = await GetCurrentGlobalOpsFolder(entityNo);
            if (string.IsNullOrEmpty(currentGlobalOpsFolder))
            {
                throw new DirectoryNotFoundException("Root folder not found");
            }
            var fullPath = Path.Combine(currentGlobalOpsFolder, path);
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                throw new FileNotFoundException("Item not found", path);
            }
            try
            {
                if (isFolder)
                {
                    Directory.Delete(fullPath, true);
                }
                else
                {
                    File.Delete(fullPath);
                }
                if (isFolder)
                {
                    _dbContext.ProjectsUsersFoldersToCloudStorageFolders.RemoveRange(
                        _dbContext.ProjectsUsersFoldersToCloudStorageFolders
                        .Where(f => f.Entityno == entityNo && f.UserFolderPath == path));
                    _dbContext.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting {ItemType} at path: {Path}", isFolder ? "folder" : "file", path);
                throw;
            }
        }

        public async Task ShareFolderAsync(string entityNo, string fullPath, string attachmentType, List<ShareRequest> requests)
        {
            var template = await GetCloudFolderTemplateAsync("Projects");
            if (string.IsNullOrEmpty(template))
            {
                throw new InvalidOperationException("Files from this folder cannot be shared");
            }
            var currentGlobalOpsFolder = await GetCurrentGlobalOpsFolder(entityNo);
            var targetPath = Path.Combine(currentGlobalOpsFolder, fullPath);
            if (string.IsNullOrEmpty(currentGlobalOpsFolder) || !Directory.Exists(currentGlobalOpsFolder) || !Directory.Exists(targetPath))
            {
                await CreateFolderAsync(entityNo, new CreateFolderRequest()
                {
                    FolderName = Path.GetFileName(fullPath ?? string.Empty),
                    ParentPath = fullPath ?? string.Empty,
                });
            }
            await CheckIfPathHasDbEntry(entityNo, fullPath ?? string.Empty, template ?? "PRODUCTION", attachmentType);
            foreach (var request in requests)
            {
                var parameters = new[]
                {
                    new SqlParameter("@email", request.Email),
                    new SqlParameter("@entityno", entityNo),
                    new SqlParameter("@cloudFolderTemplate", template),
                    new SqlParameter("@attachmentCategory", "Projects"),
                    new SqlParameter("@UserFolderPath", fullPath),
                    new SqlParameter("@note", request.Note)
                };
                await _dbContext.ExecuteStoredProcedureNonQueryAsync("create_dropbox_share_request", parameters);
            }
        }

        private async Task CheckIfPathHasDbEntry(string entityNo, string fullPath, string template, string attachmentType)
        {
            var userFolders = await GetUserFolders(entityNo);
            if (!userFolders.Any(uf => uf.UserFolderPath == fullPath && uf.EntityNo == entityNo))
            {
                var idLevel = GetIdLevelFromTemplateType(template);
                var userFolderExists = _dbContext.ProjectsUsersFoldersToCloudStorageFolders.Any(e => e.Entityno == entityNo && e.UserFolderPath == fullPath);
                if (!userFolderExists)
                {
                    _dbContext.ProjectsUsersFoldersToCloudStorageFolders.Add(new ProjectsUsersFoldersToCloudStorageFolder
                    {
                        Entityno = entityNo,
                        UserFolderPath = fullPath,
                        CloudFolderTemplate = template,
                        IdLevel = idLevel,
                        AttachmentType = attachmentType
                    });
                    _dbContext.SaveChanges();
                }
            }
        }

        public async Task UnshareFolderAsync(string entityNo, string fullPath, string attachmentType, List<ShareRequest> requests)
        {
            var template = await GetCloudFolderTemplateAsync("Projects");
            if (string.IsNullOrEmpty(template))
            {
                throw new InvalidOperationException("Files from this folder cannot be unshared");
            }
            await CheckIfPathHasDbEntry(entityNo, fullPath, template ?? "PRODUCTION", attachmentType);
            var emails = requests.Select(r => r.Email).ToList();
            await _dbContext.CloudFileStorageShareRequests
                .Where(r => emails.Contains(r.Email) && r.Entityno == entityNo && r.CloudFolderTemplate == template)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.IsExecuted, false)
                    .SetProperty(r => r.IsAddFolderMember, false)
                    .SetProperty(r => r.IsRemoveFolderMember, true));
        }

        public async Task<(byte[] FileContents, string ContentType, string FileName)> DownloadFileAsync(string entityNo, string filePath)
        {
            var currentGlobalOpsFolder = await GetCurrentGlobalOpsFolder(entityNo);
            if (string.IsNullOrEmpty(currentGlobalOpsFolder))
            {
                throw new DirectoryNotFoundException("Root folder not found");
            }

            var fullPath = Path.Combine(currentGlobalOpsFolder, filePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("File not found", filePath);
            }

            var fileInfo = new FileInfo(fullPath);
            var fileContents = await File.ReadAllBytesAsync(fullPath);
            var contentType = GetContentType(fileInfo.Extension);
            var fileName = fileInfo.Name;
            return (fileContents, contentType, fileName);
        }

        public async Task UploadItemAsync(string entityNo, List<UploadRequest> requests)
        {
            var template = await GetCloudFolderTemplateAsync("Projects");
            if (string.IsNullOrEmpty(template))
            {
                throw new InvalidOperationException("Files from this folder cannot be uploaded");
            }
            var currentGlobalOpsFolder = await GetCurrentGlobalOpsFolder(entityNo);
            foreach (var request in requests)
            {
                if (request == null || string.IsNullOrEmpty(request.Path))
                {
                    throw new ArgumentException("Request and Path are required", nameof(request));
                }
                else
                {
                    var targetPath = Path.Combine(currentGlobalOpsFolder, request.Path);
                    var pathTillLastFolder = request.IsFile ? Path.GetDirectoryName(targetPath) : Path.GetDirectoryName(Path.GetDirectoryName(targetPath));
                    if (string.IsNullOrEmpty(currentGlobalOpsFolder) || !Directory.Exists(currentGlobalOpsFolder) || !Directory.Exists(pathTillLastFolder))
                    {
                        await CreateFolderAsync(entityNo, new CreateFolderRequest()
                        {
                            FolderName = request.IsFile ? Path.GetFileName(Path.GetDirectoryName(request.Path) ?? string.Empty) : Path.GetFileName(request.Path),
                            ParentPath = request.Path
                        });
                    }
                    await CheckIfPathHasDbEntry(entityNo, request.Path, template, request.AttachmentType ?? string.Empty);
                    var folderPath = Path.Combine(currentGlobalOpsFolder, request.Path);
                    if (request.IsFile && !File.Exists(targetPath))
                    {
                        throw new DirectoryNotFoundException($"File/Folder not found: {request.Path}");
                    }
                    else if (!Directory.Exists(pathTillLastFolder))
                    {
                        throw new DirectoryNotFoundException($"Directory Not found: {request.Path}");
                    }
                    if (request.IsFile)
                    {
                        await ProcessFileUpload(entityNo, Path.GetDirectoryName(request.Path) ?? string.Empty, Path.GetFileName(folderPath), template, request.AttachmentType);
                    }
                    else
                    {
                        var files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            await ProcessFileUpload(entityNo, request.Path, Path.GetFileName(file), template, request.AttachmentType);
                        }
                    }
                }
            }
        }

        private async Task ProcessFileUpload(string entityNo, string targetPath, string filename, string template, string? attachmentType)
        {
            var parameters = new[]
            {
                new SqlParameter("@filename", filename),
                new SqlParameter("@entityno", entityNo),
                new SqlParameter("@attachmentType", attachmentType ?? string.Empty),
                new SqlParameter("@attachmentCategory", "Projects"),
                new SqlParameter("@UserFolderPath", targetPath)
            };
            await _dbContext.ExecuteStoredProcedureNonQueryAsync("create_dropbox_file_upload_request", parameters);
            var userFolder = await _dbContext.ProjectsUsersFoldersToCloudStorageFolders.FirstOrDefaultAsync(f => f.UserFolderPath == targetPath && f.Entityno == entityNo);
            if (userFolder != null)
            {
                userFolder.DropboxFolderId = "Temp";
                await _dbContext.SaveChangesAsync();
            }
        }

        private string GetContentType(string extension)
        {
            return extension.ToLower() switch
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
    }
}
