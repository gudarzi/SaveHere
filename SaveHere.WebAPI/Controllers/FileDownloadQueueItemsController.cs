using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaveHere.WebAPI.DTOs;
using SaveHere.WebAPI.Models;
using SaveHere.WebAPI.Models.db;
using System.Collections.Concurrent;

namespace SaveHere.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FileDownloadQueueItemsController : ControllerBase
{
  private readonly AppDbContext _context;
  private readonly HttpClient _httpClient;
  private static ConcurrentDictionary<int, CancellationTokenSource> _cancellationTokenSources = new ConcurrentDictionary<int, CancellationTokenSource>();

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

  // POST: api/FileDownloadQueueItems/canceldownload
  [HttpPost("canceldownload")]
  public IActionResult CancelFileDownload([FromBody] FileDownloadCancelRequestDTO request)
  {
    if (!ModelState.IsValid)
    {
      return BadRequest(ModelState);
    }

    try
    {
      // Find the corresponding CancellationTokenSource based on the request ID
      if (_cancellationTokenSources.TryRemove(request.Id, out var cts))
      {
        cts.Cancel();
        return Ok("File download cancelled successfully.");
      }

      return NotFound("Download request not found!");
    }
    catch (Exception ex)
    {
      // Log the exception
      return StatusCode(500, $"An error occurred while cancelling the file download.\n{ex.Message}");
    }
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
    fileDownloadQueueItem.ProgressPercentage = 0;
    _context.SaveChanges();

    // Create a new CancellationTokenSource instance to manage cancellation for the current download request
    var cts = new CancellationTokenSource();

    if (!_cancellationTokenSources.TryAdd(request.Id, cts))
    {
      return BadRequest("File download has already started!");
    }

    try
    {
      var downloadResult = await DownloadFile(fileDownloadQueueItem, request.UseHeadersForFilename ?? true, cts.Token);

      if (downloadResult)
      {
        fileDownloadQueueItem.Status = EQueueItemStatus.Finished;
        await _context.SaveChangesAsync();
        return Ok("File downloaded successfully.");
      }
      else
      {
        fileDownloadQueueItem.Status = EQueueItemStatus.Paused;
        await _context.SaveChangesAsync();
        return BadRequest("There was an issue in downloading the file!");
      }
    }
    catch (OperationCanceledException)
    {
      fileDownloadQueueItem.Status = EQueueItemStatus.Cancelled;
      await _context.SaveChangesAsync();
      return Ok("File download was cancelled.");
    }
    catch (Exception ex)
    {
      // Log the exception
      return StatusCode(500, $"An error occurred while downloading the file.\n{ex.Message}");
    }
    finally
    {
      _cancellationTokenSources.TryRemove(request.Id, out _);
    }
  }

  [NonAction]
  public async Task<bool> DownloadFile(FileDownloadQueueItem queueItem, bool UseHeadersForFilename, CancellationToken cancellationToken)
  {
    // Validate the URL (must use either HTTP or HTTPS schemes)
    if (string.IsNullOrEmpty(queueItem.InputUrl) ||
        !Uri.TryCreate(queueItem.InputUrl, UriKind.Absolute, out Uri? uriResult) ||
        !(uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
    {
      return false;
    }

    try
    {
      var fileName = Path.GetFileName(System.Web.HttpUtility.UrlDecode(queueItem.InputUrl));

      var response = await _httpClient.GetAsync(queueItem.InputUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

      if (!response.IsSuccessStatusCode) return false;

      if (UseHeadersForFilename)
      {
        var contentDisposition = response.Content.Headers.ContentDisposition;

        if (contentDisposition != null)
        {
          if (!string.IsNullOrEmpty(contentDisposition.FileNameStar)) fileName = System.Web.HttpUtility.UrlDecode(contentDisposition.FileNameStar.Replace("\"", ""));
          else if (!string.IsNullOrEmpty(contentDisposition.FileName)) fileName = System.Web.HttpUtility.UrlDecode(contentDisposition.FileName.Replace("\"", ""));
        }
      }

      // Ensure the filename is safe by removing invalid characters
      fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
      if (string.IsNullOrWhiteSpace(fileName)) fileName = "unnamed_" + DateTime.Now.ToString("yyyyMMddHHmmss");

      // Construct the file path using the base directory and the sanitized filename
      var localFilePath = Path.GetFullPath(Path.Combine("/app/downloads", fileName));

      // Ensure the file path is within the intended directory
      if (!localFilePath.StartsWith(Path.GetFullPath("/app/downloads"), StringComparison.OrdinalIgnoreCase))
      {
        throw new UnauthorizedAccessException("Invalid file path.");
      }

      // Check if a file with the same name already exists
      string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
      string fileExtension = Path.GetExtension(fileName);
      int digit = 1;

      while (System.IO.File.Exists(localFilePath))
      {
        fileName = $"{fileNameWithoutExtension}_{digit}{fileExtension}";
        localFilePath = Path.Combine("/app/downloads", fileName);
        digit++;
      }

      using (var download = await response.Content.ReadAsStreamAsync())
      {
        using var stream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write);
        var contentLength = response.Content.Headers.ContentLength;

        // Ignore progress reporting when the ContentLength's header is not available
        if (!contentLength.HasValue)
        {
          await download.CopyToAsync(stream, cancellationToken);
          queueItem.ProgressPercentage = 100;
          await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
          var buffer = new byte[81920]; // 80KB buffer (default buffer size used by Microsoft's CopyTo method in Stream)
          long totalBytesRead = 0;
          int bytesRead;

          // To avoid slowing down the process we should not be saving changes to the context on every iteration
          int saveInterval = 10;
          int counter = 0;

          while ((bytesRead = await download.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
          {
            // Check if cancellation is requested
            if (cancellationToken.IsCancellationRequested)
            {
              throw new OperationCanceledException(cancellationToken);
            }
            await stream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytesRead += bytesRead;
            queueItem.ProgressPercentage = (int)(100.0 * totalBytesRead / contentLength);
            counter++;

            // Save progress to the database at regular intervals
            if (counter >= saveInterval)
            {
              await _context.SaveChangesAsync(cancellationToken);
              counter = 0;
            }
          }

          // Save any remaining changes
          await _context.SaveChangesAsync(cancellationToken);
        }
      }

      // Fixing file permissions on linux
      if (OperatingSystem.IsLinux()) System.IO.File.SetUnixFileMode(localFilePath,
          UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead |
          UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite |
          UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }
    catch (OperationCanceledException)
    {
      // Rethrow the OperationCanceledException to be caught by the caller
      throw;
    }
    catch
    {
      return false;
    }

    return true;
  }

}
