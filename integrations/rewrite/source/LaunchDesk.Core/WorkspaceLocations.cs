using System.Text.Json;

namespace LaunchDesk.Core;

public static class WorkspaceLocations
{
 public static string DefaultWorkspacePath=>Resolve(AppContext.BaseDirectory,Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
 public static string Resolve(string applicationDirectory,string localApplicationData)
 {
  var settings=Path.Combine(Path.GetFullPath(applicationDirectory),"portable.json");
  if(!File.Exists(settings))return LegacyFusionPath(localApplicationData);
  if(new FileInfo(settings).Length>16384)throw new InvalidDataException("portable.json 不能大于 16 KB。");
  var options=JsonSerializer.Deserialize<PortableOptions>(File.ReadAllText(settings),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
  if(string.IsNullOrWhiteSpace(options?.DataDirectory))throw new InvalidDataException("portable.json 缺少 DataDirectory 数据目录。");
  var directory=Path.GetFullPath(options.DataDirectory,Path.GetFullPath(applicationDirectory));
  return Path.Combine(directory,"workspace.json");
 }
 public static string LegacyFusionPath(string localApplicationData)=>Path.Combine(Path.GetFullPath(localApplicationData),"LaunchDesk","Workspaces","Fusion","workspace.json");
 private sealed class PortableOptions {public string DataDirectory {get;set;}="";}
}
