using SaveHere.WebAPI.Models;

namespace SaveHere.WebAPI.DTOs
{
  public class FileDownloadQueueItemRequestDTO
  {
    public int Id { get; set; }
    public bool? UseHeadersForFilename { get; set; } = true;
    public ProxyServer proxyServer { get; set; } = new ProxyServer();
  }
}
