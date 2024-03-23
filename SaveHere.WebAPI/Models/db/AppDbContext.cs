using Microsoft.EntityFrameworkCore;

namespace SaveHere.WebAPI.Models.db
{
  public class AppDbContext : DbContext
  {
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<FileDownloadQueueItem> FileDownloadQueueItems { get; set; }
  }
}
