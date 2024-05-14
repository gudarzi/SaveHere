namespace SaveHere.WebAPI.DTOs;

public class DownloadFileRequestDTO
{
  public string? Url { get; set; }

  public bool? UseUrlForFilename { get; set; } // making it nullable to allow for omission

  [System.Text.Json.Serialization.JsonIgnore]
  public int ProgressPercentage { get; set; } = 0; // goes from 0 to 100 but will stay at 0 then go to 100 if ContentLength is not available
}
