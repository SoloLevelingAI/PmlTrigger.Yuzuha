#if NET35
using NativeCommand = Aveva.Pdms.Utilities.CommandLine.Command;
#else
using NativeCommand = Aveva.Core.Utilities.CommandLine.Command;
#endif
using System;
using YuzuhaToolkit.Pml;

namespace YuzuhaToolkit.PmlHost
{
    /// <summary>把共享的只读窗体查询适配到 AVEVA 命令 API；调用者必须先断言主线程。</summary>
    internal sealed class PmlFormQueryHost : IPmlFormQueryRuntime
    {
        private NativeCommand current;

        /// <summary>执行查询生成的命令，原生 false 明确转为读取失败。</summary>
        public void Execute(string command)
        {
            try
            {
                current = NativeCommand.CreateCommand(command);
                if (!current.Run()) throw new InvalidOperationException("PML_UI_READ_FAILED: " + command + " | " + ReadNativeError());
            }
            catch (Exception error) { throw new InvalidOperationException("Native form query command failed: " + command + " | " + error.Message, error); }
        }

        private static string ReadNativeError()
        {
            string variable = "YUZUHAUIERROR" + Guid.NewGuid().ToString("N").ToUpperInvariant();
            try
            {
                NativeCommand read = NativeCommand.CreateCommand("!!" + variable + " = !!error.text & ' | ' & !!error.command & ' | ' & !!error.line");
                if (!read.Run()) return "Native error details unavailable.";
                string text = read.GetPMLVariableString(variable);
                return text == null ? "Native error details unavailable." : text.Length > 32768 ? text.Substring(0, 32768) : text;
            }
            catch (Exception error) { return "Native error details unavailable: " + error.Message; }
            finally { try { NativeCommand.CreateCommand("!!" + variable + ".delete()").Run(); } catch { } }
        }

        /// <summary>读取本次命令的字符串变量；原生异常保留为查询失败。</summary>
        public string ReadString(string variable)
        {
            try { return current.GetPMLVariableString(variable); }
            catch (Exception error) { throw new InvalidOperationException("Native form query string read failed.", error); }
        }

        /// <summary>读取本次命令的 MAXINDEX 实数变量。</summary>
        public double ReadReal(string variable)
        {
            return current.GetPMLVariableReal(variable);
        }
    }
}
