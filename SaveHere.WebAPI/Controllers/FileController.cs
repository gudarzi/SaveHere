using Microsoft.AspNetCore.Mvc;

namespace SaveHere.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FileController : ControllerBase
{
  private readonly HttpClient _httpClient;

  public FileController(HttpClient httpClient)
  {
    _httpClient = httpClient;
  }

  [HttpPost("download")]
  public async Task<IActionResult> DownloadFile([FromBody] string url)
  {
    var fileName = Path.GetFileName(url);
    var localFilePath = Path.Combine("/app/downloads", fileName);

    await _httpClient.GetAsync(url).ContinueWith(async (task) =>
    {
      if (task.IsFaulted || task.IsCanceled) return;

      using var stream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write);
      await task.Result.Content.CopyToAsync(stream);

      // Fixing file permissions on linux
      if (System.OperatingSystem.IsLinux()) System.IO.File.SetUnixFileMode(localFilePath,
        UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead | UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    });

    return Ok($"/files/{fileName}");
  }

  [HttpGet("list")]
  public IActionResult ListFiles()
  {
    try
    {
      string rootPath = "/app/downloads";
      DirectoryInfo dirInfo = new DirectoryInfo(rootPath);

      var result = new List<object>();

      foreach (var fileSystemInfo in dirInfo.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
      {
        dynamic obj = new System.Dynamic.ExpandoObject();

        obj.Name = fileSystemInfo.Name;
        obj.FullName = fileSystemInfo.FullName;
        obj.Type = fileSystemInfo switch
        {
          FileInfo fi => "file",
          DirectoryInfo di => "directory",
          _ => "default"
        };

        if (obj.Type == "file")
        {
          obj.Length = ((FileInfo)fileSystemInfo).Length;
          obj.Extension = ((FileInfo)fileSystemInfo).Extension;
        }
        else if (obj.Type == "directory")
        {
          obj.Children = GetDirectoryContent((DirectoryInfo)fileSystemInfo);
        }

        result.Add(obj);
      }

      return Ok(result);
    }
    catch (Exception ex)
    {
      return BadRequest($"An error occurred while listing files: {ex.Message}");
    }
  }

  private List<object> GetDirectoryContent(DirectoryInfo dirInfo)
  {
    List<object> result = new List<object>();

    foreach (var fileSystemInfo in dirInfo.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
    {
      dynamic obj = new System.Dynamic.ExpandoObject();

      obj.Name = fileSystemInfo.Name;
      obj.FullName = fileSystemInfo.FullName;
      obj.Type = fileSystemInfo switch
      {
        FileInfo fi => "file",
        DirectoryInfo di => "directory",
        _ => "default"
      };

      if (obj.Type == "file")
      {
        obj.Length = ((FileInfo)fileSystemInfo).Length;
        obj.Extension = ((FileInfo)fileSystemInfo).Extension;
      }
      else if (obj.Type == "directory")
      {
        obj.Children = GetDirectoryContent((DirectoryInfo)fileSystemInfo);
      }

      result.Add(obj);
    }

    return result;
  }

  [HttpPost("delete")]
  public IActionResult DeleteFile([FromBody] string filePath)
  {
    try
    {
      string baseDirectory = "/app/downloads";
      string absoluteFilePath = Path.GetFullPath(Path.Combine(baseDirectory, filePath));

      if (!absoluteFilePath.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
      {
        throw new Exception("The given file path does not fall under the /app/downloads directory.");
      }

      System.IO.File.Delete(absoluteFilePath);

      return Ok();
    }
    catch (Exception ex)
    {
      return BadRequest($"Error when trying to delete file '{filePath}' - Error Message: {ex.Message}");
    }
  }

  [HttpPost("rename")]
  public IActionResult RenameFile([FromBody] dynamic data)
  {
    try
    {
      string baseDirectory = "/app/downloads";
      string oldFilePath = Path.Combine(baseDirectory, data.oldPath);
      string newFilePath = Path.Combine(baseDirectory, data.newPath);

      if (!oldFilePath.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
      {
        throw new Exception("The source file path does not fall under the /app/downloads directory.");
      }

      if (System.IO.File.Exists(newFilePath))
      {
        throw new Exception("A destination file already exists!");
      }

      System.IO.File.Move(oldFilePath, newFilePath);

      return Ok(new { Success = true, OldPath = oldFilePath, NewPath = newFilePath });
    }
    catch (Exception ex)
    {
      return BadRequest($"Error when trying to rename file '{data.oldPath}' -> '{data.newPath}' - Error Message: {ex.Message}");
    }
  }

}
