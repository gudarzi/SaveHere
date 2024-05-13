namespace SaveHere.WebAPI.DTOs
{
  public class FileDownloadQueueItemRequestDTO
  {
    public int Id { get; set; }
    public bool? UseHeadersForFilename { get; set; } = true;
  }

}
