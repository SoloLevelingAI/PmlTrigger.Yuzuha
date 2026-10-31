using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

using File = SeaGendManager.ManagedFile;
using Directory = SeaGendManager.ManagedDirectory;

namespace SeaGendManager {
 public static partial class Engine {
  internal static void RemoveManagedTree(string root,string path) {
   if(!Store.IsInside(root,path) || Path.GetFullPath(root).TrimEnd('\\').Equals(Path.GetFullPath(path).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))throw new IOException("临时目录超出管理范围。");
   if(!Directory.Exists(path))return;NoLinks(path);
   foreach(string d in Walk(path,false).Concat(new [] {path}))File.SetAttributes(d,File.GetAttributes(d)&~FileAttributes.ReadOnly);
   // Keep the identifying receipt until the other managed files have been removed.
   foreach(string f in Walk(path,true).OrderBy(f=>Path.GetFileName(f).Equals("receipt.json",StringComparison.OrdinalIgnoreCase)?1:0)) {
    File.SetAttributes(f,File.GetAttributes(f)&~FileAttributes.ReadOnly);File.Delete(f);
   }
   Directory.Delete(path,true);
  }
  static void Cleanup(string tx,bool keepArchive,Action<string> log) {
   try {
    RemoveManagedTree(tx,Path.Combine(tx,"stage"));
    foreach(string name in new [] {"package.7z",RollbackArchive.Name+".tmp"})if(File.Exists(Path.Combine(tx,name)))File.Delete(Path.Combine(tx,name));
    if(!keepArchive && File.Exists(Path.Combine(tx,RollbackArchive.Name)))File.Delete(Path.Combine(tx,RollbackArchive.Name));
   }catch(Exception e) {try{log("临时数据清理未完成："+e.Message);}catch{}}
  }
  public static Receipt Apply(Plan plan,Action<string> progress) {
   Store.Configure(plan.DataRoot);string target=NormalizeTarget(plan.Target);Store.ValidateStorage(target);
   string archive=Path.GetFullPath(plan.Source),tx=Store.Transaction(target,plan.Id);NoLinks(tx);
   ValidateSources(plan,target);
   using(Lock(target)) {
    MigrateLegacyCore(target,progress);
    if(Store.History(target).Any(h=>h.Status=="Applying" || h.Status=="RecoveryRequired"))throw new IOException("此目录存在未完成的更新。请先恢复后重试。");
    if(Directory.Exists(tx))throw new IOException("更新记录编号已存在。");
    Directory.CreateDirectory(target);Directory.CreateDirectory(tx);
    string receiptPath=Path.Combine(tx,"receipt.json");
    var r=new Receipt {Schema=2,Id=plan.Id,Archive=archive,Target=target,KeepBackup=plan.KeepBackup,Started=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),Status="Preparing",Message="准备更新",ToolId=plan.ToolId,ToolName=plan.ToolName,SourceKind=plan.SourceKind};
    Action<string> log=s=>{File.AppendAllText(Path.Combine(tx,"execution.log"),DateTime.Now.ToString("HH:mm:ss")+"  "+s+Environment.NewLine,Encoding.UTF8);if(progress!=null)progress(s);};
    Action save=()=>Store.Save(receiptPath,r);save();bool committed=false;
    try {
     log("准备导入内容");var stateDrive=new DriveInfo(Path.GetPathRoot(tx));
     string payload=PreparePayload(plan,tx,r,log),stage=Path.Combine(tx,"stage");
     var stageFiles=Walk(stage,true);var stageDirectories=Walk(stage,false);
     var files=stageFiles.Where(f=>f.StartsWith(payload+"\\",StringComparison.OrdinalIgnoreCase)).OrderBy(f=>f,StringComparer.OrdinalIgnoreCase).ToList();
     if(files.Count==0)throw new IOException("压缩包未包含可更新文件。");r.PackageFiles=files.Count;
     log("比较文件内容，相同文件将跳过");
     foreach(string file in files) {
      string rel=file.Substring(payload.Length+1);ValidateRelative(rel);string dest=Path.Combine(target,rel);NoLinks(dest);
      if(Directory.Exists(dest))throw new IOException("文件与目标目录冲突："+rel);
      string wanted=Hash(file);bool existed=File.Exists(dest);
      if(existed && new FileInfo(LongPaths.Io(dest)).Length==new FileInfo(LongPaths.Io(file)).Length && Hash(dest)==wanted){r.SkippedFiles++;continue;}
      r.Files.Add(new FileChange {Path=rel,Existed=existed,Hash=wanted});
     }
     foreach(string directory in stageDirectories.Where(d=>d.StartsWith(payload+"\\",StringComparison.OrdinalIgnoreCase)).OrderBy(d=>d.Length)) {
      string rel=directory.Substring(payload.Length+1);ValidateRelative(rel);string d=Path.Combine(target,rel);NoLinks(d);
      if(File.Exists(d))throw new IOException("目录与目标文件冲突："+rel);if(!Directory.Exists(d))r.CreatedDirectories.Add(rel);
     }
     if(r.Files.Count==0 && r.CreatedDirectories.Count==0) {
      r.Status="NoChanges";r.Message="文件内容相同，已跳过 "+r.SkippedFiles+" 个文件；未写入或保留备份。";r.Finished=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");save();committed=true;Cleanup(tx,false,log);return r;
     }
     long backupEstimate=r.Files.Where(f=>f.Existed).Sum(f=>new FileInfo(LongPaths.Io(Path.Combine(target,f.Path))).Length);
     if(stateDrive.AvailableFreeSpace<backupEstimate+16*1048576)throw new IOException("临时压缩恢复数据的磁盘空间不足。");
     log("压缩保存将变化的原文件");RollbackArchive.Create(r,tx,r.Files,f=>Path.Combine(target,f.Path));
     r.Status="Applying";save();
     foreach(string rel in r.CreatedDirectories){string d=Path.Combine(target,rel);NoLinks(d);Directory.CreateDirectory(d);}
     int count=0;
     using(var journal=new FileStream(LongPaths.Io(Path.Combine(tx,"touch.journal")),FileMode.CreateNew,FileAccess.Write,FileShare.Read)) {
      foreach(var f in r.Files) {
       string dest=Path.Combine(target,f.Path);NoLinks(dest);
       if(f.Existed?(!File.Exists(dest) || Hash(dest)!=f.BeforeHash):File.Exists(dest))throw new IOException("目标文件在比较后发生变化，更新已停止："+f.Path);
       byte[] intent=Encoding.ASCII.GetBytes(count+"\n");journal.Write(intent,0,intent.Length);journal.Flush(true);f.Touched=true;
       File.Copy(Path.Combine(payload,f.Path),dest,true);if(Hash(dest)!=f.Hash)throw new IOException("写入后校验失败："+f.Path);
       count++;if(count==1 || count%100==0 || count==r.Files.Count)log("写入文件 "+count+" / "+r.Files.Count);
      }
     }
     r.Status="Success";r.CanRollback=plan.KeepBackup;
     r.Message="已更新 "+r.Files.Count+" 个文件，跳过 "+r.SkippedFiles+" 个相同文件。"+(plan.KeepBackup?"已保留压缩回滚数据。":"未保留成功后的回滚备份。");
     r.Finished=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");save();committed=true;
     try{log(r.Message);}catch{}Cleanup(tx,plan.KeepBackup,log);return r;
    }catch(Exception e) {
     if(committed)return r;
     r.Status="Failed";r.Message=e.Message;
     try {Restore(r,tx,false,log);r.Status="FailedRolledBack";r.CanRollback=false;r.Message=e.Message+"\n本次写入已自动恢复。";}
     catch(Exception restore){r.Status="RecoveryRequired";r.Message=e.Message+"\n自动恢复未完成："+restore.Message;}
     r.Finished=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");save();Cleanup(tx,r.Status=="RecoveryRequired",log);return r;
    }
   }
  }
  static void Restore(Receipt r,string tx,bool verify,Action<string> log,Action beforeWrite=null) {
   var changed=r.Files.Where(f=>f.Touched).ToList();ZipArchive zip=null;
   try {
    if(changed.Any(f=>f.Existed))zip=RollbackArchive.Open(r,tx);
    foreach(var f in changed) {
     ValidateRelative(f.Path);string dest=Path.Combine(r.Target,f.Path);NoLinks(dest);
     if(verify && (!File.Exists(dest) || Hash(dest)!=f.Hash))throw new IOException("更新后文件已被修改，未执行回滚："+f.Path);
     if(f.Existed)RollbackArchive.Entry(zip,f);
    }
    foreach(string rel in r.CreatedDirectories){ValidateRelative(rel);NoLinks(Path.Combine(r.Target,rel));}
    if(beforeWrite!=null)beforeWrite();
    foreach(var f in changed.AsEnumerable().Reverse()) {
     string dest=Path.Combine(r.Target,f.Path);
     if(f.Existed) {
      if(!File.Exists(dest) || Hash(dest)!=f.BeforeHash) {
       var entry=zip.GetEntry(f.Path.Replace('\\','/'));using(var input=entry.Open())using(var output=new FileStream(LongPaths.Io(dest),FileMode.Create,FileAccess.Write,FileShare.None))input.CopyTo(output);
       if(Hash(dest)!=f.BeforeHash)throw new IOException("恢复后校验失败："+f.Path);
       DateTime time;if(DateTime.TryParse(f.BeforeWriteTimeUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out time))File.SetLastWriteTimeUtc(dest,time.ToUniversalTime());
      }
     }else if(File.Exists(dest))File.Delete(dest);
    }
    foreach(string rel in r.CreatedDirectories.OrderByDescending(d=>d.Length)){string d=Path.Combine(r.Target,rel);if(Directory.Exists(d) && !Entries(d).Any())Directory.Delete(d);}
    log("原文件已恢复；本次新增文件已移除。");
   }finally{if(zip!=null)zip.Dispose();}
  }
  public static Receipt Rollback(string target,string id) {
   target=NormalizeTarget(target);Store.ValidateStorage(target);
   using(Lock(target)) {
    MigrateLegacyCore(target,null);
    string tx=Store.Transaction(target,id);NoLinks(tx);var r=Store.LoadReceipt(Path.Combine(tx,"receipt.json"));
    if(!string.Equals(r.Target,target,StringComparison.OrdinalIgnoreCase) || r.Id!=id)throw new IOException("更新记录与目标不一致。");
    var latest=Store.History(target).FirstOrDefault(x=>x.Status=="Success" || x.Status=="Applying" || x.Status=="RecoveryRequired");
    if(latest==null || latest.Id!=id || (r.Status!="Success" && r.Status!="RecoveryRequired" && r.Status!="Applying"))throw new IOException("只能回滚最新成功更新或恢复最新中断更新。");
    if(r.Status=="Success" && !r.CanRollback)throw new IOException("此更新未勾选保留备份，成功后不提供手动回滚。");
    Action<string> log=s=>File.AppendAllText(Path.Combine(tx,"execution.log"),DateTime.Now.ToString("HH:mm:ss")+"  "+s+Environment.NewLine,Encoding.UTF8);
    try {
     Restore(r,tx,r.Status=="Success",log,()=>{r.Status="RecoveryRequired";r.Message="正在恢复。";Store.Save(Path.Combine(tx,"receipt.json"),r);});
     r.Status="RolledBack";r.CanRollback=false;r.Message="已恢复更新前的文件。";Store.Save(Path.Combine(tx,"receipt.json"),r);Cleanup(tx,false,log);return r;
    }catch(Exception e){log("回滚停止："+e.Message);throw;}
   }
  }
 }
}
