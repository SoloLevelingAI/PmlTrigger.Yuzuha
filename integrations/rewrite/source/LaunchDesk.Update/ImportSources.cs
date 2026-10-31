using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using LaunchDesk.Core;

namespace SeaGendManager {
 public sealed class SourcePreview { public string RelativePath {get;set;} public string SourcePath {get;set;} public long Bytes {get;set;} public string State {get;set;} }
 public static partial class Engine {
  static ArchiveInfo InspectZip(string path) {
   var result=new ArchiveInfo();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   using(var zip=ZipFile.OpenRead(path)) foreach(var e in zip.Entries) {
    string rel=e.FullName.Replace('/','\\').TrimEnd('\\');ValidateRelative(rel);
    if(!seen.Add(rel))throw new InvalidDataException("压缩包包含重复路径："+rel);
    if(((e.ExternalAttributes>>16)&0xF000)==0xA000 || (e.ExternalAttributes&0x400)!=0)throw new InvalidDataException("不支持压缩包中的链接。");
    result.Paths.Add(rel);result.Bytes=checked(result.Bytes+e.Length);
   }
   if(result.Paths.Count==0)throw new InvalidDataException("压缩包为空。");
   string first=result.Paths[0].Split('\\')[0];if(result.Paths.All(p=>p==first||p.StartsWith(first+"\\",StringComparison.OrdinalIgnoreCase))&&result.Paths.Any(p=>p.Contains('\\')))result.RootName=first;
   return result;
  }
  static void ValidateSources(Plan p,string target) {
   if(!Enum.IsDefined(p.SourceKind))throw new InvalidDataException("导入来源类型无效。");
   foreach(string source in p.SourceKind==ToolSourceKind.Files?(p.SourceFiles??new List<ToolFile>()).Select(f=>f.SourcePath):new[]{p.Source}) {
    if(!Path.IsPathFullyQualified(source))throw new InvalidDataException("来源需为完整本机路径。");NoLinks(source);
    if(Store.IsInside(target,source)||p.SourceKind==ToolSourceKind.Folder&&Store.IsInside(source,target))throw new IOException("来源与目标目录不能重叠。");
    if(p.SourceKind==ToolSourceKind.Folder?!Directory.Exists(source):!File.Exists(source))throw new IOException("来源不存在："+source);
   }
   if(p.SourceKind==ToolSourceKind.Files&&(p.SourceFiles==null||p.SourceFiles.Count==0))throw new InvalidDataException("请选择至少一个普通文件。");
  }
  public static List<SourcePreview> Preview(Plan p) {
   string target=NormalizeTarget(p.Target);ValidateSources(p,target);var list=new List<SourcePreview>();
   if(p.SourceKind==ToolSourceKind.Archive){var info=Inspect(p.Source);bool flatten=p.Flatten&&!string.IsNullOrEmpty(info.RootName)&&Path.GetFileName(target).Equals(info.RootName,StringComparison.OrdinalIgnoreCase);foreach(string rel in info.Paths){if(flatten&&rel.Equals(info.RootName,StringComparison.OrdinalIgnoreCase))continue;string mapped=flatten?rel.Substring(info.RootName.Length+1):rel;list.Add(new SourcePreview {SourcePath=p.Source,RelativePath=mapped,State="包内条目 · 部署时校验"});}}
   else {
    var files=p.SourceKind==ToolSourceKind.Folder?Walk(p.Source,true).Select(f=>new ToolFile {SourcePath=f,RelativePath=Path.GetRelativePath(p.Source,f)}).ToList():p.SourceFiles;
    var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach(var f in files){ValidateRelative(f.RelativePath);if(!seen.Add(f.RelativePath.Replace('/','\\')))throw new InvalidDataException("文件映射中目标路径重复："+f.RelativePath);string dest=Path.Combine(target,f.RelativePath);NoLinks(dest);list.Add(new SourcePreview {SourcePath=f.SourcePath,RelativePath=f.RelativePath,Bytes=new FileInfo(f.SourcePath).Length,State=File.Exists(dest)?Hash(f.SourcePath)==Hash(dest)?"内容相同 · 跳过":"覆盖已有文件":"新增文件"});}
   }
   if(list.Count==0||list.Count>100000)throw new InvalidDataException("导入内容为空或超过 100000 个条目。");return list;
  }
  static string PreparePayload(Plan p,string tx,Receipt r,Action<string> log) {
   var preview=Preview(p);string stage=Path.Combine(tx,"stage");Directory.CreateDirectory(stage);
   if(p.SourceKind==ToolSourceKind.Archive) {
    string snapshot=Path.Combine(tx,"package.7z");File.Copy(p.Source,snapshot,false);r.ArchiveHash=Hash(snapshot);
    string extension=Path.GetExtension(p.Source);var info=extension.Equals(".zip",StringComparison.OrdinalIgnoreCase)?InspectZip(snapshot):Inspect(snapshot);
    if(new DriveInfo(Path.GetPathRoot(tx)).AvailableFreeSpace<checked(info.Bytes+16*1048576))throw new IOException("暂存磁盘空间不足。");
    if(extension.Equals(".zip",StringComparison.OrdinalIgnoreCase)){
     using(var zip=ZipFile.OpenRead(snapshot))foreach(var e in zip.Entries){string rel=e.FullName.Replace('/','\\').TrimEnd('\\');ValidateRelative(rel);string dest=Path.Combine(stage,rel);if(e.FullName.EndsWith("/")||e.FullName.EndsWith("\\")){Directory.CreateDirectory(dest);continue;}Directory.CreateDirectory(Path.GetDirectoryName(dest));using(var input=e.Open())using(var output=new FileStream(dest,FileMode.CreateNew,FileAccess.Write,FileShare.None))input.CopyTo(output);}
    } else { Run7z("t -y -sccUTF-8 -- "+Quote(snapshot),log);Run7z("x -y -sccUTF-8 "+Quote("-o"+stage)+" -- "+Quote(snapshot),log); }
    r.Flattened=p.Flatten&&!string.IsNullOrEmpty(info.RootName)&&Path.GetFileName(p.Target.TrimEnd('\\')).Equals(info.RootName,StringComparison.OrdinalIgnoreCase);
    return r.Flattened?Path.Combine(stage,info.RootName):stage;
   }
   long bytes=preview.Sum(x=>x.Bytes);if(new DriveInfo(Path.GetPathRoot(tx)).AvailableFreeSpace<checked(bytes+16*1048576))throw new IOException("暂存磁盘空间不足。");
   if(p.SourceKind==ToolSourceKind.Folder)foreach(string directory in Walk(p.Source,false)){string rel=Path.GetRelativePath(p.Source,directory);ValidateRelative(rel);Directory.CreateDirectory(Path.Combine(stage,rel));}
   foreach(var f in preview){NoLinks(f.SourcePath);string dest=Path.Combine(stage,f.RelativePath);Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Copy(f.SourcePath,dest,false);}
   return stage;
  }
 }
}
