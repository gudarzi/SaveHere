using HtmlAgilityPack;
using System.Text;

namespace SaveHere.Services
{
  public class SpotifySearchService
  {
    private readonly HttpClient _http;

    // Inject HttpClient via DI.
    public SpotifySearchService(HttpClient http)
    {
      _http = http;
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
          result.Error = $"API returned status {(int)response.StatusCode}: {response.ReasonPhrase}";
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
        result.Error = $"Network error: {ex.Message}";
      }
      catch (Exception ex)
      {
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
