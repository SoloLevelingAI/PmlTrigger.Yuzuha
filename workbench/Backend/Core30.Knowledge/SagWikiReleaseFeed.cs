using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.Knowledge;

internal sealed record SagWikiRelease(Version Version, string Digest)
{
    public string Asset => $"SAG-Wiki-{Version}-Windows-x64-setup.exe";
    public string RelativeUrl => $"releases/download/v{Version}/{Asset}";
}

internal sealed record SagWikiInstaller(SagWikiRelease Release, string Source, byte[] Bytes);

internal interface ISagWikiReleaseFeed
{
    Task<SagWikiRelease> Select(CoreData request);
    Task<SagWikiInstaller> Download(SagWikiRelease release, string? mirror);
}

/// <summary>Verifies the same published Windows asset when switching download hosts.</summary>
internal sealed class SagWikiReleaseFeed(HttpClient http) : ISagWikiReleaseFeed
{
    internal const int MaxInstallerBytes = 64 * 1024 * 1024;
    private const int MaxMetadataBytes = 2 * 1024 * 1024;
    private static readonly TimeSpan PublicMetadataTimeout = TimeSpan.FromSeconds(15);
    private const string Github = "https://github.com/Zleap-AI/SAG-Wiki";
    private const string GithubLatest = "https://api.github.com/repos/Zleap-AI/SAG-Wiki/releases/latest";
    private const string GiteeSearch = "https://gitee.com/api/v5/search/repositories?q=SAG-Wiki&per_page=20";
    private static readonly SagWikiRelease Pinned = new(new Version(0, 4, 6),
        "1a7877b0110dd5632572a105e2d117392f156febbc3a3a6ad8e97210705b141f");
    private static readonly Regex Digest = new("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant);
    private static readonly Regex Repository = new(@"^/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public async Task<SagWikiRelease> Select(CoreData request)
    {
        var pinned = request.Children().SingleOrDefault();
        return pinned is null ? await ReadLatest() ?? Pinned
            : new SagWikiRelease(Version.Parse((string)pinned.Field("version")!), (string)pinned.Field("sha256")!);
    }

    public async Task<SagWikiInstaller> Download(SagWikiRelease release, string? mirror)
    {
        var mirrorUrl = mirror is null ? null : ValidateMirror(mirror);
        var primary = await Fetch($"{Github}/{release.RelativeUrl}", TimeSpan.FromSeconds(12), default,
            MaxInstallerBytes, bodyTimeout: TimeSpan.FromMinutes(3));
        if (primary is not null)
            return Verify(release, "GitHub", primary);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        return await DownloadFromGitee(release, mirrorUrl, budget.Token);
    }

    private async Task<SagWikiInstaller> DownloadFromGitee(SagWikiRelease release, string? mirror, CancellationToken budget)
    {
        var repositories = mirror is null ? await FindGiteeRepositories(budget) : [mirror];
        foreach (var repository in repositories)
        {
            if (budget.IsCancellationRequested)
                break;
            var payload = await Fetch($"{repository}/{release.RelativeUrl}", TimeSpan.FromSeconds(22), budget, MaxInstallerBytes);
            if (payload is not null)
                return Verify(release, "Gitee", payload);
        }
        throw new IOException("GitHub 发布资产无法下载。已尝试 Gitee，未取得同版本镜像资产。可在部署 XML 填入 Gitee 镜像仓库。");
    }

    private static SagWikiInstaller Verify(SagWikiRelease release, string source, byte[] payload)
    {
        if (!Sha256(payload).Equals(release.Digest, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{source} 的 SAG-Wiki 发布资产未通过 SHA-256 校验。");
        return new SagWikiInstaller(release, source, payload);
    }

    private async Task<SagWikiRelease?> ReadLatest()
    {
        using var doc = await ReadCatalog(GithubLatest);
        var apiRelease = doc is null ? null : FindWindowsRelease(doc.RootElement);
        if (apiRelease is not null)
            return apiRelease;

        var version = await ReadPublicReleaseVersion();
        if (version is null)
            return null;
        var release = new SagWikiRelease(version, "");
        var tag = $"v{version}";
        var assets = await Fetch($"{Github}/releases/expanded_assets/{tag}", PublicMetadataTimeout, default, MaxMetadataBytes);
        if (assets is not null && FindPublicAssetDigest(Encoding.UTF8.GetString(assets), release.Asset) is { } digest)
            return release with { Digest = digest };

        var checksums = await Fetch($"{Github}/releases/download/{tag}/{tag}-SHA256SUMS.txt", PublicMetadataTimeout,
            default, MaxMetadataBytes);
        if (checksums is null)
            return null;
        foreach (var line in Encoding.UTF8.GetString(checksums).TrimStart('\uFEFF').Split('\n'))
        {
            var match = Regex.Match(line, $@"^([0-9a-fA-F]{{64}})[ \t]+\*?{Regex.Escape(release.Asset)}[ \t\r]*$");
            if (match.Success)
                return release with { Digest = match.Groups[1].Value };
        }
        return null;
    }

    private async Task<Version?> ReadPublicReleaseVersion()
    {
        using var deadline = new CancellationTokenSource(PublicMetadataTimeout);
        try
        {
            using var response = await http.GetAsync($"{Github}/releases/latest", HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            var uri = response.RequestMessage?.RequestUri;
            var tagPrefix = $"{Github}/releases/tag/v";
            var uriPath = uri?.GetLeftPart(UriPartial.Path);
            return response.IsSuccessStatusCode && uriPath is not null && uriPath.StartsWith(tagPrefix, StringComparison.Ordinal)
                && Version.TryParse(uriPath[tagPrefix.Length..], out var version) && version.Build >= 0 && version.Revision < 0
                ? version : null;
        }
        catch (HttpRequestException) { return null; }
        catch (OperationCanceledException) { return null; }
    }

    private static string? FindPublicAssetDigest(string html, string asset)
    {
        var label = $@"\baria-label\s*=\s*""Copy to clipboard digest for {Regex.Escape(asset)}""";
        foreach (Match element in Regex.Matches(html, @"<clipboard-copy\b[^>]*>"))
        {
            if (!Regex.IsMatch(element.Value, label))
                continue;
            var digest = Regex.Match(element.Value, @"\bvalue\s*=\s*""sha256:([0-9a-fA-F]{64})""");
            if (digest.Success)
                return digest.Groups[1].Value;
        }
        return null;
    }

    private static SagWikiRelease? FindWindowsRelease(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !Version.TryParse(Value(root, "tag_name").TrimStart('v'), out var version)
            || version.Build < 0 || version.Revision >= 0 || !root.TryGetProperty("assets", out var assets)
            || assets.ValueKind != JsonValueKind.Array)
            return null;
        var expected = new SagWikiRelease(version, "");
        var asset = assets.EnumerateArray().FirstOrDefault(item => Value(item, "name") == expected.Asset);
        var digest = Value(asset, "digest");
        return digest.StartsWith("sha256:", StringComparison.Ordinal) && Digest.IsMatch(digest[7..])
            ? expected with { Digest = digest[7..] } : null;
    }

    private async Task<string[]> FindGiteeRepositories(CancellationToken budget)
    {
        using var doc = await ReadCatalog(GiteeSearch, budget);
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Array)
            return [];
        return doc.RootElement.EnumerateArray().Select(item => Value(item, "full_name"))
            .Where(name => name.EndsWith("/SAG-Wiki", StringComparison.OrdinalIgnoreCase) && Repository.IsMatch("/" + name))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(3).Select(name => "https://gitee.com/" + name.TrimEnd('/')).ToArray();
    }

    internal static string ValidateMirror(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.Host != "gitee.com"
            || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || !Repository.IsMatch(uri.AbsolutePath))
            throw new InvalidDataException("Gitee 镜像须是 https://gitee.com/<组织>/<仓库> 地址。");
        return "https://gitee.com" + uri.AbsolutePath.TrimEnd('/');
    }

    private async Task<JsonDocument?> ReadCatalog(string url, CancellationToken budget = default)
    {
        var bytes = await Fetch(url, TimeSpan.FromSeconds(10), budget, MaxMetadataBytes);
        try { return bytes is null ? null : JsonDocument.Parse(bytes); }
        catch (JsonException) { return null; }
    }

    private async Task<byte[]?> Fetch(string url, TimeSpan timeout, CancellationToken budget, int maxBytes,
        TimeSpan? bodyTimeout = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(budget);
        deadline.CancelAfter(timeout);
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode)
                return null;
            if (bodyTimeout.HasValue)
                deadline.CancelAfter(bodyTimeout.Value);
            await response.Content.LoadIntoBufferAsync(maxBytes, deadline.Token);
            return await response.Content.ReadAsByteArrayAsync(deadline.Token);
        }
        catch (HttpRequestException) { return null; }
        catch (OperationCanceledException) { return null; }
    }

    private static string Value(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()! : "";
}
