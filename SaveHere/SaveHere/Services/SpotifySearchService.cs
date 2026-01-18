using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;

namespace SaveHere.Services
{
  public class SpotifySearchService
  {
    private readonly HttpClient _http;
    private readonly ILogger<SpotifySearchService> _logger;

    // Inject HttpClient and ILogger via DI.
    public SpotifySearchService(HttpClient http, ILogger<SpotifySearchService> logger)
    {
      _http = http;
      _logger = logger;
    }

    public async Task<SpotifySearchResult> ConvertUrlAsync(string spotifyUrl)
    {
      // Create the result and mark the original URL.
      var result = new SpotifySearchResult
      {
        OriginalUrl = spotifyUrl
      };

      // Determine which service the input URL comes from.
      var inputService = GetServiceName(spotifyUrl);
      // Add the original service (if we detected it).
      result.Links[inputService] = spotifyUrl;

      try
      {
        // Build form data and post to the API.
        var formData = new Dictionary<string, string>
              {
                  { "link", spotifyUrl }
              };
        var content = new FormUrlEncodedContent(formData);
        var response = await _http.PostAsync("https://idonthavespotify.donado.co/search", content);

        if (!response.IsSuccessStatusCode)
        {
          _logger.LogWarning("Spotify API returned status {StatusCode} for URL: {Url}", (int)response.StatusCode, spotifyUrl);
          result.Error = response.StatusCode switch
          {
            HttpStatusCode.NotFound => "The media link could not be found. Please verify the URL is correct.",
            HttpStatusCode.TooManyRequests => "Too many requests. Please wait a moment and try again.",
            HttpStatusCode.ServiceUnavailable => "The conversion service is temporarily unavailable. Please try again later.",
            _ => $"Service error ({(int)response.StatusCode}): {response.ReasonPhrase}"
          };
          return result;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var html = Encoding.UTF8.GetString(bytes);

        // Use HtmlAgilityPack to parse the returned HTML.
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Select all list items that represent a media link.
        var liNodes = doc.DocumentNode.SelectNodes("//li[@data-controller='search-link']");
        if (liNodes != null)
        {
          foreach (var li in liNodes)
          {
            // Each <li> has an attribute "data-search-link-url-value" with the media URL.
            var link = li.GetAttributeValue("data-search-link-url-value", "").Trim();
            if (string.IsNullOrEmpty(link))
            {
              continue;
            }

            // Inside each <li>, the <a> tag's aria-label is something like "Listen on Apple Music".
            var aNode = li.SelectSingleNode(".//a[@aria-label]");
            if (aNode is not null)
            {
              var ariaLabel = aNode.GetAttributeValue("aria-label", "").Trim();
              if (ariaLabel.StartsWith("Listen on ", StringComparison.InvariantCultureIgnoreCase))
              {
                var serviceName = ariaLabel.Substring("Listen on ".Length).Trim();
                // Add to the dictionary if not already there.
                if (!result.Links.ContainsKey(serviceName))
                {
                  result.Links[serviceName] = link;
                }
              }
            }
          }
        }
      }
      catch (HttpRequestException ex)
      {
        _logger.LogError(ex, "Network error calling Spotify API for URL: {Url}", spotifyUrl);
        result.Error = $"Network error: {ex.Message}";
      }
      catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
      {
        _logger.LogWarning("Spotify search was cancelled for URL: {Url}", spotifyUrl);
        result.Error = "The search was cancelled.";
      }
      catch (TaskCanceledException)
      {
        _logger.LogWarning("Spotify search timed out for URL: {Url}", spotifyUrl);
        result.Error = "Request timed out. Please try again.";
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error during Spotify search for URL: {Url}", spotifyUrl);
        result.Error = $"Unexpected error: {ex.Message}";
      }

      return result;
    }

    /// <summary>
    /// Returns a guessed service name from the URL.
    /// </summary>
    private string GetServiceName(string url)
    {
      var lowerUrl = url.ToLowerInvariant();
      if (lowerUrl.Contains("spotify.com"))
        return "Spotify";
      if (lowerUrl.Contains("apple.com"))
        return "Apple Music";
      if (lowerUrl.Contains("deezer.com"))
        return "Deezer";
      if (lowerUrl.Contains("soundcloud.com"))
        return "SoundCloud";
      if (lowerUrl.Contains("tidal.com"))
        return "Tidal";
      if (lowerUrl.Contains("youtube"))
        return "YouTube Music";
      return "Unknown";
    }
  }

  public class SpotifySearchResult
  {
    public string OriginalUrl { get; set; } = string.Empty;
    public Dictionary<string, string> Links { get; set; } = new();
    public string? Error { get; set; }
  }
}
