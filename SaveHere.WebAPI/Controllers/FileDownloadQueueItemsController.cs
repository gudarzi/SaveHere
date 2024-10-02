using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaveHere.WebAPI.DTOs;
using SaveHere.WebAPI.Models;
using SaveHere.WebAPI.Models.db;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;

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
    Console.WriteLine("StartFileDownload");
    Console.WriteLine(request.Id);
    Console.WriteLine(request.UseHeadersForFilename);
    Console.WriteLine(request.proxyServer.Host);
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
      var downloadResult = await DownloadFile(fileDownloadQueueItem, request.UseHeadersForFilename ?? true, cts.Token, request.proxyServer);

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
  public async Task<bool> DownloadFile(FileDownloadQueueItem queueItem, bool UseHeadersForFilename, CancellationToken cancellationToken, ProxyServer? proxyServer = null)
  {
    Console.WriteLine("Downloading file...step1");
    // Validate the URL (must use either HTTP or HTTPS schemes)
    if (string.IsNullOrEmpty(queueItem.InputUrl) ||
        !Uri.TryCreate(queueItem.InputUrl, UriKind.Absolute, out Uri? uriResult) ||
        !(uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
    {
      return false;
    }

    try
    {
      var httpClient = _httpClient;
      Console.WriteLine("Downloading file...step2");
      Console.WriteLine(proxyServer);
      if (proxyServer != null) {
        var url = proxyServer.Protocol + "://" + proxyServer.Host + ":" + proxyServer.Port;
        Console.WriteLine(url);
        var proxy = new WebProxy
        {
            Address = new Uri(url),
            BypassProxyOnLocal = false,
            //Credentials = new NetworkCredential(username, password)
        };

        var httpClientHandler = new HttpClientHandler
        {
            Proxy = proxy,
            UseProxy = true
        };

        httpClient = new HttpClient(httpClientHandler);

      }


      var fileName = Helpers.ExtractFileNameFromUrl(queueItem.InputUrl);

      try
      {
              var res = await httpClient.GetAsync("https://dummy.me");

      }
      catch (System.Exception e)
      {
        
        Console.WriteLine(e.Message);
      }

      var response = await httpClient.GetAsync(queueItem.InputUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

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

      // Ensure the filename is safe by removing invalid characters and making sure it cannot end up being empty
      fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
      if (string.IsNullOrWhiteSpace(fileName)) fileName = "unnamed_" + DateTime.Now.ToString("yyyyMMddHHmmss");

      // Try to determine the file extension based on common mime types if the filename doesn't have one already
      if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
      {
        var contentType = response.Content.Headers.ContentType?.MediaType;

        if (contentType != null && Helpers.CommonMimeTypes.TryGetValue(contentType, out var extension))
        {
          fileName += extension;
        }
      }

      // Construct the file path using the base directory and the sanitized filename
      var localFilePath = Path.GetFullPath(Path.Combine("/app/downloads", fileName));

      // Ensure the file path is within the intended directory
      if (!localFilePath.StartsWith(Path.GetFullPath("/app/downloads"), StringComparison.OrdinalIgnoreCase))
      {
        throw new UnauthorizedAccessException("Invalid file path.");
      }

      // Check for existing temp file and corresponding final file
      string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
      string fileExtension = Path.GetExtension(fileName);
      var tempFilePath = localFilePath + ".tmp";
      long totalBytesRead = 0;
      int digit = 1;

      while (System.IO.File.Exists(tempFilePath) || System.IO.File.Exists(localFilePath))
      {
        if (System.IO.File.Exists(tempFilePath))
        {
          totalBytesRead = new FileInfo(tempFilePath).Length;
          break;
        }

        fileName = $"{fileNameWithoutExtension}_{digit}{fileExtension}";
        localFilePath = Path.Combine("/app/downloads", fileName);
        tempFilePath = localFilePath + ".tmp";
        digit++;
      }

      var requestMessage = new HttpRequestMessage(HttpMethod.Get, queueItem.InputUrl);

      if (totalBytesRead > 0)
      {
        requestMessage.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(totalBytesRead, null);
      }

      response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

      if (!response.IsSuccessStatusCode) return false;

      // If the server supports resumption (HTTP 206), do not restart the download from scratch
      bool restartDownload = response.StatusCode != System.Net.HttpStatusCode.PartialContent;

      if (restartDownload)
      {
        totalBytesRead = 0;
      }

      using (var download = await response.Content.ReadAsStreamAsync())
      {
        using var stream = new FileStream(tempFilePath, restartDownload ? FileMode.Create : FileMode.Append, FileAccess.Write);
        var contentLength = response.Content.Headers.ContentLength;

        var buffer = new byte[81920]; // 80KB buffer (default buffer size used by Microsoft's CopyTo method in Stream)
        int bytesRead;
        double elapsedNanoSeconds;
        double elapsedNanoSecondsAvg;
        double bytesPerSecond;
        double bytesPerSecondAvg;

        // To avoid slowing down the process we should not be saving changes to the context on every iteration
        double saveIntervalInSeconds = 0.5; // saving and updating every 0.5 second
        double debounceInSeconds = 0;

        var stopwatch = Stopwatch.StartNew();
        var stopwatchAvg = Stopwatch.StartNew();

        // Ignore progress reporting when the ContentLength's header is not available
        if (!contentLength.HasValue)
        {
          while ((bytesRead = await download.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
          {
            // Check if cancellation is requested
            if (cancellationToken.IsCancellationRequested)
            {
              throw new OperationCanceledException(cancellationToken);
            }

            await stream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytesRead += bytesRead;

            // Calculate download speed
            elapsedNanoSeconds = stopwatch.Elapsed.TotalNanoseconds;
            bytesPerSecond = elapsedNanoSeconds > 0 ? bytesRead / elapsedNanoSeconds * 1e9 : 0;
            elapsedNanoSecondsAvg = stopwatchAvg.Elapsed.TotalNanoseconds;
            bytesPerSecondAvg = elapsedNanoSecondsAvg > 0 ? totalBytesRead / elapsedNanoSecondsAvg * 1e9 : 0;
            stopwatch.Restart();

            // Inform the client at regular intervals
            debounceInSeconds += elapsedNanoSeconds / 1e9;
            if (debounceInSeconds >= saveIntervalInSeconds)
            {
              await WebSocketHandler.SendMessageAsync($"speed:{queueItem.Id}:{bytesPerSecond:F2}");
              await WebSocketHandler.SendMessageAsync($"speedavg:{queueItem.Id}:{bytesPerSecondAvg:F2}");
              debounceInSeconds = 0;
            }
          }

          queueItem.ProgressPercentage = 100;
          await WebSocketHandler.SendMessageAsync($"progress:{queueItem.Id}:{queueItem.ProgressPercentage}");
          await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
          // Adjusted for correct progress reporting when resuming download
          long totalContentLength = contentLength.Value + totalBytesRead;

          while ((bytesRead = await download.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
          {
            // Check if cancellation is requested
            if (cancellationToken.IsCancellationRequested)
            {
              throw new OperationCanceledException(cancellationToken);
            }

            await stream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytesRead += bytesRead;
            queueItem.ProgressPercentage = (int)(100.0 * totalBytesRead / totalContentLength);

            // Calculate download speed
            elapsedNanoSeconds = stopwatch.Elapsed.TotalNanoseconds;
            bytesPerSecond = elapsedNanoSeconds > 0 ? bytesRead / elapsedNanoSeconds * 1e9 : 0;
            elapsedNanoSecondsAvg = stopwatchAvg.Elapsed.TotalNanoseconds;
            bytesPerSecondAvg = elapsedNanoSecondsAvg > 0 ? totalBytesRead / elapsedNanoSecondsAvg * 1e9 : 0;
            stopwatch.Restart();

            // Save progress to the database and inform the client at regular intervals
            debounceInSeconds += elapsedNanoSeconds / 1e9;
            if (debounceInSeconds >= saveIntervalInSeconds)
            {
              await _context.SaveChangesAsync(cancellationToken);
              await WebSocketHandler.SendMessageAsync($"progress:{queueItem.Id}:{queueItem.ProgressPercentage}");
              await WebSocketHandler.SendMessageAsync($"speed:{queueItem.Id}:{bytesPerSecond:F2}");
              await WebSocketHandler.SendMessageAsync($"speedavg:{queueItem.Id}:{bytesPerSecondAvg:F2}");
              debounceInSeconds = 0;
            }
          }

          // Save any remaining changes
          await WebSocketHandler.SendMessageAsync($"progress:{queueItem.Id}:{queueItem.ProgressPercentage}");
          await _context.SaveChangesAsync(cancellationToken);
        }
      }

      // Rename temp file to final file
      System.IO.File.Move(tempFilePath, localFilePath);

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
    catch (Exception e)
    {
      Console.WriteLine(e.Message);
      return false;
    }

    return true;
  }

  [HttpPost("testconnection")]
  public async Task<IActionResult> TestConnectionWithProxy([FromBody] ProxyServer proxyServer)
  {
    try
    {
      var url = proxyServer.Protocol + "://" + proxyServer.Host + ":" + proxyServer.Port;
      Console.WriteLine(url);
      var proxy = new WebProxy
      {
          Address = new Uri(url),
          BypassProxyOnLocal = false,
          //Credentials = new NetworkCredential(username, password)
      };

      var httpClientHandler = new HttpClientHandler
      {
          Proxy = proxy,
          UseProxy = true
      };

      var httpClient = new HttpClient(httpClientHandler);

      var res = await httpClient.GetAsync("https://dummy.me");

      return Ok(res);
    }
    catch (Exception ex)
    {
      return BadRequest(ex.Message);
    }
  }
}
