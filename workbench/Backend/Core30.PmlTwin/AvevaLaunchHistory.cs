using System.Formats.Nrbf;
using System.Runtime.Serialization;
using Core30.Core;

namespace Core30.PmlTwin;

internal sealed record LaunchLoginSource(string Project, string User, string Mdb, string Source, string Kind, long ChangedUtc);

/// <summary>Reads only the project, username and MDB strings from AVEVA's last-login records.</summary>
internal static class AvevaLaunchHistory
{
    public static LaunchLoginSource[] Read(string localData, string product, string release, LaunchCatalogSources sources)
    {
        if (localData.Length == 0)
            return [];
        var folders = product == "E3D" ? new[] { "AVEVA Everything3D", "AVEVA E3D Design" } : ["Hull & Outfitting"];
        var version = string.Join('.', release.Split('.').Take(2));
        var versions = product == "E3D" && version == "2.10" ? new[] { version, "2.1" } : [version];
        return folders.SelectMany(folder => versions.Select(name => Path.Combine(localData, "AVEVA", folder, name, "LoginHistory", "LoginHistory")))
            .Select(path => ReadEntry(path, sources.Observe(path))).OfType<LaunchLoginSource>()
            .OrderByDescending(entry => entry.ChangedUtc).ToArray();
    }

    private static LaunchLoginSource? ReadEntry(string path, LaunchSourceStamp stamp)
    {
        if (!stamp.Exists)
            return null;
        try
        {
            using var stream = new MemoryStream(FileContent.ReadBounded(path, 128 * 1024));
            if (!NrbfDecoder.StartsWithPayloadHeader(stream))
                return null;
            return ReadFields(NrbfDecoder.DecodeClassRecord(stream), path, stamp.Modified);
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error) || error is SerializationException or NotSupportedException)
        {
            return null;
        }
    }

    private static LaunchLoginSource? ReadFields(ClassRecord record, string path, long modified)
    {
        if (record.TypeName.FullName != "System.Collections.Hashtable" || !record.HasMember("Keys") || !record.HasMember("Values")
            || record.GetArrayRecord("Keys") is not { } keyRecords || record.GetArrayRecord("Values") is not { } valueRecords)
            return null;
        var keys = (object?[])keyRecords.GetArray(typeof(object[]), allowNulls: true);
        var values = (object?[])valueRecords.GetArray(typeof(object[]), allowNulls: true);
        var project = ReadString(keys, values, "Project").ToUpperInvariant();
        var user = ReadString(keys, values, "Username");
        var mdb = ReadString(keys, values, "MDB");
        return project.Length == 3 && project.All(char.IsAsciiLetterOrDigit) && (user.Length != 0 || mdb.Length != 0)
            ? new(project, user, mdb, path, "login-history", modified) : null;
    }

    private static string ReadString(object?[] keys, object?[] values, string name)
    {
        var index = Array.FindIndex(keys, key => Text(key) == name);
        var value = index >= 0 && index < values.Length ? Text(values[index]).Trim() : "";
        return value.Length <= 128 && value.All(System.Xml.XmlConvert.IsXmlChar) ? value : "";
    }

    private static string Text(object? value) => value switch
    {
        string text => text,
        PrimitiveTypeRecord<string> text => text.Value,
        _ => ""
    };
}
