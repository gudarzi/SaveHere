using SaveHere.Helpers;
using SaveHere.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace SaveHere.Services
{
  public interface IYtdlpService
  {
    Task EnsureYtdlpAvailable();
    Task<string> GetLatestVersion();
    Task<bool> IsUpdateAvailable();
    Task DownloadLatestVersion();
    Task<string> GetExecutablePath();
    Task<string> GetSupportedSitesFilePath();
    Task UpdateSupportedSitesFile();
    Task DownloadVideo(int itemId, string url, string? customFileName, string quality, string proxy, string? downloadFolder, CancellationToken cancellationToken);
    Task<VideoInfo?> GetVideoInfo(string url, string proxy);
  }

  public class YtdlpService : IYtdlpService
  {
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<YtdlpService> _logger;
    private const string GITHUB_API_URL = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";
    private const string SUPPORTED_SITES_URL = "https://raw.githubusercontent.com/yt-dlp/yt-dlp/refs/heads/master/supportedsites.md";
    private const string SUPPORTED_SITES_FILENAME = "supportedsites.md";
    private readonly string _basePath;
    private readonly string _executableName;
    private readonly IProgressHubService _progressHubService;

    public YtdlpService(IHttpClientFactory httpClientFactory, ILogger<YtdlpService> logger, IProgressHubService progressHubService)
    {
      _httpClientFactory = httpClientFactory;
      _logger = logger;
      _progressHubService = progressHubService;

      // Set up base path and executable name based on OS
      _basePath = Path.Combine(Directory.GetCurrentDirectory(), "tools", "ytdlp");
      _executableName = GetExecutableNameForCurrentOS();

      // Ensure tools directory exists
      Directory.CreateDirectory(_basePath);
    }

    private string GetExecutableNameForCurrentOS()
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        return "yt-dlp.exe";
      else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        return "yt-dlp_linux";
      else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        return "yt-dlp_macos";
      else
        throw new PlatformNotSupportedException("Current OS is not supported");
    }

    public async Task EnsureYtdlpAvailable()
    {
      try
      {
        string executablePath = await GetExecutablePath();
        string supPath = await GetSupportedSitesFilePath();

        if (!File.Exists(executablePath) || await IsUpdateAvailable())
        {
          await DownloadLatestVersion();
          await UpdateSupportedSitesFile();
        }

        if (!File.Exists(supPath))
        {
          await UpdateSupportedSitesFile();
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to ensure yt-dlp availability");
        throw;
      }
    }

    public async Task<string> GetLatestVersion()
    {
      try
      {
        using var client = CreateGitHubClient();
        var release = await client.GetFromJsonAsync<GitHubRelease>(GITHUB_API_URL);
        return release?.TagName ?? throw new Exception("Could not get latest version");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to get latest version");
        throw;
      }
    }

    public async Task<bool> IsUpdateAvailable()
    {
      try
      {
        var versionFile = Path.Combine(_basePath, "version.txt");

        if (!File.Exists(versionFile))
          return true;

        var currentVersion = await File.ReadAllTextAsync(versionFile);
        var latestVersion = await GetLatestVersion();

        return currentVersion != latestVersion;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to check for updates");
        throw;
      }
    }

    public async Task DownloadLatestVersion()
    {
      try
      {
        using var client = CreateGitHubClient();
        var release = await client.GetFromJsonAsync<GitHubRelease>(GITHUB_API_URL);
        if (release == null) throw new Exception("Could not get release information");

        var asset = release.Assets.FirstOrDefault(a => a.Name == _executableName)
            ?? throw new Exception($"Could not find asset for {_executableName}");

        var executablePath = Path.Combine(_basePath, _executableName);
        var tempPath = executablePath + ".tmp";

        // Download the file
        using (var response = await client.GetAsync(asset.BrowserDownloadUrl))
        using (var fileStream = File.Create(tempPath))
        {
          await response.Content.CopyToAsync(fileStream);
        }

        // Replace old file with new one
        if (File.Exists(executablePath))
          File.Delete(executablePath);

        File.Move(tempPath, executablePath);

        // Set executable permissions on Unix systems
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
          File.SetUnixFileMode(executablePath,
              UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
              UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
              UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        // Save version information
        await File.WriteAllTextAsync(Path.Combine(_basePath, "version.txt"), release.TagName);

        _logger.LogInformation("Successfully downloaded yt-dlp version {Version}", release.TagName);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to download latest version");
        throw;
      }
    }

    public Task<string> GetExecutablePath()
    {
      return Task.FromResult(Path.Combine(_basePath, _executableName));
    }

    public Task<string> GetSupportedSitesFilePath()
    {
      return Task.FromResult(Path.Combine(_basePath, SUPPORTED_SITES_FILENAME));
    }

    public async Task UpdateSupportedSitesFile()
    {
      try
      {
        using var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync(SUPPORTED_SITES_URL);
        response.EnsureSuccessStatusCode();

        var filePath = await GetSupportedSitesFilePath();
        await File.WriteAllTextAsync(filePath, await response.Content.ReadAsStringAsync());

        _logger.LogInformation("Successfully downloaded supported sites documentation");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to download supported sites documentation");
        throw;
      }
    }

    private HttpClient CreateGitHubClient()
    {
      var client = _httpClientFactory.CreateClient();
      //client.DefaultRequestHeaders.UserAgent.ParseAdd("SaveHere-App");
      client.DefaultRequestHeaders.UserAgent.ParseAdd("curl");
      return client;
    }

        public async Task DownloadVideo(int itemId, string url, string? customfilename, string quality, string proxy, string? downloadFolder, CancellationToken cancellationToken)
        {
            var executablePath = await GetExecutablePath();

            // Format option
            var formatOption = quality switch
            {
                "Best" or null or "" => "-f best",
                _ => quality.Split(" --", 2) switch
                {
                    [var format, var extras] => $"-f \"{format}\" --{extras}",
                    [var format] => $"-f \"{format}\"",
                    _ => "-f best\""
                }
            };

            var proxyOption = !string.IsNullOrEmpty(proxy)
                ? $"--proxy \"{proxy}\""
                : "";

            const string mergeOption = "--merge-output-format mp4";

            // Determine download path
            string fullDownloadPath = DirectoryBrowser.DownloadsPath;
            if (!string.IsNullOrWhiteSpace(downloadFolder))
            {
                string combinedPath = Path.Combine(DirectoryBrowser.DownloadsPath, downloadFolder);
                string normalizedFullPath = Path.GetFullPath(combinedPath);
                string normalizedBasePath = Path.GetFullPath(DirectoryBrowser.DownloadsPath);

                if (normalizedFullPath.StartsWith(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
                {
                    fullDownloadPath = normalizedFullPath;
                }
            }

            // Create directory if it doesn't exist
            Directory.CreateDirectory(fullDownloadPath);

            // Determine output filename logic (force yt-dlp to append correct extension)
            string outputTemplate = !string.IsNullOrWhiteSpace(customfilename)
                ? Path.Combine(fullDownloadPath, Path.GetFileNameWithoutExtension(customfilename) + ".%(ext)s")
                : Path.Combine(fullDownloadPath, "%(title)s.%(ext)s");

            string outputOption = $"-o \"{outputTemplate}\"";

            // Combine all args
            string finalArgs = $"{formatOption} {proxyOption} {mergeOption} {outputOption} \"{url}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = finalArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = fullDownloadPath
            };

            await _progressHubService.BroadcastLogUpdate(itemId, $"Running yt-dlp with arguments: {startInfo.Arguments}");
            _logger.LogInformation("yt-dlp command: {args}", startInfo.Arguments);

            using var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += async (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    await _progressHubService.BroadcastLogUpdate(itemId, e.Data);
                }
            };

            process.ErrorDataReceived += async (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    string errorLog = $"Error: {e.Data}";
                    await _progressHubService.BroadcastLogUpdate(itemId, errorLog);
                    _logger.LogWarning("yt-dlp error: {Error}", e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    string exitCodeError = $"Process exited with code {process.ExitCode}";
                    await _progressHubService.BroadcastLogUpdate(itemId, exitCodeError);
                    throw new Exception($"yt-dlp exited with code {process.ExitCode}");
                }

                await _progressHubService.BroadcastLogUpdate(itemId, "Download completed successfully!");
                await _progressHubService.BroadcastStateChange(itemId, EQueueItemStatus.Finished.ToString());
            }
            catch (OperationCanceledException)
            {
                await _progressHubService.BroadcastStateChange(itemId, EQueueItemStatus.Cancelled.ToString());
                await _progressHubService.BroadcastLogUpdate(itemId, "Download was cancelled.");
                if (!process.HasExited)
                {
                    process.Kill();
                }
                throw;
            }
            catch (Exception ex)
            {
                string exceptionError = $"Download failed: {ex.Message}";
                await _progressHubService.BroadcastStateChange(itemId, EQueueItemStatus.Paused.ToString());
                await _progressHubService.BroadcastLogUpdate(itemId, exceptionError);
                throw;
            }
        }


        public async Task<VideoInfo?> GetVideoInfo(string url, string proxy)
    {
      try
      {
        string executablePath = await GetExecutablePath();

        var proxyOption = !string.IsNullOrEmpty(proxy)
          ? $"--proxy \"{proxy}\""
          : "";

        var startInfo = new ProcessStartInfo
        {
          FileName = executablePath,
          Arguments = $"--dump-json --no-warnings --quiet --no-playlist {proxyOption} \"{url}\"",
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          UseShellExecute = false,
          CreateNoWindow = true
        };

        using Process process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
          _logger.LogError("yt-dlp process exited with code {ExitCode}", process.ExitCode);
          return new VideoInfo { Errors = $"Error: Non-zero exit code. Exit code: {process.ExitCode}" };
        }

        // Deserialize the JSON
        var videoData = JsonSerializer.Deserialize<VideoInfo>(output, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });


        if (videoData == null)
        {
          return new VideoInfo { Errors = "Error: No output received." };
        }

        if (output.Contains("\"http_headers\":") && output.Contains("\"Authorization\""))
        {
          videoData.RequiresLogin = true;
        }

        return videoData;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Exception occurred while getting video info for URL: {Url}", url);
        return new VideoInfo { Errors = $"Exception: {ex.Message}" };
      }
    }

  }
}