namespace LaunchDesk.Kernel;

/// <summary>校验Marine多层cmd链可原样传递的单词元；不执行命令，不改厂商批处理。</summary>
/// <remarks>marine.bat以%1原样比较模块，再经cmd /c转交参数。一次转义无法保证引用、变量展开和命令控制字符经多层解析后仍保留原值；须拒绝不支持的输入；原生EXE不受此限。</remarks>
public static class MarineBatchPolicy
{
    /// <summary>判断非空词元能否在 Marine 无额外引号的参数协议中保持字面内容。</summary>
    /// <param name="value">项目、模块、MDB、用户或运行时组合登录词元；不得记录秘密实值。</param>
    /// <returns>不含空白、控制字符、引用、变量展开、命令控制或批处理参数分隔符时为 true。</returns>
    public static bool IsLiteralToken(string value) => !string.IsNullOrEmpty(value) && value.Length <= 1024 &&
        !value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character) ||
            character is '"' or '%' or '!' or '^' or '&' or '|' or '<' or '>' or '(' or ')' or ',' or ';' or '=');
}
