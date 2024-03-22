using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace SaveHere.WebAPI.Controllers
{
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

        if (System.OperatingSystem.IsLinux()) System.IO.File.SetUnixFileMode(localFilePath,
          UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead | UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
      });

      return Ok($"/files/{fileName}");
    }

  }
}
