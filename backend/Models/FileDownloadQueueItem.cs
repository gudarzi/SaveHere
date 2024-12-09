﻿using System.ComponentModel.DataAnnotations;

namespace SaveHere.WebAPI.Models
{
  public class FileDownloadQueueItem
  {
    [Key]
    public int Id { get; set; }

    public string? InputUrl { get; set; }

    public EQueueItemStatus Status { get; set; } = EQueueItemStatus.Paused;

    public int ProgressPercentage { get; set; } = 0;
  }

  public enum EQueueItemStatus
  {
    Paused,
    Downloading,
    Finished,
    Cancelled
  }
}
