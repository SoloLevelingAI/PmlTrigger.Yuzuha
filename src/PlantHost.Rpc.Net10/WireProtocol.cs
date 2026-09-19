using System;
using System.IO;
using System.Text;

namespace PlantHost.Rpc
{
    internal sealed class RpcWireRequest
    {
        public Guid RequestId;
        public string Service;
        public string Operation;
        public string Serializer;
        public byte[] Payload;
    }

    internal sealed class RpcWireResponse
    {
        public Guid RequestId;
        public bool Success;
        public string ErrorCode;
        public string ErrorMessage;
        public byte[] Payload;
    }

    internal static class WireProtocol
    {
        internal const int Version = 1;
        internal const int MaxPayloadLength = 16 * 1024 * 1024;
        internal const int MaxFrameLength = 17 * 1024 * 1024;
        private const int RequestMagic = 0x5152504D;  // MPRQ
        private const int ResponseMagic = 0x5352504D; // MPRS
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public static byte[] EncodeRequest(RpcWireRequest request)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Utf8))
            {
                writer.Write(RequestMagic);
                writer.Write(Version);
                writer.Write(request.RequestId.ToByteArray());
                WriteString(writer, request.Service);
                WriteString(writer, request.Operation);
                WriteString(writer, request.Serializer);
                WriteBytes(writer, request.Payload);
                writer.Flush();
                return stream.ToArray();
            }
        }

        public static RpcWireRequest DecodeRequest(byte[] data)
        {
            using (var stream = new MemoryStream(data, false))
            using (var reader = new BinaryReader(stream, Utf8))
            {
                Require(reader.ReadInt32() == RequestMagic, "Invalid request magic.");
                Require(reader.ReadInt32() == Version, "Unsupported protocol version.");

                var request = new RpcWireRequest
                {
                    RequestId = new Guid(ReadFixed(reader, 16)),
                    Service = ReadString(reader),
                    Operation = ReadString(reader),
                    Serializer = ReadString(reader),
                    Payload = ReadBytes(reader)
                };

                Require(stream.Position == stream.Length, "Trailing request data.");
                return request;
            }
        }

        public static byte[] EncodeResponse(RpcWireResponse response)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Utf8))
            {
                writer.Write(ResponseMagic);
                writer.Write(Version);
                writer.Write(response.RequestId.ToByteArray());
                writer.Write(response.Success);
                WriteString(writer, response.ErrorCode ?? string.Empty);
                WriteString(writer, response.ErrorMessage ?? string.Empty);
                WriteBytes(writer, response.Payload ?? Array.Empty<byte>());
                writer.Flush();
                return stream.ToArray();
            }
        }

        public static RpcWireResponse DecodeResponse(byte[] data)
        {
            using (var stream = new MemoryStream(data, false))
            using (var reader = new BinaryReader(stream, Utf8))
            {
                Require(reader.ReadInt32() == ResponseMagic, "Invalid response magic.");
                Require(reader.ReadInt32() == Version, "Unsupported protocol version.");

                var response = new RpcWireResponse
                {
                    RequestId = new Guid(ReadFixed(reader, 16)),
                    Success = reader.ReadBoolean(),
                    ErrorCode = ReadString(reader),
                    ErrorMessage = ReadString(reader),
                    Payload = ReadBytes(reader)
                };

                Require(stream.Position == stream.Length, "Trailing response data.");
                return response;
            }
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            if (value == null)
                throw new InvalidDataException("Protocol strings cannot be null.");

            byte[] bytes = Utf8.GetBytes(value);
            if (bytes.Length > 64 * 1024)
                throw new InvalidDataException("Protocol string is too long.");

            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            Require(length >= 0 && length <= 64 * 1024, "Invalid string length.");
            return Utf8.GetString(ReadFixed(reader, length));
        }

        private static void WriteBytes(BinaryWriter writer, byte[] value)
        {
            if (value == null)
                throw new InvalidDataException("Protocol payload cannot be null.");
            if (value.Length > MaxPayloadLength)
                throw new InvalidDataException("Payload exceeds the configured maximum.");

            writer.Write(value.Length);
            writer.Write(value);
        }

        private static byte[] ReadBytes(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            Require(length >= 0 && length <= MaxPayloadLength, "Invalid payload length.");
            return ReadFixed(reader, length);
        }

        private static byte[] ReadFixed(BinaryReader reader, int length)
        {
            byte[] result = reader.ReadBytes(length);
            Require(result.Length == length, "Unexpected end of protocol frame.");
            return result;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidDataException(message);
        }
    }
}
