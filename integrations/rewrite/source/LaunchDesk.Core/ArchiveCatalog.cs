namespace LaunchDesk.Core;

public sealed record ArchiveCandidate(string FullPath,long Bytes,DateTime ModifiedUtc)
{
 public string Name=>Path.GetFileName(FullPath);
 public string Display=>$"{Name}  ·  {Bytes/1048576d:0.##} MB  ·  {ModifiedUtc.ToLocalTime():MM-dd HH:mm}";
}
public static class ArchiveCatalog
{
 public static IReadOnlyList<ArchiveCandidate> Scan(string directory)
 {
  if(string.IsNullOrWhiteSpace(directory)||!Directory.Exists(directory))return [];
  var result=new List<ArchiveCandidate>();
  foreach(var path in Directory.EnumerateFiles(directory,"*",new EnumerationOptions{RecurseSubdirectories=false,IgnoreInaccessible=true,AttributesToSkip=FileAttributes.ReparsePoint})){
   if(!Path.GetExtension(path).Equals(".7z",StringComparison.OrdinalIgnoreCase)&&!Path.GetExtension(path).Equals(".zip",StringComparison.OrdinalIgnoreCase))continue;
   try{var file=new FileInfo(path);result.Add(new(file.FullName,file.Length,file.LastWriteTimeUtc));}catch(IOException){}catch(UnauthorizedAccessException){}
  }
  return result.OrderByDescending(x=>x.ModifiedUtc).ThenBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray();
 }
}
