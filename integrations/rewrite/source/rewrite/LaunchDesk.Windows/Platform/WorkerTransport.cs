using System.Buffers.Binary;

namespace LaunchDesk.Windows;

/// <summary>父子进程之间读写有界协议消息；身份认证由调用方完成。</summary>
public static class WorkerTransport
{
    /// <summary>向传入的数据流写入带长度上限的有限 Worker 协议消息；对端身份由调用方核验。</summary>
    /// <param name="stream">调用方拥有生命周期的协议数据流。</param>
    /// <param name="message">含有限信封、事实或回执的 Worker 协议消息；禁止传输运行时秘密对象。</param>
    /// <include file="../../Comments.xml" path="doc/AsyncResult/param"/>
    /// <returns>异步完成任务。</returns>
    public static async ValueTask WriteAsync(Stream stream, WorkerMessage message, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, AtomicFiles.Json);
        if (bytes.Length > 32 * 1024 * 1024) throw new InvalidDataException("Worker message exceeds limit.");
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, ct);
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
    }

    /// <summary>读取并校验有长度边界的 Worker 协议消息；反序列化成功不代替对端身份核验。</summary>
    /// <param name="stream">调用方拥有生命周期的协议数据流。</param>
    /// <include file="../../Comments.xml" path="doc/AsyncResult/param"/>
    /// <returns>完成长度及 JSON 结构校验的消息；协议不完整、过大或为空时抛出异常。</returns>
    public static async ValueTask<WorkerMessage> ReadAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, ct);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is < 1 or > 32 * 1024 * 1024) throw new InvalidDataException("Invalid worker message size.");
        var bytes = new byte[length];
        await stream.ReadExactlyAsync(bytes, ct);
        return JsonSerializer.Deserialize<WorkerMessage>(bytes, AtomicFiles.Json) ??
               throw new InvalidDataException("Worker message is empty.");
    }
}
