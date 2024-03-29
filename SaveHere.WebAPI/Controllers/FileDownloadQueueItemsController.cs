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

  public FileDownloadQueueItemsController(AppDbContext context)
  {
    _context = context;
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

  private bool FileDownloadQueueItemExists(int id)
  {
    return _context.FileDownloadQueueItems.Any(e => e.Id == id);
  }
}
