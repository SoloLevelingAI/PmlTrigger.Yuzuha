using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using File = SeaGendManager.ManagedFile;
using Directory = SeaGendManager.ManagedDirectory;

namespace SeaGendManager {
 public static partial class Store {
  public static string DataRoot {get;private set;} = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SeaGendManager");
  public static void Configure(string root) {
   if(string.IsNullOrWhiteSpace(root))return;
   if(!Path.IsPathRooted(root) || root.StartsWith(@"\\") || Path.GetFullPath(root).TrimEnd('\\')==Path.GetPathRoot(root).TrimEnd('\\'))throw new IOException("更新记录目录必须是本机的专用目录。");
   DataRoot=Path.GetFullPath(root).TrimEnd('\\');Engine.NoLinks(DataRoot);
  }
  public static string RecordsRoot(string target) {
   string normalized=Path.GetFullPath(target).TrimEnd('\\').ToUpperInvariant();
   string key;using(var sha=SHA256.Create())key=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(normalized))).Replace("-","");
   return Path.Combine(DataRoot,"Updates",key);
  }
  public static string ExistingTransaction(string target,string id) {
   string current=Transaction(target,id);
   return File.Exists(Path.Combine(current,"receipt.json"))?current:Path.Combine(target,Internal,id);
  }
  public static bool IsInside(string root,string path) {
   root=Path.GetFullPath(root).TrimEnd('\\');path=Path.GetFullPath(path).TrimEnd('\\');
   return path.Equals(root,StringComparison.OrdinalIgnoreCase) || path.StartsWith(root+"\\",StringComparison.OrdinalIgnoreCase);
  }
  public static void ValidateStorage(string target) {
   if(IsInside(target,DataRoot) || IsInside(DataRoot,target))throw new IOException("更新记录和备份必须位于安装目录之外。");
   foreach(var scope in new [] {EnvironmentVariableTarget.Process,EnvironmentVariableTarget.User,EnvironmentVariableTarget.Machine})
    foreach(string variable in new [] {"PMLLIB","PMLUI","PDMSUI"}) {
     string value=Environment.GetEnvironmentVariable(variable,scope);if(string.IsNullOrEmpty(value))continue;
     foreach(string entry in value.Split(';')) {
      string p=Environment.ExpandEnvironmentVariables(entry.Trim().Trim('"'));
      if(Path.IsPathRooted(p) && IsInside(p,DataRoot))throw new IOException("更新记录目录不能位于 "+variable+" 的搜索路径内。");
     }
    }
   Engine.NoLinks(DataRoot);
  }
 }
}
