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

    // Call the DownloadFile method in the FileController
    var fileController = new FileController(_httpClient);
    var downloadResult = await fileController.DownloadFile(new DownloadFileRequestDTO() { Url = fileDownloadQueueItem.InputUrl });

    if (downloadResult is OkObjectResult okObjectResult)
    {
      // The file download has started successfully
      // You might want to update the FileDownloadQueueItem object to reflect this
      // For example, you could set a property like "IsDownloading" to true
      fileDownloadQueueItem.Status = EQueueItemStatus.Finished;
      _context.SaveChanges();
      return Ok(okObjectResult.Value);
    }
    else
    {
      // The file download could not be started
      // You might want to update the FileDownloadQueueItem object to reflect this
      // For example, you could set a property like "DownloadAttempts" to increment it
      fileDownloadQueueItem.Status = EQueueItemStatus.Paused;
      _context.SaveChanges();
      return BadRequest("The file download could not be started.");
    }
  }


  //private bool FileDownloadQueueItemExists(int id)
  //{
  //  return _context.FileDownloadQueueItems.Any(e => e.Id == id);
  //}
}
