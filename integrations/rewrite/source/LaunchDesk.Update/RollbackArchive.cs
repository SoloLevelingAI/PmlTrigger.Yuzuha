using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

using File = SeaGendManager.ManagedFile;
using Directory = SeaGendManager.ManagedDirectory;

namespace SeaGendManager {
 internal static class RollbackArchive {
  public const string Name="rollback.zip";
  internal static string StreamHash(Stream stream) {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
  internal static void Create(Receipt r,string tx,IEnumerable<FileChange> changes,Func<FileChange,string> source) {
   var originals=changes.Where(f=>f.Existed).ToList();
   r.BackupFiles=originals.Count;r.BeforeBytes=0;r.BackupArchive=null;r.BackupHash=null;r.BackupBytes=0;
   if(originals.Count==0)return;
   string temp=Path.Combine(tx,Name+".tmp"),file=Path.Combine(tx,Name);Engine.NoLinks(temp);Engine.NoLinks(file);
   using(var output=new FileStream(LongPaths.Io(temp),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None)) {
    using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true)) {
     foreach(var f in originals) {
      Engine.ValidateRelative(f.Path);string old=source(f);Engine.NoLinks(old);
      using(var input=new FileStream(LongPaths.Io(old),FileMode.Open,FileAccess.Read,FileShare.Read)) {
       f.BeforeHash=StreamHash(input);input.Position=0;f.BeforeBytes=input.Length;
       f.BeforeWriteTimeUtc=File.GetLastWriteTimeUtc(old).ToString("o");
       var entry=zip.CreateEntry(f.Path.Replace('\\','/'),CompressionLevel.Optimal);
       using(var destination=entry.Open())input.CopyTo(destination);
       r.BeforeBytes+=input.Length;
      }
     }
    }
    output.Flush(true);
   }
   File.Move(temp,file);r.BackupArchive=Name;r.BackupHash=Engine.Hash(file);r.BackupBytes=new FileInfo(LongPaths.Io(file)).Length;
  }
  internal static ZipArchive Open(Receipt r,string tx) {
   if(r.BackupArchive!=Name)throw new IOException("回滚压缩包记录无效。");
   string file=Path.Combine(tx,Name);Engine.NoLinks(file);
   if(!File.Exists(file) || string.IsNullOrEmpty(r.BackupHash) || Engine.Hash(file)!=r.BackupHash)throw new IOException("回滚压缩包缺失或校验失败；尚未恢复文件。");
   var zip=new ZipArchive(File.OpenRead(file),ZipArchiveMode.Read);
   try {
    var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach(var entry in zip.Entries) {Engine.ValidateRelative(entry.FullName);if(!names.Add(entry.FullName.Replace('/','\\')))throw new IOException("回滚压缩包包含重复路径。");}
    return zip;
   }catch{zip.Dispose();throw;}
  }
  internal static ZipArchiveEntry Entry(ZipArchive zip,FileChange f) {
   var entry=zip.GetEntry(f.Path.Replace('\\','/'));if(entry==null)throw new IOException("回滚压缩包缺少文件："+f.Path);
   using(var input=entry.Open())if(StreamHash(input)!=f.BeforeHash)throw new IOException("回滚文件校验失败："+f.Path);
   return entry;
  }
 }
}
