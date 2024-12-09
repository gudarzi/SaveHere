using Microsoft.AspNetCore.Mvc;

namespace SaveHere.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FileController : ControllerBase
{
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
      string absoluteFilePath = SanitizeAndValidatePath(baseDirectory, filePath);

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
      string oldFilePath = SanitizeAndValidatePath(baseDirectory, data.oldPath);
      string newFilePath = SanitizeAndValidatePath(baseDirectory, data.newPath);

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

  [NonAction]
  private string SanitizeAndValidatePath(string baseDirectory, string filePath)
  {
    // Ensure the filename is safe by removing invalid characters
    string fileName = string.Join("_", Path.GetFileName(filePath).Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    if (string.IsNullOrWhiteSpace(fileName)) throw new Exception($"The filename cannot be empty.");

    // Reconstruct the file path using the base directory and the sanitized filename
    string fullPath = Path.GetFullPath(Path.Combine(baseDirectory, fileName));

    // Ensure the file path is within the intended directory
    if (!fullPath.StartsWith(Path.GetFullPath(baseDirectory), StringComparison.OrdinalIgnoreCase))
    {
      throw new Exception($"The file path '{filePath}' does not fall under the {baseDirectory} directory.");
    }

    return fullPath;
  }
}
