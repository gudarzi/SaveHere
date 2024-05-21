using System.Web;

namespace SaveHere.WebAPI;

public static class Helpers
{
  public static List<object> GetDirectoryContent(DirectoryInfo dirInfo)
  {
    List<object> result = new List<object>();

    foreach (var fileSystemInfo in dirInfo.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
    {
      dynamic obj = new System.Dynamic.ExpandoObject();

      obj.Name = fileSystemInfo.Name;
      obj.FullName = fileSystemInfo.FullName;
      obj.Type = fileSystemInfo switch
      {
        FileInfo fi => "file",
        DirectoryInfo di => "directory",
        _ => "default"
      };

      if (obj.Type == "file")
      {
        obj.Length = ((FileInfo)fileSystemInfo).Length;
        obj.Extension = ((FileInfo)fileSystemInfo).Extension;
      }
      else if (obj.Type == "directory")
      {
        obj.Children = GetDirectoryContent((DirectoryInfo)fileSystemInfo);
      }

      result.Add(obj);
    }

    result = result.OrderBy(r => (string)((dynamic)r).Type).ThenBy(r => (string)((dynamic)r).Name).ToList();

    return result;
  }

  public static string ExtractFileNameFromUrl(string url)
  {
    // Decode the URL
    string decodedUrl = HttpUtility.UrlDecode(url);
    Uri uri = new Uri(decodedUrl);

    // Extract the filename from the path
    string fileName = Path.GetFileName(uri.AbsolutePath);

    // Check if the filename is valid and contains an extension
    if (!string.IsNullOrEmpty(fileName) && fileName.Contains('.'))
    {
      return fileName;
    }

    // If no valid filename is found in the path, check the query parameters
    string query = uri.Query;

    // Search for the last slash in the query string, indicating a potential filename
    int lastSlashIndex = query.LastIndexOf('/');

    if (lastSlashIndex != -1)
    {
      fileName = query.Substring(lastSlashIndex + 1);

      // Strip any remaining query parameters from the filename
      int queryIndex = fileName.IndexOf('?');

      if (queryIndex != -1)
      {
        fileName = fileName.Substring(0, queryIndex);
      }

      // If a valid filename with an extension is found, return it
      if (!string.IsNullOrEmpty(fileName) && fileName.Contains('.'))
      {
        return fileName;
      }
    }

    // Parse query parameters for potential filenames
    var queryParams = HttpUtility.ParseQueryString(query);

    foreach (string? key in queryParams.AllKeys)
    {
      if (key != null)
      {
        string? paramValue = queryParams[key];

        if (!string.IsNullOrEmpty(paramValue) && paramValue.Contains('.'))
        {
          return paramValue;
        }
      }
    }

    return fileName;
  }

  public static readonly Dictionary<string, string> CommonMimeTypes = new Dictionary<string, string>
  {
    // A collection of the most common MIME types and their corresponding file extensions
    { "application/epub+zip", ".epub" },
    { "application/java-archive", ".jar" },
    { "application/json", ".json" },
    { "application/msword", ".doc" },
    { "application/octet-stream", ".bin" }, //can also be .exe
    { "application/ogg", ".ogx" },
    { "application/pdf", ".pdf" },
    { "application/rtf", ".rtf" },
    { "application/vnd.amazon.ebook", ".azw" },
    { "application/vnd.apple.installer+xml", ".mpkg" },
    { "application/vnd.mozilla.xul+xml", ".xul" },
    { "application/vnd.ms-excel", ".xls" },
    { "application/vnd.ms-powerpoint", ".ppt" },
    { "application/vnd.oasis.opendocument.presentation", ".odp" },
    { "application/vnd.oasis.opendocument.spreadsheet", ".ods" },
    { "application/vnd.oasis.opendocument.text", ".odt" },
    { "application/vnd.openxmlformats-officedocument.presentationml.presentation", ".pptx" },
    { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx" },
    { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx" },
    { "application/x-7z-compressed", ".7z" },
    { "application/x-abiword", ".abw" },
    { "application/x-bzip", ".bz" },
    { "application/x-bzip2", ".bz2" },
    { "application/x-csh", ".csh" },
    { "application/x-rar-compressed", ".rar" },
    { "application/x-sh", ".sh" },
    { "application/x-shockwave-flash", ".swf" },
    { "application/x-tar", ".tar" },
    { "application/x-zip-compressed", ".zip" },
    { "application/xhtml+xml", ".xhtml" },
    { "application/xml", ".xml" },
    { "application/zip", ".zip" },
    { "application/x-msdownload", ".exe" },
    { "audio/aac", ".aac" },
    { "audio/midi", ".midi" },
    { "audio/ogg", ".oga" },
    { "audio/webm", ".weba" },
    { "audio/x-wav", ".wav" },
    { "audio/3gpp", ".3gp" },
    { "audio/3gpp2", ".3g2" },
    { "audio/mpeg", ".mp3" },
    { "font/otf", ".otf" },
    { "font/ttf", ".ttf" },
    { "font/woff", ".woff" },
    { "font/woff2", ".woff2" },
    { "image/bmp", ".bmp" },
    { "image/gif", ".gif" },
    { "image/jpeg", ".jpeg" },
    { "image/png", ".png" },
    { "image/svg+xml", ".svg" },
    { "image/tiff", ".tiff" },
    { "image/webp", ".webp" },
    { "image/x-icon", ".ico" },
    { "text/calendar", ".ics" },
    { "text/css", ".css" },
    { "text/csv", ".csv" },
    { "text/html", ".html" },
    { "text/plain", ".txt" },
    { "text/xml", ".xml" },
    { "video/3gpp", ".3gp" },
    { "video/3gpp2", ".3g2" },
    { "video/mp2t", ".ts" },
    { "video/mpeg", ".mpeg" },
    { "video/ogg", ".ogv" },
    { "video/webm", ".webm" },
    { "video/x-msvideo", ".avi" },
    { "application/vnd.android.package-archive", ".apk" },
    { "application/x-iso9660-image", ".iso" },
    { "application/x-ms-shortcut", ".lnk" },
    { "application/x-msi", ".msi" },
    { "application/x-python-code", ".py" },
    { "application/x-sharedlib", ".so" },
    { "application/x-sqlite3", ".sqlite" },
    { "application/x-web-app-manifest+json", ".webapp" },
    { "audio/basic", ".au" },
    { "audio/x-aiff", ".aiff" },
    { "audio/x-matroska", ".mka" },
    { "image/avif", ".avif" },
    { "image/heif", ".heif" },
    { "image/heic", ".heic" },
    { "image/vnd.microsoft.icon", ".ico" },
    { "text/javascript", ".js" },
    { "text/markdown", ".md" },
    { "text/x-python", ".py" },
    { "text/x-shellscript", ".sh" },
    { "video/mp4", ".mp4" },
    { "video/x-matroska", ".mkv" },
    { "application/vnd.ms-fontobject", ".eot" },
    { "application/x-xpinstall", ".xpi" },
    { "application/vnd.google-earth.kml+xml", ".kml" },
    { "application/vnd.google-earth.kmz", ".kmz" }
  };
}
