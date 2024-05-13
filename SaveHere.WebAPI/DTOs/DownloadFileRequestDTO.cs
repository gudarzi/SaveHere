namespace SaveHere.WebAPI.DTOs;

public class DownloadFileRequestDTO
{
  public string? Url { get; set; }
  public bool? UseUrlForFilename { get; set; } // making it nullable to allow for omission
}
