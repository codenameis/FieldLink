using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.PlcDrivers.Common.Clients
{
    // 자료형 변환/사용자 수명은 PlcClient, 주소/패킷/협상은 제조사 드라이버가 소유한다.
    internal interface IPlcDriver
    {
        int StringAlignment { get; }
        bool HasStringLength { get; }
        Task InitializeAsync(TimeSpan timeout, CancellationToken token);
        Task ShutdownAsync(TimeSpan timeout, CancellationToken token);
        Task<OperationResult<byte[]>> TransferAsync(PlcTransferRequest request, TimeSpan timeout, CancellationToken token);
    }

    internal enum PlcValueType { Bytes, Boolean, Int16, UInt16, Int32, UInt32, Int64, UInt64, Float, Double, String }

    internal sealed class PlcTransferRequest
    {
        private readonly Func<byte[]> serialize;
        internal PlcTransferRequest(string address, PlcValueType type, int count, Func<byte[]> serialize = null, int textLength = 0)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("주소가 필요합니다.", nameof(address));
            if (count < 1 || count > int.MaxValue / Width(type))
                throw new ArgumentOutOfRangeException(nameof(count));
            Address = address;
            Type = type;
            Count = count;
            this.serialize = serialize;
            TextLength = textLength;
        }
        internal string Address { get; }
        internal PlcValueType Type { get; }
        internal int Count { get; }
        internal int TextLength { get; }
        internal int ByteCount => Count * Width(Type);
        internal bool IsWrite => serialize != null;
        internal bool IsBit => Type == PlcValueType.Boolean;
        internal byte[] Encode() => serialize == null ? null : serialize();
        internal string Unit { get; set; } = "byte";
        internal int Confirmed { get; private set; }
        internal int Pending { get; set; }
        internal void Confirm() { Confirmed += Pending; Pending = 0; }
        internal OperationFailureDetails Details(Exception cause = null, bool sent = true) =>
            new OperationFailureDetails(Address, IsWrite ? "Write" : "Read", Unit, Confirmed,
                IsWrite && sent ? Pending : 0, cause);
        internal OperationResult<byte[]> Failed(OperationResult status)
        {
            var result = status.ConvertFailed<byte[]>();
            result.FailureDetails = Details(status.FailureDetails?.Cause);
            return result;
        }
        internal static int Width(PlcValueType type)
        {
            switch (type)
            {
                case PlcValueType.Int16: case PlcValueType.UInt16: return 2;
                case PlcValueType.Int32: case PlcValueType.UInt32: case PlcValueType.Float: return 4;
                case PlcValueType.Int64: case PlcValueType.UInt64: case PlcValueType.Double: return 8;
                default: return 1;
            }
        }
        internal static T Require<T>(OperationResult<T> result)
        {
            if (!result.IsSuccess)
                throw new PlcProtocolException(result.ErrorCode, result.Message);
            return result.Content;
        }
    }

    internal sealed class PlcProtocolException : Exception
    {
        internal PlcProtocolException(int code, string message) : base(message) { Code = code; }
        internal int Code { get; }
    }
}
