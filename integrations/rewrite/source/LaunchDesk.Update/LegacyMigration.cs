using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using File = SeaGendManager.ManagedFile;
using Directory = SeaGendManager.ManagedDirectory;

namespace SeaGendManager {
 public sealed class MigrationResult {public int Records {get;set;} public long BeforeBytes {get;set;} public long AfterBytes {get;set;} public string DataRoot {get;set;}}
 public static partial class Engine {
  public static MigrationResult MigrateLegacy(string target) {
   target=NormalizeTarget(target);Store.ValidateStorage(target);using(Lock(target))return MigrateLegacyCore(target,null);
  }
  static MigrationResult MigrateLegacyCore(string target,Action<string> progress) {
   var result=new MigrationResult {DataRoot=Store.RecordsRoot(target)};
   string legacy=Path.Combine(target,Store.Internal);if(!Directory.Exists(legacy))return result;NoLinks(legacy);
   foreach(string old in Entries(legacy).Where(Directory.Exists).ToList()) {
    Guid id;if(!Guid.TryParseExact(Path.GetFileName(old),"N",out id))throw new IOException("旧记录目录无法识别，已保留："+old);
    NoLinks(old);string receiptPath=Path.Combine(old,"receipt.json");string tx=Store.Transaction(target,Path.GetFileName(old));NoLinks(tx);
    // A crash during final cleanup may leave only stage directories, without the old receipt.
    if(!File.Exists(receiptPath)) {
     var completed=Store.LoadReceipt(Path.Combine(tx,"receipt.json"));
     if(completed.Schema!=2 || completed.Id!=Path.GetFileName(old) || !string.Equals(completed.Target,target,StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(completed.MigratedFromHash))throw new IOException("旧记录缺失且没有已验证的迁移记录，原目录已保留。");
     if(completed.BackupFiles>0){using(var check=RollbackArchive.Open(completed,tx))foreach(var f in completed.Files.Where(f=>f.Existed))RollbackArchive.Entry(check,f);}
     result.BeforeBytes+=Walk(old,true).Sum(f=>new FileInfo(LongPaths.Io(f)).Length);
     RemoveManagedTree(legacy,old);result.Records++;result.AfterBytes+=Walk(tx,true).Sum(f=>new FileInfo(LongPaths.Io(f)).Length);continue;
    }
    var r=Store.LoadReceipt(receiptPath);
    if(r.Id!=Path.GetFileName(old) || !string.Equals(NormalizeTarget(r.Target),target,StringComparison.OrdinalIgnoreCase))throw new IOException("旧更新记录与目标不一致。");
    string sourceHash=Hash(receiptPath);if(File.Exists(Path.Combine(old,"touch.journal")))sourceHash+=":"+Hash(Path.Combine(old,"touch.journal"));
    result.BeforeBytes+=Walk(old,true).Sum(f=>new FileInfo(LongPaths.Io(f)).Length);
    bool ready=false;
    if(File.Exists(Path.Combine(tx,"receipt.json"))) {
     var existing=Store.LoadReceipt(Path.Combine(tx,"receipt.json"));
     if(existing.Schema!=2 || existing.MigratedFromHash!=sourceHash)throw new IOException("迁移目标已有不同记录，原数据已保留。");
     if(existing.BackupFiles>0){using(var check=RollbackArchive.Open(existing,tx))foreach(var f in existing.Files.Where(f=>f.Existed))RollbackArchive.Entry(check,f);}
     ready=true;
    }
    if(!ready) {
     // An interrupted migration has not touched the installed files or legacy data.
     if(Directory.Exists(tx))RemoveManagedTree(Store.RecordsRoot(target),tx);
     Directory.CreateDirectory(tx);var selected=new List<FileChange>();int originalCount=r.Files.Count;
     foreach(var f in r.Files) {
      ValidateRelative(f.Path);if(!f.Touched)continue;
      if(f.Existed) {
       string original=Path.Combine(old,"backup",f.Path);NoLinks(original);
       if(!File.Exists(original))throw new IOException("旧回滚备份缺失，尚未移除旧记录："+f.Path);
       if(r.Status=="Success" && Hash(original)==f.Hash)continue;
      }
      selected.Add(f);
     }
     r.Schema=2;r.Files=selected;r.SkippedFiles+=originalCount-selected.Count;r.PackageFiles=Math.Max(r.PackageFiles,originalCount);
     r.KeepBackup=true;r.CanRollback=r.Status=="Success";r.MigratedFromHash=sourceHash;
     RollbackArchive.Create(r,tx,selected,f=>Path.Combine(old,"backup",f.Path));
     if(r.BackupFiles>0){using(var check=RollbackArchive.Open(r,tx))foreach(var f in selected.Where(f=>f.Existed))RollbackArchive.Entry(check,f);}
     string oldLog=Path.Combine(old,"execution.log");if(File.Exists(oldLog))File.Copy(oldLog,Path.Combine(tx,"execution.log"),false);
     Store.Save(Path.Combine(tx,"receipt.json"),r);
    }
    // Receipt and verified compressed originals are durable before removing scan-visible copies.
    RemoveManagedTree(legacy,old);result.Records++;result.AfterBytes+=Walk(tx,true).Sum(f=>new FileInfo(LongPaths.Io(f)).Length);
    if(progress!=null)progress("旧记录已迁移并压缩："+r.Id);
   }
   if(!Entries(legacy).Any())Directory.Delete(legacy);
   return result;
  }
 }
}
