using FieldLink.PlcDrivers.Common;

namespace FieldLink.PlcDrivers.Delta
{
    /// <summary>Delta 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class DeltaAddressParser
    {
        /// <summary>DVP 또는 AS의 주소를 Modbus 주소 표현으로 변환합니다.</summary>
        /// <param name = "series">대상 PLC 계열입니다.</param>
        /// <param name = "address">변환할 제조사 주소입니다.</param>
        /// <param name = "modbusCode">요청의 Modbus 기능 코드입니다.</param>
        /// <returns>변환된 주소 또는 지원하지 않는 계열 오류입니다.</returns>
        public static OperationResult<string> TranslateToModbusAddress(DeltaSeries series, string address, byte modbusCode)
        {
            switch (series)
            {
                case DeltaSeries.Dvp:
                    return DeltaDvpAddressParser.ParseDeltaDvpAddress(address, modbusCode);
                case DeltaSeries.AS:
                    return DeltaAsAddressParser.ParseDeltaASAddress(address, modbusCode);
                default:
                    return new OperationResult<string>(ProtocolMessages.NotSupportedDataType);
            }
        }
    }
}
