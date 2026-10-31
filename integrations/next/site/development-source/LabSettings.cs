using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace LaunchDesk.Development.RemoteProjectLab
{
    // Developer-only model. Licence/app parameters live once in the shared LaunchModel file.
    public sealed class LabSettings
    {
        public int SchemaVersion { get; set; }
        public string AllowedVmRoot { get; set; }
        public string VmPath { get; set; }
        public string ExpectedUuid { get; set; }
        public string ExpectedMac { get; set; }
        public string VixLibraryPath { get; set; }
        public string CredentialTarget { get; set; }
        public string VmCredentialFile { get; set; }
        public string GuestCredentialFile { get; set; }
        public int TimeoutSeconds { get; set; }
        public string EvidenceDirectory { get; set; }
        public static LabSettings Read(string path)
        {
            if(new FileInfo(path).Length>65536) throw new ArgumentException("Lab model too large.");
            var value=new JavaScriptSerializer().Deserialize<LabSettings>(File.ReadAllText(path));
            if(value==null) throw new ArgumentException("Lab model missing.");
            value.Validate();return value;
        }
        public void Validate()
        {
            if(SchemaVersion!=1 || TimeoutSeconds<1 || TimeoutSeconds>90) throw new ArgumentException("Invalid schema or timeout (1-90 seconds).");
            foreach(string path in new[]{AllowedVmRoot,VmPath,VixLibraryPath,VmCredentialFile,GuestCredentialFile,EvidenceDirectory})
                if(String.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) throw new ArgumentException("Lab paths must be explicit absolute paths.");
            string root=Path.GetFullPath(AllowedVmRoot).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!Path.GetFullPath(VmPath).StartsWith(root,StringComparison.OrdinalIgnoreCase) || !VmPath.EndsWith(".vmx",StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("VM must be inside the explicitly configured lab root.");
            if(String.IsNullOrWhiteSpace(ExpectedUuid) || String.IsNullOrWhiteSpace(ExpectedMac) || String.IsNullOrWhiteSpace(CredentialTarget))
                throw new ArgumentException("Expected VM identity and credential target are required.");
            if(!Path.GetFileName(VixLibraryPath).Equals("vix.dll",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("VIX library name must be vix.dll.");
        }
        public string Fingerprint()
        {
            using(var sha=SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(VmPath).ToUpperInvariant()+"|"+ExpectedUuid.ToUpperInvariant()+"|"+ExpectedMac.ToUpperInvariant()))).Replace("-","");
        }
        public void VerifyTarget(string confirmation)
        {
            Validate();
            if(!String.Equals(confirmation,Fingerprint(),StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Target confirmation differs; review the model and explicitly confirm its fingerprint.");
            // Do not follow junctions/symlinks to an unreviewed VM.
            for(var part=new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(VmPath)));part!=null;part=part.Parent)
                if((part.Attributes&FileAttributes.ReparsePoint)!=0) throw new InvalidOperationException("VM path cannot traverse a reparse point.");
            if((File.GetAttributes(VmPath)&FileAttributes.ReparsePoint)!=0) throw new InvalidOperationException("VM file cannot be a link.");
            var identity=File.ReadLines(VmPath).Where(line=>line.StartsWith("uuid.bios = ") || line.StartsWith("ethernet0.generatedAddress = ") || line.StartsWith("ethernet0.connectionType = "))
                .Select(line=>line.Split(new[]{'='},2)).ToDictionary(parts=>parts[0].Trim(),parts=>parts[1].Trim().Trim('"'));
            if(!identity.ContainsKey("uuid.bios") || !String.Equals(identity["uuid.bios"],ExpectedUuid,StringComparison.OrdinalIgnoreCase) ||
               !identity.ContainsKey("ethernet0.generatedAddress") || !String.Equals(identity["ethernet0.generatedAddress"],ExpectedMac,StringComparison.OrdinalIgnoreCase) ||
               !identity.ContainsKey("ethernet0.connectionType") || identity["ethernet0.connectionType"]!="hostonly")
                throw new InvalidOperationException("VM identity or Host-only configuration differs; no VIX action performed.");
        }
        public string SaveSnapshot(string action)
        {
            Directory.CreateDirectory(EvidenceDirectory);
            string path=Path.Combine(EvidenceDirectory,Guid.NewGuid().ToString("N")+".json");
            string temporary=path+".tmp";
            byte[] bytes=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(new{AttemptId=Path.GetFileNameWithoutExtension(path),Utc=DateTime.UtcNow.ToString("o"),Action=action,Model=this}));
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            File.Move(temporary,path);return Path.GetFileNameWithoutExtension(path);
        }
    }
}
