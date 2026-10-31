using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SeaGendManager {
 // Keep stored/displayed paths ordinary; pass extended paths only to filesystem APIs.
 internal static class LongPaths {
  internal static string Io(string path) {
   if(path.StartsWith(@"\\?\",StringComparison.Ordinal))return path;
   string full=Path.GetFullPath(path);
   return full.StartsWith(@"\\",StringComparison.Ordinal)?@"\\?\UNC\"+full.Substring(2):@"\\?\"+full;
  }
  internal static string Normal(string path) {
   if(path.StartsWith(@"\\?\UNC\",StringComparison.OrdinalIgnoreCase))return @"\\"+path.Substring(8);
   return path.StartsWith(@"\\?\",StringComparison.Ordinal)?path.Substring(4):path;
  }
 }
 internal static class ManagedFile {
  public static bool Exists(string p){return File.Exists(LongPaths.Io(p));}
  public static FileStream OpenRead(string p){return File.OpenRead(LongPaths.Io(p));}
  public static void Copy(string a,string b,bool overwrite){File.Copy(LongPaths.Io(a),LongPaths.Io(b),overwrite);}
  public static void Move(string a,string b){File.Move(LongPaths.Io(a),LongPaths.Io(b));}
  public static void Delete(string p){File.Delete(LongPaths.Io(p));}
  public static FileAttributes GetAttributes(string p){return File.GetAttributes(LongPaths.Io(p));}
  public static void SetAttributes(string p,FileAttributes a){File.SetAttributes(LongPaths.Io(p),a);}
  public static DateTime GetLastWriteTimeUtc(string p){return File.GetLastWriteTimeUtc(LongPaths.Io(p));}
  public static void SetLastWriteTimeUtc(string p,DateTime t){File.SetLastWriteTimeUtc(LongPaths.Io(p),t);}
  public static string ReadAllText(string p,Encoding e){return File.ReadAllText(LongPaths.Io(p),e);}
  public static string[] ReadAllLines(string p){return File.ReadAllLines(LongPaths.Io(p));}
  public static void AppendAllText(string p,string t,Encoding e){File.AppendAllText(LongPaths.Io(p),t,e);}
 }
 internal static class ManagedDirectory {
  public static bool Exists(string p){return Directory.Exists(LongPaths.Io(p));}
  public static DirectoryInfo CreateDirectory(string p){return Directory.CreateDirectory(LongPaths.Io(p));}
  public static void Delete(string p){Directory.Delete(LongPaths.Io(p));}
  public static void Delete(string p,bool recursive){Directory.Delete(LongPaths.Io(p),recursive);}
  public static IEnumerable<string> EnumerateFileSystemEntries(string p){return Directory.EnumerateFileSystemEntries(LongPaths.Io(p)).Select(LongPaths.Normal);}
  public static IEnumerable<string> EnumerateFiles(string p,string pattern){return Directory.EnumerateFiles(LongPaths.Io(p),pattern).Select(LongPaths.Normal);}
 }
}
