using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using YuzuhaToolkit.Workspace;
using Xunit;

namespace YuzuhaToolkit.Tests;

/// <summary>用户说明与独立开发参考的文件、身份及边界验证。</summary>
public sealed class DocumentationPackageTests
{
    /// <summary>旧安装残留允许保留，但不会进入默认用户站的文件白名单。</summary>
    [Fact]
    public void UserGuideOnlyRegistersDeclaredFilesAndReferenceIdentity()
    {
        using var fixture = new DocumentationPackageFixture();
        var user = DocumentationPackage.ValidateUserGuide(fixture.UserRoot);
        Assert.Equal(DocumentationPackageFixture.Commit, user.SourceCommit);
        Assert.Equal(4, user.Files.Count);
        Assert.Contains("developer-reference.html", user.Files.Keys);
        Assert.DoesNotContain("api/Old.html", user.Files.Keys);
        Assert.DoesNotContain("documentation-profile.json", user.Files.Keys);
        Assert.Equal(fixture.ManifestHash, user.DeveloperReference.ManifestSha256);
    }

    /// <summary>正确独立包完整验证 site 与 source-docs，挂载结果只包含 site 相对成员。</summary>
    [Fact]
    public void MatchingReferenceValidatesCompleteInventoryAndExposesOnlySite()
    {
        using var fixture = new DocumentationPackageFixture();
        var reference = DocumentationPackage.ValidateReference(fixture.UserRoot, fixture.ReferenceRoot);
        Assert.Equal(fixture.ReferenceRoot, reference.PackageRoot);
        Assert.Equal(Path.Combine(fixture.ReferenceRoot, "site"), reference.SiteRoot);
        Assert.Equal(fixture.ManifestHash, reference.ManifestSha256);
        Assert.Contains("api/Type.html", reference.Files.Keys);
        Assert.DoesNotContain("source-docs/README.md", reference.Files.Keys);
        using var stream = DocumentationPackage.OpenRead(reference.SiteRoot, reference.Files["module.d.mts"]);
        Assert.Equal(reference.Files["module.d.mts"].Bytes, stream.Length);
        Assert.Equal(0, stream.Position);
    }

    /// <summary>开发资料树和源码映射不能登记成默认用户帮助，即使其文件摘要有效。</summary>
    [Theory]
    [InlineData("api/Type.html")]
    [InlineData("reference/trace.html")]
    [InlineData("source-docs/README.md")]
    [InlineData("assets/main.js.map")]
    public void UserGuideRejectsDeveloperTreesAndSourceMaps(string relative)
    {
        using var fixture = new DocumentationPackageFixture();
        var path = Path.Combine(fixture.UserRoot, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "not user help");
        var node = JsonNode.Parse(File.ReadAllText(fixture.UserManifest))!;
        node["files"]!.AsArray().Add(new JsonObject { ["path"] = relative, ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(), ["bytes"] = new FileInfo(path).Length });
        File.WriteAllText(fixture.UserManifest, node.ToJsonString());
        Rejects(() => DocumentationPackage.ValidateUserGuide(fixture.UserRoot));
    }

    /// <summary>错误摘要、旧源码修订、版本和缺失目录均不能匹配当前用户说明。</summary>
    [Theory]
    [InlineData("hash")]
    [InlineData("revision")]
    [InlineData("version")]
    [InlineData("missing")]
    [InlineData("filename")]
    public void ReferenceRejectsWrongIdentityOrMissingPackage(string mode)
    {
        using var fixture = new DocumentationPackageFixture();
        fixture.BreakIdentity(mode);
        Rejects(() => DocumentationPackage.ValidateReference(fixture.UserRoot, fixture.ReferenceRoot));
    }

    /// <summary>文件内容漂移、未登记文件及清单成员丢失均停止完整包验证。</summary>
    [Theory]
    [InlineData("tamper")]
    [InlineData("extra")]
    [InlineData("missing")]
    [InlineData("source-docs")]
    public void ReferenceRejectsChangedOrIncompleteFileInventory(string mode)
    {
        using var fixture = new DocumentationPackageFixture();
        if (mode == "tamper") File.AppendAllText(Path.Combine(fixture.ReferenceRoot, "source-docs", "README.md"), "changed");
        if (mode == "extra") File.WriteAllText(Path.Combine(fixture.ReferenceRoot, "site", "extra.html"), "extra");
        if (mode == "missing") File.Delete(Path.Combine(fixture.ReferenceRoot, "site", "api", "Type.html"));
        if (mode == "source-docs") { Directory.Delete(Path.Combine(fixture.ReferenceRoot, "source-docs"), true); fixture.WriteReferenceManifest(); fixture.BindUserGuide(); }
        Rejects(() => DocumentationPackage.ValidateReference(fixture.UserRoot, fixture.ReferenceRoot));
    }

    /// <summary>重复 JSON 字段、重复成员、路径逃逸和 Windows 别名均在读取成员前拒绝。</summary>
    [Theory]
    [InlineData("duplicate-field")]
    [InlineData("duplicate-path")]
    [InlineData("parent-path")]
    [InlineData("absolute-path")]
    [InlineData("device-name")]
    public void ReferenceRejectsAmbiguousManifestAndUnsafePaths(string mode)
    {
        using var fixture = new DocumentationPackageFixture();
        var node = JsonNode.Parse(File.ReadAllText(fixture.ReferenceManifest))!;
        var files = node["files"]!.AsArray();
        if (mode == "duplicate-path") files.Add(files[0]!.DeepClone());
        if (mode == "parent-path") files[0]!["path"] = "site/../../outside.html";
        if (mode == "absolute-path") files[0]!["path"] = "C:/outside.html";
        if (mode == "device-name") files[0]!["path"] = "site/CON.html";
        var text = node.ToJsonString();
        if (mode == "duplicate-field") text = "{\"schema\":\"phayton.developer-reference/1\"," + text[1..];
        File.WriteAllText(fixture.ReferenceManifest, text);
        fixture.BindUserGuide();
        Rejects(() => DocumentationPackage.ValidateReference(fixture.UserRoot, fixture.ReferenceRoot));
    }

    /// <summary>单文件和累计大小、清单项数及默认站 HTML 数量使用明确预算。</summary>
    [Theory]
    [InlineData("single-file")]
    [InlineData("total-size")]
    [InlineData("member-count")]
    [InlineData("user-html-count")]
    [InlineData("user-total-size")]
    [InlineData("manifest-size")]
    public void PackageBudgetsRejectOversizedClaimsBeforeOpeningMembers(string mode)
    {
        using var fixture = new DocumentationPackageFixture();
        var userMode = mode is "user-html-count" or "user-total-size" or "manifest-size";
        var path = userMode ? fixture.UserManifest : fixture.ReferenceManifest;
        var node = JsonNode.Parse(File.ReadAllText(path))!; var files = node["files"]!.AsArray();
        if (mode == "single-file") files[0]!["bytes"] = 128L * 1024 * 1024 + 1;
        if (mode == "total-size") foreach (var item in files) item!["bytes"] = 128L * 1024 * 1024;
        if (mode == "member-count") while (files.Count <= 12000) files.Add(files[0]!.DeepClone());
        if (mode == "user-html-count") for (var i = 0; i < 40; i++) { var item = files[0]!.DeepClone(); item["path"] = "extra" + i + ".html"; files.Add(item); }
        if (mode == "user-total-size") files[0]!["bytes"] = 15L * 1024 * 1024 + 1;
        File.WriteAllText(path, node.ToJsonString());
        if (mode == "manifest-size") File.WriteAllBytes(path, new byte[8 * 1024 * 1024 + 1]);
        if (!userMode) fixture.BindUserGuide();
        Rejects(() => { if (userMode) DocumentationPackage.ValidateUserGuide(fixture.UserRoot); else DocumentationPackage.ValidateReference(fixture.UserRoot, fixture.ReferenceRoot); });
    }

    /// <summary>正常工作区祖先上的 OneDrive 云项可读取；不把全部重解析点误判为目录链接。</summary>
    [Fact]
    public void UserGuideAcceptsNormalWorkspaceAndCloudAncestors()
    {
        using var fixture = new DocumentationPackageFixture(AppContext.BaseDirectory);
        Assert.Equal(fixture.UserRoot, DocumentationPackage.ValidateUserGuide(fixture.UserRoot).Root);
    }

    /// <summary>验证后新增的文件篡改和目录联接不能被读取或重新采信。</summary>
    [Fact]
    public void ValidatedFileRechecksContentAndNewDirectoryLinks()
    {
        using var fixture = new DocumentationPackageFixture();
        var reference = DocumentationPackage.ValidateReference(fixture.UserRoot, fixture.ReferenceRoot);
        var file = reference.Files["api/Type.html"];
        File.AppendAllText(Path.Combine(reference.SiteRoot, "api", "Type.html"), "changed");
        Rejects(() => DocumentationPackage.OpenRead(reference.SiteRoot, file));
        fixture.ReplaceApiWithLink();
        Rejects(() => DocumentationPackage.OpenRead(reference.SiteRoot, file));
        Rejects(() => DocumentationPackage.ValidateReference(fixture.UserRoot, fixture.ReferenceRoot));
    }
    private static void Rejects(Action action) => Assert.True(Record.Exception(action) is IOException or InvalidDataException);
}

internal sealed class DocumentationPackageFixture : IDisposable
{
    private string? junction;
    internal const string Commit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    internal string Root { get; }
    internal string UserRoot => Path.Combine(Root, "user");
    internal string ReferenceRoot => Path.Combine(Root, "reference");
    internal string ReferenceManifest => Path.Combine(ReferenceRoot, "developer-reference.json");
    internal string UserManifest => Path.Combine(UserRoot, "documentation-profile.json");
    internal string ManifestHash => Hash(File.ReadAllBytes(ReferenceManifest));

    internal DocumentationPackageFixture(string? parent = null)
    {
        Root = Path.Combine(parent ?? Path.GetTempPath(), "documentation-package-" + Guid.NewGuid().ToString("N"));
        foreach (var relative in new[] { "user/api", "user/reference/ui-trace", "reference/site/api", "reference/source-docs" }) Directory.CreateDirectory(Path.Combine(Root, relative));
        File.WriteAllText(Path.Combine(UserRoot, "index.html"), "<!doctype html><h1>User guide</h1>");
        File.WriteAllText(Path.Combine(UserRoot, "developer-reference.html"), "<!doctype html><h1>Choose independent reference</h1>");
        File.WriteAllText(Path.Combine(UserRoot, "theme.css"), "body { color: black; }");
        File.WriteAllText(Path.Combine(UserRoot, "xrefmap.yml"), "### YamlMime:XRefMap\nreferences: []\n");
        File.WriteAllText(Path.Combine(UserRoot, "api", "Old.html"), "old API must not be served");
        File.WriteAllText(Path.Combine(UserRoot, "reference", "ui-trace", "old.html"), "old UI trace must not be served");
        File.WriteAllText(Path.Combine(ReferenceRoot, "site", "index.html"), "<!doctype html><h1>Developer reference</h1>");
        File.WriteAllText(Path.Combine(ReferenceRoot, "site", "api", "Type.html"), "<!doctype html><h1>Type API</h1>");
        byte[] source = [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes("// 原始源码\r\nexport declare const value: string;\r\n")];
        foreach (var name in new[] { "source.cs", "client.ts", "module.mts", "module.d.mts", "guide.md", "xrefmap.yml", "metadata.yaml", "source.yuzuha-unknown" }) File.WriteAllBytes(Path.Combine(ReferenceRoot, "site", name), source);
        File.WriteAllBytes(Path.Combine(ReferenceRoot, "site", "design.figcraft"), [0x50, 0x4b, 0x03, 0x04, 0, 1, 2, 3]);
        File.WriteAllText(Path.Combine(ReferenceRoot, "source-docs", "README.md"), "# Original development source\n");
        WriteReferenceManifest(); BindUserGuide();
    }
    internal void WriteReferenceManifest(string commit = Commit, string version = "0.3.4.2")
    {
        var files = Directory.EnumerateFiles(ReferenceRoot, "*", SearchOption.AllDirectories).Where(path => path != ReferenceManifest)
            .Select(path => Entry(ReferenceRoot, path)).OrderBy(entry => entry.path, StringComparer.Ordinal).ToArray();
        File.WriteAllText(ReferenceManifest, JsonSerializer.Serialize(new { schema = "phayton.developer-reference/1", sourceCommit = commit, version, siteRoot = "site", files }));
    }
    internal void BindUserGuide(string? manifestHash = null)
    {
        var files = new[] { "index.html", "developer-reference.html", "theme.css", "xrefmap.yml" }.Select(path => Entry(UserRoot, Path.Combine(UserRoot, path))).ToArray();
        File.WriteAllText(UserManifest, JsonSerializer.Serialize(new { schema = "phayton.user-guide/1", sourceCommit = Commit, version = "0.3.4.2",
            developerReference = new { fileName = "Phayton-0.3.4.2-Developer-Reference.zip", sha256 = new string('b', 64), manifestSha256 = manifestHash ?? ManifestHash, sourceCommit = Commit }, files }));
    }
    internal void BreakIdentity(string mode)
    {
        if (mode == "hash") BindUserGuide(new string('0', 64));
        else if (mode == "filename")
        {
            var profile = JsonNode.Parse(File.ReadAllText(UserManifest))!;
            profile["developerReference"]!["fileName"] = "Phayton-0.3.4.2-Wrong.zip";
            File.WriteAllText(UserManifest, profile.ToJsonString());
        }
        else if (mode == "missing") Directory.Move(ReferenceRoot, Path.Combine(Root, "missing-reference"));
        else { WriteReferenceManifest(mode == "revision" ? new string('c', 40) : Commit, mode == "version" ? "old" : "0.3.4.2"); BindUserGuide(); }
    }
    internal void ReplaceApiWithLink()
    {
        var api = Path.Combine(ReferenceRoot, "site", "api");
        var outside = Path.Combine(Root, "outside"); Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "Type.html"), "outside directory must not be served");
        Directory.Delete(api, true);
        if (OperatingSystem.IsWindows())
        {
            var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("/c"); start.ArgumentList.Add("mklink"); start.ArgumentList.Add("/J"); start.ArgumentList.Add(api); start.ArgumentList.Add(outside);
            using var process = Process.Start(start)!; process.WaitForExit();
            if (process.ExitCode != 0) throw new IOException("Could not create isolated junction fixture: " + process.StandardError.ReadToEnd());
        }
        else Directory.CreateSymbolicLink(api, outside);
        junction = api;
    }
    private static FixtureEntry Entry(string root, string file) =>
        new(Path.GetRelativePath(root, file).Replace('\\', '/'), Hash(File.ReadAllBytes(file)), new FileInfo(file).Length);
    private sealed record FixtureEntry(string path, string sha256, long bytes);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public void Dispose()
    {
        if (junction is not null && Directory.Exists(junction)) Directory.Delete(junction);
        var expected = Path.GetDirectoryName(Root)! + Path.DirectorySeparatorChar + "documentation-package-";
        if (!Root.StartsWith(expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("Fixture cleanup escaped its declared parent.");
        if (Directory.Exists(Root)) Directory.Delete(Root, true);
    }
}
