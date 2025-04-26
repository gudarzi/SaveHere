using System.ComponentModel.DataAnnotations;

namespace SaveHere.Models
{
  namespace SaveHere.Models
  {
    public class YoutubeDownloadQueueItem
    {
      [Key]
      public int Id { get; set; }

      [Required]
      public string Url { get; set; } = "";
      public string? CustomFileName { get; set; }
      public string Quality { get; set; } = "";
      public string Proxy { get; set; } = "";

      public EQueueItemStatus Status { get; set; } = EQueueItemStatus.Paused;

      public List<string> OutputLog { get; set; } = new List<string>(); // For keeping the logs in memory

      public string PersistedLog { get; set; } = string.Empty; // For saving logs in the database

      public string? DownloadFolder { get; set; }
    }
  }
}
