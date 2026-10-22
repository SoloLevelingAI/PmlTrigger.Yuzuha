using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PlantHost.Rpc
{
    /// <summary>通过有长度前缀的帧读写管道；限制帧大小并拒绝不完整数据。</summary>
    internal static class PipeFraming
    {
        /// <summary>把完整帧及长度前缀写入流，遵守取消并限制最大帧长度。</summary>
        /// <param name="stream">承载长度前缀帧的输入或输出流。</param>
        /// <param name="frame">需要写入的完整帧内容。</param>
        /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
        /// <returns>表示异步操作完成的任务；取消或传输失败通过异常传播。</returns>
        public static async Task WriteAsync(
            Stream stream,
            byte[] frame,
            CancellationToken cancellationToken)
        {
            if (frame.Length > WireProtocol.MaxFrameLength)
                throw new InvalidDataException("Frame exceeds the configured maximum.");

            byte[] length = BitConverter.GetBytes(frame.Length);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(length);

            await stream.WriteAsync(length, 0, length.Length, cancellationToken)
                .ConfigureAwait(false);
            await stream.WriteAsync(frame, 0, frame.Length, cancellationToken)
                .ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>读取并校验长度前缀和完整帧；遇到 EOF 或非法长度时失败。</summary>
        /// <param name="stream">承载长度前缀帧的输入或输出流。</param>
        /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
        public static async Task<byte[]> ReadAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {
            byte[] lengthBytes = new byte[4];
            await ReadExactlyAsync(stream, lengthBytes, cancellationToken)
                .ConfigureAwait(false);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);

            int length = BitConverter.ToInt32(lengthBytes, 0);
            if (length <= 0 || length > WireProtocol.MaxFrameLength)
                throw new InvalidDataException("Invalid RPC frame length.");

            byte[] frame = new byte[length];
            await ReadExactlyAsync(stream, frame, cancellationToken)
                .ConfigureAwait(false);
            return frame;
        }

        private static async Task ReadExactlyAsync(
            Stream stream,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = await stream.ReadAsync(
                        buffer,
                        offset,
                        buffer.Length - offset,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (read == 0)
                    throw new EndOfStreamException("The RPC peer closed the pipe.");

                offset += read;
            }
        }
    }
}
