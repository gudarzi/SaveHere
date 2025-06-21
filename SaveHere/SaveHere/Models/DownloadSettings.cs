namespace SaveHere.Models
{
    public class DownloadSettings
    {
        public int ParallelConnections { get; set; } = 1;
        public int BufferSizeKB { get; set; } = 80;
        public bool UseHttp2 { get; set; } = true;
        public bool EnableCompression { get; set; } = true;
        public int MaxRetryAttempts { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 2;
        public bool EnableAdaptiveBuffering { get; set; } = false;
        public int MinBufferSizeKB { get; set; } = 80;
        public int MaxBufferSizeKB { get; set; } = 1024;
        
        public int GetBufferSizeBytes() => BufferSizeKB * 1024;
        public int GetMinBufferSizeBytes() => MinBufferSizeKB * 1024;
        public int GetMaxBufferSizeBytes() => MaxBufferSizeKB * 1024;
    }
}