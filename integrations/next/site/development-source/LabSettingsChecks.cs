using System;
using System.IO;
using LaunchDesk.Development.RemoteProjectLab;
class LabSettingsChecks
{
    static void ExpectFailure(Action action){try{action();}catch(ArgumentException){return;}catch(InvalidOperationException){return;}throw new Exception("Expected rejection");}
    static int Main()
    {
        var root=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings-check-"+Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);var vm=Path.Combine(root,"fixture.vmx");
        File.WriteAllText(vm,"uuid.bios = \"fixture-uuid\"\nethernet0.generatedAddress = \"fixture-mac\"\nethernet0.connectionType = \"hostonly\"\n");
        var m=new LabSettings{SchemaVersion=1,AllowedVmRoot=root,VmPath=vm,ExpectedUuid="fixture-uuid",ExpectedMac="fixture-mac",VixLibraryPath=Path.Combine(root,"vix.dll"),CredentialTarget="fixture",VmCredentialFile=Path.Combine(root,"vm.json"),GuestCredentialFile=Path.Combine(root,"guest.json"),TimeoutSeconds=30,EvidenceDirectory=Path.Combine(root,"evidence")};
        m.VerifyTarget(m.Fingerprint());
        ExpectFailure(()=>m.VerifyTarget("wrong"));
        m.ExpectedUuid="wrong";ExpectFailure(()=>m.VerifyTarget(m.Fingerprint()));m.ExpectedUuid="fixture-uuid";
        m.ExpectedMac="wrong";ExpectFailure(()=>m.VerifyTarget(m.Fingerprint()));m.ExpectedMac="fixture-mac";
        m.VmPath=Path.GetFullPath(Path.Combine(root,"..","outside.vmx"));ExpectFailure(()=>m.Validate());m.VmPath=vm;
        m.TimeoutSeconds=0;ExpectFailure(()=>m.Validate());m.TimeoutSeconds=30;
        File.WriteAllText(vm,"uuid.bios = \"fixture-uuid\"\nethernet0.generatedAddress = \"fixture-mac\"\nethernet0.connectionType = \"nat\"\n");
        ExpectFailure(()=>m.VerifyTarget(m.Fingerprint()));
        string id=m.SaveSnapshot("probe-fixture-only");if(!File.Exists(Path.Combine(m.EvidenceDirectory,id+".json")))throw new Exception("Snapshot missing");
        Console.WriteLine("8 configurable-target/snapshot checks passed; fixture VMX only, no VIX or VM action.");return 0;
    }
}
