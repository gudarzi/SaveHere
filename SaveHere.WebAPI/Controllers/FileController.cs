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
