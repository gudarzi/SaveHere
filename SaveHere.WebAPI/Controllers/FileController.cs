using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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

      await _httpClient.GetAsync(url).ContinueWith((task) =>
      {
        if (task.IsFaulted || task.IsCanceled) return;

        using var stream = new FileStream(localFilePath, FileMode.CreateNew);
        task.Result.Content.CopyToAsync(stream);
      });

      return Ok($"/files/{fileName}");
    }

  }
}
