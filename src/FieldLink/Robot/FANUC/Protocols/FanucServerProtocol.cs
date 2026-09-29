using System;
using System.Linq;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Robot.FANUC.Protocols
{
    /// <summary>FANUC 서버가 수신한 요청의 해석과 고정 승인 응답 구성입니다. 메모리 저장소는 포함하지 않습니다.</summary>
    public static class FanucServerProtocol
    {
        /// <summary>최초 연결 요청에 대한 56바이트 응답입니다.</summary>
        public static byte[] BuildConnectReply()
        {
            byte[] result = new byte[56];
            result[0] = 1;
            result[8] = 1;
            return result;
        }

        /// <summary>두 번째 세션 요청에 대한 승인 응답입니다.</summary>
        public static byte[] BuildSessionReply() => ProtocolBytes.HexStringToBytes("03 00 01 00 00 00 00 00 00 01 00 00 00 00 00 00 00 01 00 00 00 00 00 00 00 00 00 00 00 00 01 d4 10 0e 00 00 30 3a 00 00 01 01 00 00 00 00 00 00 01 01 ff 02 00 00 7c 21");
        /// <summary>G 영역 명령 또는 일반 쓰기의 승인 응답입니다. 원본의 두 응답 템플릿을 유지합니다.</summary>
        public static byte[] BuildWriteReply(byte function, bool isCommandArea = false)
        {
            byte[] result = ProtocolBytes.HexStringToBytes("03 00 09 00 00 00 00 00 00 01 00 00 00 00 00 00 00 01 00 00 00 00 00 00 00 00 00 00 00 00 09 d4 10 0e 00 00 30 3a 00 00 01 01 00 00 00 00 00 00 01 01 ff 04 00 00 7c 21");
            result[2] = function;
            if (isCommandArea)
                result[30] = 8;
            return result;
        }

        /// <summary>완전한 요청에서 기능·영역·0 기반 주소·길이·쓰기 값을 분리합니다. 원본처럼 프레임 길이의 사전 검증은 없습니다.</summary>
        public static OperationResult<FanucServerRequest> ParseRequest(byte[] packet)
        {
            byte function = packet[2];
            if (function != 6 && function != 8 && function != 9)
                return new OperationResult<FanucServerRequest>("지원하지 않는 기능: " + function);
            int offset = function == 9 ? 52 : 44;
            byte selector = packet[offset - 1];
            ushort address = BitConverter.ToUInt16(packet, offset);
            ushort length = BitConverter.ToUInt16(packet, offset + 2);
            bool words = selector == FanucProtocol.SELECTOR_D || selector == FanucProtocol.SELECTOR_AI || selector == FanucProtocol.SELECTOR_AQ;
            byte[] data = new byte[0];
            if (function != 6 && selector != FanucProtocol.SELECTOR_G)
            {
                int index = function == 8 ? 48 : 56;
                data = words ? packet.SelectMiddle(index, length * 2) : packet.RemoveBegin(index).ToBoolArray().SelectMiddle(address % 8, length).Select(bit => bit ? (byte)1 : (byte)0).ToArray();
            }

            return OperationResult.CreateSuccessResult(new FanucServerRequest(function, selector, address, length, words, data));
        }
    }

    /// <summary>FANUC 메모리 요청의 해석 결과입니다.</summary>
    public sealed class FanucServerRequest
    {
        internal FanucServerRequest(byte function, byte selector, ushort address, ushort length, bool words, byte[] data)
        {
            Function = function;
            Selector = selector;
            Address = address;
            Length = length;
            IsWord = words;
            Data = data;
        }

        /// <summary>6: 읽기, 8: 짧은 쓰기, 9: 긴 쓰기입니다.</summary>
        public byte Function { get; }
        /// <summary>데이터 영역입니다. G 영역은 별도의 명령 처리 대상입니다.</summary>
        public byte Selector { get; }
        /// <summary>전송 프레임의 0 기반 주소입니다.</summary>
        public ushort Address { get; }
        /// <summary>읽거나 쓸 워드 또는 비트 수입니다.</summary>
        public ushort Length { get; }
        /// <summary>D·AI·AQ 워드 영역이면 true입니다.</summary>
        public bool IsWord { get; }
        /// <summary>쓰기 데이터입니다. 워드는 원시 바이트, 비트는 접점마다 0 또는 1 한 바이트입니다. 읽기와 G 영역은 비어 있습니다.</summary>
        public byte[] Data { get; }
    }
}
