using System;
using System.IO;
using System.Text;

namespace PlantHost.Rpc
{
    /// <summary>二进制管道请求包，保存关联编号、路由、序列化名称和载荷。</summary>
    internal sealed class RpcWireRequest
    {
        /// <summary>用于关联一个请求与响应的编号。</summary>
        public Guid RequestId;
        /// <summary>RPC 服务的稳定路由名称。</summary>
        public string Service;
        /// <summary>RPC 操作的稳定路由名称。</summary>
        public string Operation;
        /// <summary>载荷采用的协议序列化格式名称。</summary>
        public string Serializer;
        /// <summary>该协议帧承载的序列化字节。</summary>
        public byte[] Payload;
    }

    /// <summary>与请求关联的成功标志、错误代码和响应载荷。</summary>
    internal sealed class RpcWireResponse
    {
        /// <summary>用于关联一个请求与响应的编号。</summary>
        public Guid RequestId;
        /// <summary>该次协议或业务操作报告的成功标志。</summary>
        public bool Success;
        /// <summary>机器可判定的协议或执行错误代码。</summary>
        public string ErrorCode;
        /// <summary>执行失败的可读原因，成功时通常为空。</summary>
        public string ErrorMessage;
        /// <summary>该协议帧承载的序列化字节。</summary>
        public byte[] Payload;
    }

    /// <summary>PlantHost v1 二进制帧编解码，校验魔数、版本、长度和尾随数据。</summary>
    internal static class WireProtocol
    {
        /// <summary>协议或状态快照版本，用于判断兼容性或更新次序。</summary>
        internal const int Version = 1;
        /// <summary>单个协议载荷允许的最大字节数。</summary>
        internal const int MaxPayloadLength = 16 * 1024 * 1024;
        /// <summary>包含协议字段的最大帧字节数。</summary>
        internal const int MaxFrameLength = 17 * 1024 * 1024;
        private const int RequestMagic = 0x5152504D;  // MPRQ
        private const int ResponseMagic = 0x5352504D; // MPRS
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        /// <summary>编码 v1 请求魔数、版本、关联编号、路由和载荷。</summary>
        /// <param name="request">当前操作的请求数据及其显式目标或参数。</param>
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

        /// <summary>解析并校验 v1 请求包，拒绝非法长度、版本与尾随字节。</summary>
        /// <param name="data">需要解析的完整二进制协议数据。</param>
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

        /// <summary>编码与请求关联的执行状态、错误及响应载荷。</summary>
        /// <param name="response">需要编码的服务端响应状态与载荷。</param>
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

        /// <summary>解析并校验 v1 响应包，拒绝非法长度、版本与尾随字节。</summary>
        /// <param name="data">需要解析的完整二进制协议数据。</param>
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
