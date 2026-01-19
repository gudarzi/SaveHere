using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SaveHere.Models
{
  public class FileDownloadQueueItem
  {
    [Key]
    public int Id { get; set; }

    [Required]
    [Url]
    public string? InputUrl { get; set; }

    public EQueueItemStatus Status { get; set; } = EQueueItemStatus.Paused;

    public int ProgressPercentage { get; set; } = 0;
    public int MaxBytesPerSecond { get; set; } = 50000;
    public bool bShowMoreOptions { get; set; } = false;
    public bool bShouldGetFilenameFromHttpHeaders { get; set; } = true;
    public double CurrentDownloadSpeed { get; set; } = 0;
    public double AverageDownloadSpeed { get; set; } = 0;
    public string? CustomFileName { get; set; }
    public string? DownloadFolder { get; set; }

    public List<double> SpeedHistory { get; set; } = [];
    
    // Download acceleration settings
    public int ParallelConnections { get; set; } = 1;
    public int BufferSizeKB { get; set; } = 80;
    public bool UseHttp2 { get; set; } = true;
    public bool EnableCompression { get; set; } = true;
    public bool SupportsRangeRequests { get; set; } = false;

    // Authentication settings
    public AuthenticationType AuthType { get; set; } = AuthenticationType.None;
    public string? AuthUsername { get; set; }
    public string? AuthPassword { get; set; }
    public string? AuthBearerToken { get; set; }
    public string? AuthCookies { get; set; }
    public string? AuthCustomHeaders { get; set; }

    [NotMapped]
    public bool HasAuthentication => AuthType != AuthenticationType.None;
  }
}
