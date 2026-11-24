using System.Xml.Linq;

using Core30.Core;

namespace Core30.PmlTwin;

/// <summary>Reads the AVEVA login identity from one PID and start time, independent of the launch request.</summary>
internal sealed class AvevaLoginSession(IPhaytonFunctionClient client)
{
    private const string Execute = "--allow-execution";
    private static readonly string[] IdentityFields = ["Project", "User", "Mdb", "Module", "Product", "Version"];

    internal CoreData Read(CoreData request)
    {
        var target = new PhaytonTarget((int)request.Field("pid")!, (long)request.Field("startTicks")!, "");
        var status = client.Run("aveva.session.status", new { }, target, timeout: 3);
        if (!status.Success || !target.Matches(status.Data))
            return Result("waiting-host", "尚未收到此进程的可用 PmlTwin Host。", status.Describe());
        return ReadIdentity(target with { Model = status.Data.Field("Host").String("Model") }, status);
    }

    internal CoreData ReadIdentity(PhaytonTarget target, PhaytonReceipt status)
    {
        var token = Guid.NewGuid().ToString("N");
        var project = "Core30LoginProject" + token;
        var fields = "Core30LoginFields" + token;
        var setup = Command($"!!{project} = CURRENT PROJECT", target);
        if (!setup.HostSucceeded)
            return Failure(setup, status);

        PhaytonReceipt? values = null;
        try
        {
            values = Command($"!!{fields} = {IdentityExpression(project)}", target);
            if (!values.HostSucceeded)
                return Failure(values, status);
            var reply = client.Run("pml.array.read", new
            {
                pmlCommand = $"!!{fields}.Split('|')",
                globalVar = "Core30LoginRead" + token,
                deleteGlobalVar = true,
                includeEmpty = true
            }, target, Execute, timeout: 5);
            return ReadResult(target, status, reply);
        }
        finally
        {
            if (values?.HostSucceeded == true)
                Command($"!!{fields}.delete()", target);
            Command($"!!{project}.delete()", target);
        }
    }

    private PhaytonReceipt Command(string command, PhaytonTarget target) =>
        client.Run("pml.command.execute", new { pmlCommand = command }, target, Execute, timeout: 5);

    private static string IdentityExpression(string project)
    {
        var session = $"!!{project}.CurrentSession()";
        string[] expressions =
        [
            $"!!{project}.Code()", $"{session}.User().Name", $"{session}.MDB().Name",
            $"{session}.Module()", "!!PhaytonProduct", "!!PhaytonVersion"
        ];
        return string.Join(" + '|' + ", IdentityFields.Zip(expressions, (name, expression) => $"'[0] {name}<STRING> ' + {expression}"));
    }

    private static CoreData ReadResult(PhaytonTarget target, PhaytonReceipt status, PhaytonReceipt reply)
    {
        if (!reply.HostSucceeded)
            return Failure(reply, status);
        var fields = ReadFields(reply);
        if (fields is null)
            return Result("unverified", "Host 回执缺少实际项目、用户、MDB、模块或产品版本。", status.Describe(), reply.Describe());
        return Result("observed", "", new CoreData("session",
            new CoreField("pid", target.Pid), new CoreField("startTicks", target.StartTicks),
            fields.Select((value, index) => new CoreField(IdentityFields[index].ToLowerInvariant(), value))),
            status.Describe(), reply.Describe());
    }

    private static string[]? ReadFields(PhaytonReceipt reply)
    {
        var items = reply.Data.Field("Items").Items().ToArray();
        if (items.Length != IdentityFields.Length || reply.Data.Integer("UnparsedCount") != 0)
            return null;
        var values = IdentityFields.Select(name => items
            .FirstOrDefault(item => item.String("Path") == name && item.String("Type") == "STRING").String("Value").Trim()).ToArray();
        return values.All(value => value.Length != 0) ? values : null;
    }

    private static CoreData Failure(PhaytonReceipt error, PhaytonReceipt status)
    {
        var detail = error.Data.String("ErrorMessage");
        if (detail.Length == 0)
            detail = error.Message;
        return Result("unverified", detail.Length == 0 ? "Host 尚未能读取当前登录会话。" : detail,
            status.Describe(), error.Describe());
    }

    private static CoreData Result(string state, string message, params CoreData[] evidence) =>
        new("result", new CoreField("state", state), new CoreData("message", message), evidence);
}
