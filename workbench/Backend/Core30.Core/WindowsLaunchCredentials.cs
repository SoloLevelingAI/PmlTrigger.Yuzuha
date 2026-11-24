using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Core30.Core;

public static class LaunchCredentialKeys
{
    public static string Target(string workspace, string profileId) =>
        $"Core30/LaunchDesk/{Hash(Path.TrimEndingDirectorySeparator(workspace), profileId)}";

    public static string Identity(string product, string installation, string project, string user) =>
        Hash(product, Path.TrimEndingDirectorySeparator(installation), project.Trim(), user.Trim());

    private static string Hash(params string[] parts) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\0', parts).ToUpperInvariant())));
}

/// <summary>Stores a generic credential for the current Windows user on this computer.</summary>
public sealed class WindowsLaunchCredentials
{
    private const int Generic = 1;
    private const int LocalMachine = 2;
    private const int NotFound = 1168;
    private const int BlobLimit = 2560;

    public bool Exists(string target, string identity) => ReadStored(target, identity, false, out _);

    public string? Read(string target, string identity) =>
        ReadStored(target, identity, true, out var password) ? password : null;

    private static bool ReadStored(string target, string identity, bool includePassword, out string? password)
    {
        password = null;
        if (!CredRead(target, Generic, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            return error == NotFound ? false : throw new Win32Exception(error);
        }
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            if (credential.UserName != identity || credential.Blob == IntPtr.Zero ||
                credential.BlobSize is <= 0 or > BlobLimit || credential.BlobSize % sizeof(char) != 0)
                return false;
            if (includePassword)
                password = Marshal.PtrToStringUni(credential.Blob, credential.BlobSize / sizeof(char));
            return true;
        }
        finally
        {
            CredFree(pointer);
        }
    }

    public void Write(string target, string identity, string password)
    {
        if (password.Length is < 1 or > 1024 || password.IndexOfAny(['\0', '\r', '\n']) >= 0)
            throw new Win32Exception(87);
        var blob = Marshal.StringToCoTaskMemUni(password);
        try
        {
            var credential = new Credential { Type = Generic, TargetName = target, UserName = identity,
                BlobSize = password.Length * sizeof(char), Blob = blob, Persist = LocalMachine };
            if (!CredWrite(ref credential, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            Marshal.ZeroFreeCoTaskMemUnicode(blob);
        }
    }

    public void Delete(string target)
    {
        if (CredDelete(target, Generic, 0))
            return;
        var error = Marshal.GetLastWin32Error();
        if (error != NotFound)
            throw new Win32Exception(error);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public int Flags, Type;
        public string? TargetName, Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int BlobSize;
        public IntPtr Blob;
        public int Persist, AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias, UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern void CredFree(IntPtr credential);
}
