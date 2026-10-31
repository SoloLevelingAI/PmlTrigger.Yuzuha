using LaunchDesk.Next.Application.Features.LoginLaunch;
using LaunchDesk.Next.Desktop.Features.Launch;
using NUnit.Framework;
namespace LaunchDesk.Next.UiTests;

public sealed class InputBehaviorTests
{
    [Test]
    public void ModelAndBindingAdapterShareValuesAndObserveBothDirections()
    {
        var model=new LaunchModel();var vm=new LaunchViewModel(model);var changed=new List<string?>();
        vm.PropertyChanged+=(_,e)=>changed.Add(e.PropertyName);
        vm.Project="from-ui";Assert.That(model.Project,Is.EqualTo("from-ui"));
        model.Project="from-backend";Assert.That(vm.Project,Is.EqualTo("from-backend"));Assert.That(changed,Does.Contain("Project"));
        vm.LicencePort="unfinished";Assert.That(model.LicencePort,Is.EqualTo("unfinished"));Assert.That(vm.Validation,Is.Not.Null);
        Assert.That(vm.Model,Is.SameAs(model));
    }
    [Test]
    public void CandidateRefreshPreservesAllManualValuesAndSubmittedSnapshot()
    {
        var first = new LaunchInput { DisplayName = "first", ExecutablePath = "first.exe", WorkingDirectory = "first", Protocol = EntryProtocol.Monitor, PasswordPolicy = PasswordPolicy.Prompt, CheckLicence = true };
        var vm = new LaunchViewModel(); vm.Load(first.CreateSnapshot(Guid.Empty)); vm.Project = "manual-project"; vm.Module = "manual-module"; vm.Protocol = EntryProtocol.Direct; vm.PasswordPolicy = PasswordPolicy.None; vm.CheckLicence = false;
        var submitted = vm.Capture().CreateSnapshot(Guid.NewGuid());
        vm.Load(new LaunchInput { DisplayName = "second", ExecutablePath = "second.exe", WorkingDirectory = "second" }.CreateSnapshot(Guid.Empty), true);
        Assert.Multiple(() => { Assert.That(vm.Project, Is.EqualTo("manual-project")); Assert.That(vm.Module, Is.EqualTo("manual-module")); Assert.That(vm.Protocol, Is.EqualTo(EntryProtocol.Direct)); Assert.That(vm.PasswordPolicy, Is.EqualTo(PasswordPolicy.None)); Assert.That(vm.CheckLicence, Is.False); Assert.That(vm.ExecutablePath, Is.EqualTo("second.exe")); Assert.That(submitted.ExecutablePath, Is.EqualTo("first.exe")); Assert.That(submitted.Sources["Project"], Is.EqualTo("Manual")); });
        vm.Project = "changed-after-submit"; Assert.That(submitted.Project, Is.EqualTo("manual-project"));
    }
    [Test]
    public void CorrectingFieldUpdatesValidation()
    { var vm = new LaunchViewModel(); Assert.That(vm.Validation, Is.Not.Empty); vm.DisplayName = "valid"; vm.ExecutablePath = "fixture.exe"; vm.WorkingDirectory = "fixture"; Assert.That(vm.Validation, Is.Empty); vm.LicencePort = "invalid"; Assert.That(vm.Validation, Does.Contain("端口")); vm.LicencePort = "5053"; Assert.That(vm.Validation, Is.Empty); }
}
