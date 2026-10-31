using System.Runtime.InteropServices;
using System.Text;
using LaunchDesk.Next.Application.Infrastructure.Contracts;
namespace LaunchDesk.Next.Platform.Credentials;

public sealed class SecretHandle : ISecretHandle
{
    private char[]? value;
    public SecretHandle(string secret) { value = secret.ToCharArray(); }
    public SecretHandle(char[] secret) { value = (char[])secret.Clone(); }
    public string Reveal() => value is null ? throw new ObjectDisposedException(nameof(SecretHandle)) : new string(value);
    public void Dispose()
    {
        if (value != null) Array.Clear(value);
        value = null;
    }
    public override string ToString() => "[secret]";
}
public sealed class WindowsCredentialStore : ICredentialStore
{
    private const string Prefix = "LaunchDesk.Next/";
    public ISecretHandle? Read(string reference)
    {
        if (!CredRead(Prefix + reference, 1, 0, out var pointer)) return null;
        try
        {
            var value = Marshal.PtrToStructure<Credential>(pointer);
            var bytes = new byte[value.Size];
            Marshal.Copy(value.Blob, bytes, 0, bytes.Length);
            try { return new SecretHandle(Encoding.Unicode.GetString(bytes)); }
            finally { Array.Clear(bytes); }
        }
        finally { CredFree(pointer); }
    }
    public void Save(string reference, ISecretHandle secret)
    {
        if (string.IsNullOrWhiteSpace(reference)) throw new ArgumentException("Credential reference required.");
        var bytes = Encoding.Unicode.GetBytes(secret.Reveal());
        var pointer = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            var credential = new Credential { Type = 1, Target = Prefix + reference, Size = (uint)bytes.Length, Blob = pointer, Persist = 2, User = "LaunchDesk.Next" };
            if (!CredWrite(ref credential, 0)) throw new InvalidOperationException("Windows credential save failed.");
        }
        finally
        {
            for (var i = 0; i < bytes.Length; i++) Marshal.WriteByte(pointer, i, 0);
            Marshal.FreeHGlobal(pointer);
            Array.Clear(bytes);
        }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct Credential { public uint Flags, Type; public string Target; public string? Comment; public long LastWritten; public uint Size; public IntPtr Blob; public uint Persist, AttributeCount; public IntPtr Attributes; public string? Alias; public string User; }
    [DllImport("advapi32", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CredWrite(ref Credential credential, uint flags);
    [DllImport("advapi32")] private static extern void CredFree(IntPtr pointer);
}
