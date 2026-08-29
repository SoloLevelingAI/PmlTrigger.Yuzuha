#nullable disable
using System.Globalization;
using System.Text;

namespace YuzuhaToolkit.Pml;

/// <summary>
///     One parameter parsed from an external source such as JSON or Markdown.
///     TypeName and Value are data; they do not come from a C# method signature.
/// </summary>
public sealed class DynamicParameter
{
    public DynamicParameter(string typeName, object value)
    {
        TypeName = typeName;
        Value = value;
    }

    public string TypeName { get; }

    public object Value { get; }
}

/// <summary>
///     Generates PML calls from an ordered collection of dynamic parameter data.
///     Compatible with .NET Framework 3.5.
/// </summary>
public static class PmlMethodSignatureGenerator
{
    public static string Build(
        string methodName,
        params DynamicParameter[] parameters)
    {
        ValidateMethodName(methodName);

        var result = new StringBuilder();
        result.Append("!!");
        result.Append(methodName.Trim());
        result.Append("(");

        if (parameters != null)
            for (var index = 0; index < parameters.Length; index++)
            {
                if (index > 0) result.Append(",");

                result.Append(FormatParameter(parameters[index], index));
            }

        result.Append(")");
        return result.ToString();
    }

    public static string Build(
        string methodName,
        IList<DynamicParameter> parameters)
    {
        if (parameters == null) return Build(methodName);

        var values =
            new DynamicParameter[parameters.Count];
        parameters.CopyTo(values, 0);
        return Build(methodName, values);
    }

    private static string FormatParameter(
        DynamicParameter parameter,
        int parameterIndex)
    {
        if (parameter == null)
            throw new ArgumentNullException(
                "parameters",
                "Parameter at index " + parameterIndex + " is null.");

        if (string.IsNullOrEmpty(parameter.TypeName))
            throw new ArgumentException(
                "Type name at parameter index " + parameterIndex +
                " cannot be empty.");

        var normalizedType =
            parameter.TypeName.Trim().ToLowerInvariant();

        switch (normalizedType)
        {
            case "string":
            case "str":
                return FormatString(parameter.Value);

            case "bool":
            case "boolean":
                return FormatBoolean(parameter.Value, parameterIndex);

            case "double":
            case "real":
            case "number":
                return FormatDouble(parameter.Value, parameterIndex);

            default:
                throw new NotSupportedException(
                    "Unsupported type '" + parameter.TypeName +
                    "' at parameter index " + parameterIndex +
                    ". Supported types are string, double and bool.");
        }
    }

    private static string FormatString(object value)
    {
        var text = value == null
            ? string.Empty
            : Convert.ToString(value, CultureInfo.InvariantCulture);

        // PML string literals use single quotes. A quote inside the value
        // is represented by two consecutive single quotes.
        return "'" + text.Replace("'", "''") + "'";
    }

    private static string FormatBoolean(object value, int parameterIndex)
    {
        if (value is bool) return (bool)value ? "TRUE" : "FALSE";

        var text = value == null
            ? string.Empty
            : Convert.ToString(value, CultureInfo.InvariantCulture).Trim();

        if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)) return "TRUE";

        if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase)) return "FALSE";

        throw new FormatException(
            "Value '" + text + "' at parameter index " +
            parameterIndex + " is not a valid bool.");
    }

    private static string FormatDouble(object value, int parameterIndex)
    {
        try
        {
            double number;
            if (value is double)
            {
                number = (double)value;
            }
            else
            {
                var text = value == null
                    ? string.Empty
                    : Convert.ToString(
                        value,
                        CultureInfo.InvariantCulture).Trim();

                number = double.Parse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture);
            }

            return number.ToString(
                "0.###############################",
                CultureInfo.InvariantCulture);
        }
        catch (Exception exception)
        {
            throw new FormatException(
                "Value at parameter index " + parameterIndex +
                " is not a valid double.",
                exception);
        }
    }

    private static void ValidateMethodName(string methodName)
    {
        if (string.IsNullOrEmpty(methodName) ||
            methodName.Trim().Length == 0)
            throw new ArgumentException(
                "Method name cannot be empty.",
                "methodName");
    }
}