using Microsoft.AspNetCore.Mvc;
using SaveHere.WebAPI.DTOs;

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
  public async Task<IActionResult> DownloadFile([FromBody] DownloadFileRequestDTO request)
  {
    var url = request.Url;
    var useUrlForFilename = request.UseUrlForFilename ?? false; // defaulting to filename from content-disposition

    var fileName = Path.GetFileName(System.Web.HttpUtility.UrlDecode(url));

    await _httpClient.GetAsync(url).ContinueWith(async (task) =>
    {
      if (task.IsFaulted || task.IsCanceled) return;

      if (!useUrlForFilename)
      {
        var contentDisposition = task.Result.Content.Headers.ContentDisposition;

        if (contentDisposition != null)
        {
          if (!string.IsNullOrEmpty(contentDisposition.FileNameStar)) fileName = System.Web.HttpUtility.UrlDecode(contentDisposition.FileNameStar.Replace("\"", ""));
          else if (!string.IsNullOrEmpty(contentDisposition.FileName)) fileName = System.Web.HttpUtility.UrlDecode(contentDisposition.FileName.Replace("\"", ""));
        }
      }

      fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');

      if (string.IsNullOrWhiteSpace(fileName)) fileName = "unnamed_" + DateTime.Now.ToString("yyyyMMddHHmmss");

      var localFilePath = Path.Combine("/app/downloads", fileName);

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
      var result = Helpers.GetDirectoryContent(dirInfo);
      return Ok(result);
    }
    catch (Exception ex)
    {
      return BadRequest($"An error occurred while listing files: {ex.Message}");
    }
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
