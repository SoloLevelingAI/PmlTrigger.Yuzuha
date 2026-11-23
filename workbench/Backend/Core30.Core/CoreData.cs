using System.Collections;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Core30.Core;

/// <summary>Named sections and scalar fields passed directly between feature handlers and callers.</summary>
public sealed class CoreData(XName name)
{
    private readonly List<CoreField> _fields = [];
    private readonly List<object> _body = [];

    public CoreData(XName name, params object?[] content) : this(name) => Add(content);
    public CoreData(CoreData source) : this(source.Name)
    {
        _fields.AddRange(source._fields);
        foreach (var item in source._body)
            _body.Add(item is CoreData child ? new CoreData(child) : item);
    }

    public XName Name { get; } = name;
    public string Value => string.Concat(_body.Select(item => item is CoreData child ? child.Value : item));
    public IReadOnlyList<CoreField> Fields() => _fields;
    public CoreField? Field(XName name) => _fields.Find(field => field.Name == name);
    internal IEnumerable<object> Body => _body;
    public IEnumerable<CoreData> Children() => _body.OfType<CoreData>();
    public IEnumerable<CoreData> Children(XName name) => Children().Where(child => child.Name == name);
    public CoreData? Child(XName name) => Children(name).FirstOrDefault();
    public IEnumerable<CoreData> Descendants() => Children().SelectMany(child => child.DescendantsAndSelf());
    public IEnumerable<CoreData> Descendants(XName name) => Descendants().Where(child => child.Name == name);
    public IEnumerable<CoreData> DescendantsAndSelf() => Descendants().Prepend(this);
    public bool HasChildren => Children().Any();

    public void ReplaceChildren(IEnumerable<CoreData> children)
    {
        var replacement = children.ToArray();
        _body.Clear();
        _body.AddRange(replacement);
    }

    public bool ContentEquals(CoreData other) => Name == other.Name &&
        _fields.Select(field => (field.Name, field.Value)).SequenceEqual(other._fields.Select(field => (field.Name, field.Value))) &&
        _body.Count == other._body.Count && _body.Zip(other._body).All(pair => pair.First is CoreData child
            ? pair.Second is CoreData otherChild && child.ContentEquals(otherChild)
            : Equals(pair.First, pair.Second));

    public void SetField(XName name, object? value)
    {
        var index = _fields.FindIndex(field => field.Name == name);
        if (value is null)
        {
            if (index >= 0)
                _fields.RemoveAt(index);
            return;
        }
        var replacement = new CoreField(name, value);
        if (index >= 0)
            _fields[index] = replacement;
        else
            _fields.Add(replacement);
    }

    public void Add(params object?[] values)
    {
        foreach (var value in values)
            AddValue(value);
    }

    private void AddValue(object? value)
    {
        switch (value)
        {
            case null:
                break;
            case CoreField field:
                if (Field(field.Name) is not null)
                    throw new InvalidOperationException($"重复字段 {field.Name}。");
                _fields.Add(field);
                break;
            case CoreData child:
                _body.Add(child);
                break;
            case string text:
                _body.Add(text);
                break;
            case IEnumerable sequence:
                foreach (var entry in sequence)
                    AddValue(entry);
                break;
            case XObject:
                throw new ArgumentException("请在 XML 边界读取数据。", nameof(value));
            default:
                _body.Add(CoreField.Format(value));
                break;
        }
    }

    public static explicit operator string?(CoreData? value) => value?.Value;
}

public sealed record CoreField(XName Name, object Scalar)
{
    public string Value => Format(Scalar);

    internal static string Format(object value) => value switch
    {
        bool item => XmlConvert.ToString(item),
        DateTime item => XmlConvert.ToString(item, XmlDateTimeSerializationMode.RoundtripKind),
        DateTimeOffset item => XmlConvert.ToString(item),
        TimeSpan item => XmlConvert.ToString(item),
        float item => XmlConvert.ToString(item),
        double item => XmlConvert.ToString(item),
        IFormattable item => item.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };

    public static explicit operator string?(CoreField? value) => value?.Value;
    public static explicit operator bool(CoreField value) => XmlConvert.ToBoolean(value.Value);
    public static explicit operator bool?(CoreField? value) => value is null ? null : (bool)value;
    public static explicit operator int(CoreField value) => XmlConvert.ToInt32(value.Value);
    public static explicit operator int?(CoreField? value) => value is null ? null : (int)value;
    public static explicit operator long(CoreField value) => XmlConvert.ToInt64(value.Value);
    public static explicit operator long?(CoreField? value) => value is null ? null : (long)value;
    public static explicit operator uint?(CoreField? value) => value is null ? null : XmlConvert.ToUInt32(value.Value);
    public static explicit operator DateTimeOffset?(CoreField? value) => value is null ? null : XmlConvert.ToDateTimeOffset(value.Value);
}

public sealed class CoreRequest(string feature, string module, CoreData data)
{
    private readonly CoreData _data = new(data);

    public string Feature { get; } = feature;
    public string Module { get; } = module;
    public CoreData Data => new(_data);

    public static CoreRequest FromForm(string feature, string? module, CoreData data) => new(feature, module ?? "", data);
}

public sealed record CoreReceipt<T>(string Feature, string Version, string Configuration, string Log, T Result);

/// <summary>Information supplied by the caller for this execution only.</summary>
public sealed record RunInput(Func<string?>? ReadPassword = null, string? FixtureHost = null,
    CoreData? InstallationScan = null, CoreData? FunctionInventory = null)
{
    public CancellationToken Cancellation { get; init; }
}
