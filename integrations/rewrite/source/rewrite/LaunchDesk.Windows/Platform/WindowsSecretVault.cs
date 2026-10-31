namespace LaunchDesk.Windows;

/// <summary>隔离 Windows 凭据 API，秘密只在本机短暂取得。</summary>
public sealed class WindowsSecretVault : ISecretVault
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags, Type;
        public string TargetName, Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint BlobSize;
        public IntPtr Blob;
        public uint Persist, AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias, UserName;
    }

    /// <inheritdoc />
    public ValueTask<Result<CredentialRef>> StoreNewAsync(TransientSecret secret, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var span = secret.Borrow();
        if (span.Length > 2560 || span.Contains('\0'))
            return ValueTask.FromResult(Result<CredentialRef>.Fail("InvalidSecret", "密码长度或字符无效。"));
        var bytes = new byte[Encoding.Unicode.GetByteCount(span)];
        Encoding.Unicode.GetBytes(span, bytes);
        var memory = Marshal.AllocCoTaskMem(Math.Max(2, bytes.Length));
        try
        {
            Marshal.Copy(bytes, 0, memory, bytes.Length);
            var reference = new CredentialRef("LaunchDesk/v3/Profile/" + Guid.NewGuid().ToString("N"));
            var credential = new Credential
            {
                Type = 1, TargetName = reference.Value, Comment = "LaunchDesk project credential",
                BlobSize = (uint)bytes.Length, Blob = memory, Persist = 2, UserName = "LaunchDesk", TargetAlias = ""
            };
            return ValueTask.FromResult(CredWriteW(ref credential, 0)
                ? Result<CredentialRef>.Ok(reference)
                : Result<CredentialRef>.Fail("CredentialWriteFailed", "Windows 凭据保存失败，原关联将保留。"));
        }
        finally
        {
            Array.Clear(bytes);
            for (var i = 0; i < Math.Max(2, bytes.Length); i++) Marshal.WriteByte(memory, i, 0);
            Marshal.FreeCoTaskMem(memory);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<SecretLease>> ResolveAsync(CredentialRef reference, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Valid(reference) || !CredReadW(reference.Value, 1, 0, out var pointer))
            return ValueTask.FromResult(Result<SecretLease>.Fail("CredentialMissing", "未找到可用的本机凭据，请重新输入密码。"));
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            if (credential.BlobSize > 5120 || credential.BlobSize % 2 != 0)
                return ValueTask.FromResult(Result<SecretLease>.Fail("InvalidCredential", "本机凭据格式无效。"));
            var bytes = new byte[credential.BlobSize];
            Marshal.Copy(credential.Blob, bytes, 0, bytes.Length);
            var characters = Encoding.Unicode.GetChars(bytes);
            try
            {
                return ValueTask.FromResult(Result<SecretLease>.Ok(new SecretLease(new TransientSecret(characters))));
            }
            finally
            {
                Array.Clear(bytes);
                Array.Clear(characters);
            }
        }
        finally
        {
            CredFree(pointer);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> DeleteAsync(CredentialRef unreferenced, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Valid(unreferenced)) return ValueTask.FromResult(Result<Unit>.Fail("InvalidCredential", "凭据引用无效。"));
        return ValueTask.FromResult(CredDeleteW(unreferenced.Value, 1, 0) || Marshal.GetLastWin32Error() == 1168
            ? Result<Unit>.Ok(new Unit())
            : Result<Unit>.Fail("CredentialDeleteFailed", "旧凭据未能清理，保留供后续核验。"));
    }

    private static bool Valid(CredentialRef reference)
    {
        return reference.Value.StartsWith("LaunchDesk/v3/Profile/", StringComparison.Ordinal) ||
               reference.Value.StartsWith("LaunchDesk/Profile/", StringComparison.Ordinal);
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWriteW(ref Credential credential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDeleteW(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr pointer);
}