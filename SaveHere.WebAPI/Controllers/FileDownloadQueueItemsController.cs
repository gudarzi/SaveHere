using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaveHere.WebAPI.DTOs;
using SaveHere.WebAPI.Models;
using SaveHere.WebAPI.Models.db;

namespace SaveHere.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FileDownloadQueueItemsController : ControllerBase
{
  private readonly AppDbContext _context;
  private readonly HttpClient _httpClient;

  public FileDownloadQueueItemsController(AppDbContext context, HttpClient httpClient)
  {
    _context = context;
    _httpClient = httpClient;
  }

  // GET: api/FileDownloadQueueItems
  [HttpGet]
  public async Task<ActionResult<IEnumerable<FileDownloadQueueItem>>> GetFileDownloadQueueItems()
  {
    return await _context.FileDownloadQueueItems.ToListAsync();
  }

  // GET: api/FileDownloadQueueItems/5
  [HttpGet("{id}")]
  public async Task<ActionResult<FileDownloadQueueItem>> GetFileDownloadQueueItem(int id)
  {
    var fileDownloadQueueItem = await _context.FileDownloadQueueItems.FindAsync(id);

    if (fileDownloadQueueItem == null)
    {
      return NotFound();
    }

    return fileDownloadQueueItem;
  }

  // PUT: api/FileDownloadQueueItems/5
  // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
  //[HttpPut("{id}")]
  //public async Task<IActionResult> PutFileDownloadQueueItem(int id, FileDownloadQueueItem fileDownloadQueueItem)
  //{
  //  if (id != fileDownloadQueueItem.Id)
  //  {
  //    return BadRequest();
  //  }

  //  _context.Entry(fileDownloadQueueItem).State = EntityState.Modified;

  //  try
  //  {
  //    await _context.SaveChangesAsync();
  //  }
  //  catch (DbUpdateConcurrencyException)
  //  {
  //    if (!FileDownloadQueueItemExists(id))
  //    {
  //      return NotFound();
  //    }
  //    else
  //    {
  //      throw;
  //    }
  //  }

  //  return NoContent();
  //}

  // POST: api/FileDownloadQueueItems
  // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
  [HttpPost]
  public async Task<ActionResult<FileDownloadQueueItem>> PostFileDownloadQueueItem([FromBody] FileDownloadRequestDTO fileDownloadRequest)
  {
    if (!ModelState.IsValid || string.IsNullOrWhiteSpace(fileDownloadRequest.InputUrl))
    {
      return BadRequest();
    }

    var newFileDownload = new FileDownloadQueueItem() { InputUrl = fileDownloadRequest.InputUrl };
    _context.FileDownloadQueueItems.Add(newFileDownload);
    await _context.SaveChangesAsync();

    return CreatedAtAction("GetFileDownloadQueueItem", new { id = newFileDownload.Id }, newFileDownload);
  }

  // DELETE: api/FileDownloadQueueItems/5
  [HttpDelete("{id}")]
  public async Task<IActionResult> DeleteFileDownloadQueueItem(int id)
  {
    var fileDownloadQueueItem = await _context.FileDownloadQueueItems.FindAsync(id);
    if (fileDownloadQueueItem == null)
    {
      return NotFound();
    }

    _context.FileDownloadQueueItems.Remove(fileDownloadQueueItem);
    await _context.SaveChangesAsync();

    return NoContent();
  }

  // POST: api/FileDownloadQueueItems/startdownload
  [HttpPost("startdownload")]
  public async Task<IActionResult> StartFileDownload([FromBody] FileDownloadQueueItemRequestDTO request)
  {
    if (!ModelState.IsValid)
    {
      return BadRequest(ModelState);
    }

    var fileDownloadQueueItem = await _context.FileDownloadQueueItems.FindAsync(request.Id);

    if (fileDownloadQueueItem == null)
    {
      return NotFound();
    }

    fileDownloadQueueItem.Status = EQueueItemStatus.Downloading;
    _context.SaveChanges();

    // To Do: There's still no way of cancelling the download
    var downloadResult = await DownloadFile(fileDownloadQueueItem, request.UseHeadersForFilename ?? true);

    if (downloadResult)
    {
      fileDownloadQueueItem.Status = EQueueItemStatus.Finished;
      _context.SaveChanges();
      return Ok("File download started successfully.");
    }
    else
    {
      fileDownloadQueueItem.Status = EQueueItemStatus.Paused;
      _context.SaveChanges();
      return BadRequest("There was an issue in downloading the file!");
    }
  }

  //private bool FileDownloadQueueItemExists(int id)
  //{
  //  return _context.FileDownloadQueueItems.Any(e => e.Id == id);
  //}

  [NonAction]
  public async Task<bool> DownloadFile(FileDownloadQueueItem queueItem, bool UseHeadersForFilename = true)
  {
    if (string.IsNullOrEmpty(queueItem.InputUrl)) return false;

    var fileName = Path.GetFileName(System.Web.HttpUtility.UrlDecode(queueItem.InputUrl));

    await _httpClient.GetAsync(queueItem.InputUrl, HttpCompletionOption.ResponseHeadersRead).ContinueWith(async (task) =>
    {
      if (task.IsFaulted || task.IsCanceled) return;

      if (UseHeadersForFilename)
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

      using (var download = await task.Result.Content.ReadAsStreamAsync())
      {
        using var stream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write);
        var contentLength = task.Result.Content.Headers.ContentLength;

        // Ignore progress reporting when the ContentLength's header is not available
        if (!contentLength.HasValue)
        {
          queueItem.ProgressPercentage = 0;
          _context.SaveChanges();
          await download.CopyToAsync(stream);
          queueItem.ProgressPercentage = 100;
          _context.SaveChanges();
        }
        else
        {
          var buffer = new byte[81920]; // default buffer size used by Microsoft's CopyTo method in Stream
          long totalBytesRead = 0;
          int bytesRead;

          while ((bytesRead = download.Read(buffer, 0, buffer.Length)) != 0)
          {
            // To Do: This code has issues. It exists earlier than expected, resulting in unfinished downloads!

            //Thread.Sleep(2000); // For testing purposes!
            await stream.WriteAsync(buffer, 0, bytesRead);
            totalBytesRead += bytesRead;
            queueItem.ProgressPercentage = (int)(100.0 * totalBytesRead / contentLength);
            _context.SaveChanges();
          }

          queueItem.ProgressPercentage = 100;
          _context.SaveChanges();
        }
      }

      // Fixing file permissions on linux
      if (OperatingSystem.IsLinux()) System.IO.File.SetUnixFileMode(localFilePath,
        UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead |
        UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite |
        UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }).ConfigureAwait(false);

    return true;
  }

}
