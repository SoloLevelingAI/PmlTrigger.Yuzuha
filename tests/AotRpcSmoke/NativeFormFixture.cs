using System;
using YuzuhaToolkit.Pml;

// Synthetic query values only. Exercises the production reader on CLR 2/4 without AVEVA.
internal class NativeFormFixture : IPmlFormQueryRuntime
{
    private string expression;
    public void Execute(string command)
    {
        int assignment = command.IndexOf(" = ", StringComparison.Ordinal);
        if (assignment >= 0) expression = command.Substring(assignment + 3);
        if (command.EndsWith(".active.string()", StringComparison.Ordinal))
            throw new InvalidOperationException("Synthetic unavailable active metadata");
    }
    public double ReadReal(string variable) { return 3; }
    public string ReadString(string variable)
    {
        if (expression == "!!Synthetic.objectType()") return "FORM";
        if (expression.EndsWith(".objectType()", StringComparison.Ordinal)) return "GADGET";
        if (expression.EndsWith(".type()", StringComparison.Ordinal)) return "BUTTON";
        if (expression.EndsWith(".name()", StringComparison.Ordinal)) return expression.IndexOf("[2]", StringComparison.Ordinal) >= 0 ? "Synthetic2" : "Synthetic1";
        if (expression.EndsWith(".shown().string()", StringComparison.Ordinal)) return "TRUE";
        if (expression.StartsWith("!!YUZUHAUIEXISTS", StringComparison.Ordinal) || expression.EndsWith(".set().string()", StringComparison.Ordinal)) return "TRUE";
        if (expression.EndsWith(".callback", StringComparison.Ordinal)) return "!!SyntheticCallback('测试')";
        if (expression.EndsWith(".tooltip", StringComparison.Ordinal)) return "测试 'quoted'";
        return "accept";
    }
}
