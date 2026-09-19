using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PlantHost.Rpc
{
    internal static class PipeFraming
    {
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
