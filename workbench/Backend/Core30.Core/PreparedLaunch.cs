using System.Xml.Linq;

namespace Core30.Core;

/// <summary>Submits the vendor parameters prepared by the launch-profile service.</summary>
public static class PreparedLaunch
{
    private static readonly XNamespace Contract = "urn:core30:F01:v2";

    public static CoreData Parameters(CoreData prepared, string? module = null)
    {
        var request = new CoreData(prepared.Child(Contract + "request")
            ?? throw new InvalidDataException("配置表未返回启动参数。"));
        if (module is not null)
            request.SetField("module", module);
        if (string.IsNullOrEmpty((string?)request.Field("licenseHost")))
        {
            request.SetField("licenseHost", null);
            request.SetField("licensePort", null);
        }
        return request;
    }

    public static CoreRequest Confirm(CoreData prepared, string? module = null)
    {
        var request = Parameters(prepared, module);
        request.SetField("mode", "launch");
        request.SetField("confirm", true);
        return CoreRequest.FromForm("F01", "launcher", request);
    }

    public static CoreRequest Preview(CoreData prepared, string? module = null) =>
        CoreRequest.FromForm("F01", "launcher", Parameters(prepared, module));
}
