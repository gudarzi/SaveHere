namespace SaveHere.Helpers
{
  public static class DirectoryBrowser
  {
    private static string _downloadsPath = ".";

    public static string DownloadsPath
    {
      get
      {
        if (string.IsNullOrEmpty(_downloadsPath))
        {
          InitializeDownloadsDirectory();
        }
        return _downloadsPath;
      }
    }

    public static void InitializeDownloadsDirectory()
    {
      try
      {
        _downloadsPath = Path.Combine(Directory.GetCurrentDirectory(), "downloads");
        if (!Directory.Exists(_downloadsPath))
        {
          Directory.CreateDirectory(_downloadsPath);
        }
      }
      catch (Exception ex)
      {
        throw new DirectoryAccessException("Failed to initialize downloads directory.", ex);
      }
    }

    public static List<FileSystemItem> GetDownloadsContent()
    {
      try
      {
        var dirInfo = new DirectoryInfo(DownloadsPath);
        return GetDirectoryContent(dirInfo);
      }
      catch (Exception ex)
      {
        throw new DirectoryAccessException($"Failed to access downloads directory at {DownloadsPath}", ex);
      }
    }

    public static List<FileSystemItem> GetDirectoryContent(DirectoryInfo dirInfo)
    {
      try
      {
        var result = new List<FileSystemItem>();

        foreach (var fileSystemInfo in dirInfo.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
        {
          FileSystemItem item = fileSystemInfo switch
          {
            FileInfo fi => new FileItem
            {
              Name = fi.Name,
              FullName = fi.FullName,
              Type = "file",
              Length = fi.Length,
              Extension = fi.Extension,
              LastModified = fi.LastWriteTimeUtc,
              CreatedAt = fi.CreationTimeUtc
            },
            DirectoryInfo di => new DirectoryItem
            {
              Name = di.Name,
              FullName = di.FullName,
              Type = "directory",
              Children = GetDirectoryContent(di)
            },
            _ => new FileSystemItem
            {
              Name = fileSystemInfo.Name,
              FullName = fileSystemInfo.FullName,
              Type = "default"
            }
          };

          result.Add(item);
        }

        return result.OrderBy(r => r.Type).ThenBy(r => r.Name).ToList();
      }
      catch (Exception ex)
      {
        throw new DirectoryAccessException($"Failed to read directory content at {dirInfo.FullName}", ex);
      }
    }

    public static void DeleteFileItem(FileItem item)
    {
      File.Delete(item.FullName);
    }

    public static void DeleteDirectoryItem(DirectoryItem item)
    {
      Directory.Delete(item.FullName, true);
    }

    public static bool RenameFileSystemItem(FileSystemItem item, string newName)
    {
      try
      {
        // Validate the new name
        if (string.IsNullOrWhiteSpace(newName) ||
            newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
          return false;
        }

        // Get directory of the item
        string? parentDirectory = Path.GetDirectoryName(item.FullName);
        if (string.IsNullOrEmpty(parentDirectory))
        {
          return false;
        }

        // Construct new path
        string newPath = Path.Combine(parentDirectory, newName);

        // Normalize paths for security check
        string normalizedNewPath = Path.GetFullPath(newPath);
        string normalizedBasePath = Path.GetFullPath(DownloadsPath);

        // Security check to prevent path traversal
        if (!normalizedNewPath.StartsWith(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
        {
          return false; // Attempted path traversal
        }

        // Check if target already exists
        if (File.Exists(newPath) || Directory.Exists(newPath))
        {
          return false; // Target already exists
        }

        if (item is FileItem)
        {
          // Rename file
          File.Move(item.FullName, newPath);
          item.FullName = newPath;
          item.Name = newName;

          // If it's a file item, update the extension property
          if (item is FileItem fileItem)
          {
            fileItem.Extension = Path.GetExtension(newName);
          }
        }
        else if (item is DirectoryItem)
        {
          // Rename directory
          Directory.Move(item.FullName, newPath);
          item.FullName = newPath;
          item.Name = newName;
        }
        return true;
      }
      catch
      {
        return false;
      }
    }

    public static string GetUserDownloadsPath(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));

        // Always build on top of the original root, not _downloadsPath
        var rootPath = Path.Combine(Directory.GetCurrentDirectory(), "downloads");
        var userDownloadsPath = Path.Combine(rootPath, userId);

        if (!Directory.Exists(userDownloadsPath))
        {
            Directory.CreateDirectory(userDownloadsPath);
        }

        return userDownloadsPath;
    }
  }

  public class FileSystemItem
  {
    public string Name { get; set; } = "a.bbb";
    public string FullName { get; set; } = "C:\\a.bbb";
    public string Type { get; set; } = "bbb";
  }

  public class FileItem : FileSystemItem
  {
    public long Length { get; set; }
    public string Extension { get; set; } = "bbb";
    public DateTime LastModified { get; set; }
    public DateTime CreatedAt { get; set; }
  }

  public class DirectoryItem : FileSystemItem
  {
    public List<FileSystemItem> Children { get; set; }=new List<FileSystemItem>();
  }

  public class DirectoryAccessException : Exception
  {
    public DirectoryAccessException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
  }

}
