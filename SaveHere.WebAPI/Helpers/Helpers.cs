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

}
