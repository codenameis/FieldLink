using FieldLink.PlcDrivers.Modbus.Clients;
using FieldLink.Communication;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Inovance;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Inovance.Clients
{
    /// <summary>AM·H3U·H5U 주소를 Modbus RTU로 교환합니다.</summary>
    public sealed class InovanceSerialClient : ModbusSerialClient
    {
        /// <summary>연결된 포트와 PLC 시리즈를 지정합니다.</summary>
        public InovanceSerialClient(ISerialClient transport, InovanceSeries series = InovanceSeries.AM, byte station = 1, TimeSpan? timeout = null)
            : base(transport, station: station, timeout: timeout, addressMapping: (address, function) => InovanceAddressParser.PraseInovanceAddress(series, address, function))
        {
            if (!Enum.IsDefined(typeof(InovanceSeries), series))
                throw new ArgumentOutOfRangeException(nameof(series));
            Series = series;
        }
        /// <summary>주소 변환에 사용하는 시리즈입니다.</summary>
        public InovanceSeries Series { get; }
        /// <summary>M·MB 바이트 주소를 MW 읽기로 변환하고 워드 내 해당 바이트를 반환합니다.</summary>
        public async Task<OperationResult<byte>> ReadByteAsync(string address, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            cancellationToken.ThrowIfCancellationRequested();
            string number = address.StartsWith("MB", StringComparison.OrdinalIgnoreCase) ? address.Substring(2) :
                address.StartsWith("M", StringComparison.OrdinalIgnoreCase) ? address.Substring(1) : null;
            if (number == null || !int.TryParse(number, out int offset) || offset < 0)
                return new OperationResult<byte>("M 또는 MB 바이트 주소가 필요합니다.");
            var read = await ReadAsync("MW" + offset / 2, 1, cancellationToken).ConfigureAwait(false);
            return read.IsSuccess ? OperationResult.CreateSuccessResult(read.Content[offset % 2 == 0 ? 1 : 0]) : read.ConvertFailed<byte>();
        }
    }
}
