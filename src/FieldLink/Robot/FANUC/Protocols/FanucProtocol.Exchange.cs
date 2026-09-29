using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Robot.FANUC.Protocols
{
    /// <summary>FANUC의 메모리 주소, 데이터 명령 및 기본 할당 명령을 변환합니다.</summary>
    public static partial class FanucProtocol
    {
        /// <summary>두 연결 요청과 60개 할당 명령을 반환합니다. 각 응답 수신이 성공한 뒤 다음 명령을 보내야 합니다.</summary>
        public static byte[][] BuildInitialization(int clientId = 1024)
        {
            var commands = new List<byte[]>();
            var connect = new byte[56];
            BitConverter.GetBytes(clientId).CopyTo(connect, 1);
            commands.Add(connect);
            commands.Add(new byte[56] { 8, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 192, 0, 0, 0, 0, 16, 14, 0, 0, 1, 1, 79, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
            foreach (string command in GetAssignmentCommands())
            {
                byte[] data = Encoding.ASCII.GetBytes(command);
                commands.Add(BuildWriteData(SELECTOR_G, 1, data, data.Length));
            }

            return commands.ToArray();
        }

        /// <summary>주소 영역을 검사하여 워드 또는 비트 읽기 명령을 구성합니다. 주소는 1부터 시작합니다.</summary>
        public static OperationResult<byte[]> BuildRead(string address, ushort length, bool isBit = false)
        {
            var parsed = ParseAddress(address, isBit);
            if (!parsed.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(parsed);
            if (!IsAllowed(parsed.Content1, isBit))
                return new OperationResult<byte[]>(ProtocolMessages.NotSupportedDataType + (isBit ? NotAllowedBool : NotAllowedWord));
            return OperationResult.CreateSuccessResult(isBit ? BuildReadBits(parsed.Content1, parsed.Content2, length) : BuildReadData(parsed.Content1, parsed.Content2, length));
        }

        /// <summary>워드 주소의 쓰기 명령입니다. 홀수 바이트 길이의 원본 처리도 유지합니다.</summary>
        public static OperationResult<byte[]> BuildWrite(string address, byte[] value)
        {
            var parsed = ParseAddress(address, false);
            if (!parsed.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(parsed);
            if (!IsAllowed(parsed.Content1, false))
                return new OperationResult<byte[]>(ProtocolMessages.NotSupportedDataType + NotAllowedWord);
            return OperationResult.CreateSuccessResult(BuildWriteData(parsed.Content1, parsed.Content2, value, value.Length / 2));
        }

        /// <summary>비트 주소의 쓰기 명령을 구성합니다.</summary>
        public static OperationResult<byte[]> BuildWrite(string address, bool[] value)
        {
            var parsed = ParseAddress(address, true);
            if (!parsed.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(parsed);
            if (!IsAllowed(parsed.Content1, true))
                return new OperationResult<byte[]>(ProtocolMessages.NotSupportedDataType + NotAllowedBool);
            return OperationResult.CreateSuccessResult(BuildWriteBits(parsed.Content1, parsed.Content2, value));
        }

        private static bool IsAllowed(byte selector, bool bits) => bits ? selector == SELECTOR_M || selector == SELECTOR_I || selector == SELECTOR_Q : selector == SELECTOR_D || selector == SELECTOR_AI || selector == SELECTOR_AQ;
        private static int BitStart(ushort address) => address - 1 - (address - 1) % 8 + 1;
        private static int BitByteLength(ushort address, int length)
        {
            int end = (address + length - 1) % 8 == 0 ? address + length - 1 : (address + length - 1) / 8 * 8 + 8;
            return (end - BitStart(address) + 1) / 8;
        }

        /// <summary>비트 길이를 바이트 경계로 확장합니다. 원본처럼 요청 시작 주소는 정렬하지 않습니다.</summary>
        public static byte[] BuildReadBits(byte selector, ushort address, ushort length) => BuildReadData(selector, address, (ushort)(BitByteLength(address, length) * 8));
        /// <summary>비트 오프셋을 반영한 쓰기 명령입니다. 요청 길이는 원래 접점 수입니다.</summary>
        public static byte[] BuildWriteBits(byte selector, ushort address, bool[] value)
        {
            var bits = new bool[BitByteLength(address, value.Length) * 8];
            Array.Copy(value, 0, bits, address - BitStart(address), value.Length);
            return BuildWriteData(selector, address, ProtocolBytes.BoolArrayToByte(bits), value.Length);
        }

        /// <summary>워드 응답입니다. 0x94는 56바이트 뒤, 0xD4는 44바이트부터 값을 읽습니다.</summary>
        public static OperationResult<byte[]> ParseReadResponse(byte[] response, ushort length)
        {
            if (response[31] == 0x94)
                return OperationResult.CreateSuccessResult(response.RemoveBegin(56));
            if (response[31] == 0xD4)
                return OperationResult.CreateSuccessResult(response.SelectMiddle(44, length * 2));
            return new OperationResult<byte[]>(response[31], "로봇 읽기 오류");
        }

        /// <summary>비트 응답에서 요청 주소의 오프셋과 접점 수만 추출합니다.</summary>
        public static OperationResult<bool[]> ParseBitResponse(byte[] response, ushort address, ushort length)
        {
            byte[] bytes;
            if (response[31] == 0x94)
                bytes = response.RemoveBegin(56);
            else if (response[31] == 0xD4)
                bytes = response.SelectMiddle(44, BitByteLength(address, length));
            else
                return new OperationResult<bool[]>(response[31], "로봇 비트 읽기 오류");
            bool[] result = new bool[length];
            Array.Copy(bytes.ToBoolArray(), address - BitStart(address), result, 0, length);
            return OperationResult.CreateSuccessResult(result);
        }

        /// <summary>쓰기 상태 0xD4를 검사합니다. 비트 쓰기 오류는 원본처럼 상세 코드 없이 반환합니다.</summary>
        public static OperationResult ParseWriteResponse(byte[] response, bool isBit = false)
        {
            if (response[31] == 0xD4)
                return OperationResult.CreateSuccessResult();
            return isBit ? new OperationResult() : new OperationResult(response[31], "로봇 쓰기 오류");
        }

        /// <summary>자세·구성·좌표계·공구의 순차 쓰기 프레임입니다. 각 쓰기가 성공한 뒤 다음 프레임을 전송해야 합니다.</summary>
        public static byte[][] BuildWriteXyzwpr(ushort address, float[] xyzwpr, short[] config, short userFrame, short userTool)
        {
            var transform = new ProtocolValueConverter();
            byte[] data = new byte[xyzwpr.Length * 4 + config.Length * 2 + 2];
            transform.GetBytes(xyzwpr).CopyTo(data, 0);
            transform.GetBytes(config).CopyTo(data, 36);
            var commands = new List<byte[]>
            {
                BuildWriteData(SELECTOR_D, address, data, data.Length / 2)
            };
            if (userFrame >= 0 && userFrame <= 15)
                AddPoseWrite(commands, (ushort)(address + 45), userTool >= 0 && userTool <= 15 ? new[] { userFrame, userTool } : new[] { userFrame });
            else if (userTool >= 0 && userTool <= 15)
                AddPoseWrite(commands, (ushort)(address + 46), new[] { userTool });
            return commands.ToArray();
        }

        /// <summary>관절 좌표의 순차 쓰기 프레임입니다. 유효한 좌표계·공구만 각각의 위치에 기록합니다.</summary>
        public static byte[][] BuildWriteJoint(ushort address, float[] joint, short userFrame, short userTool)
        {
            byte[] data = new ProtocolValueConverter().GetBytes(joint);
            var commands = new List<byte[]>
            {
                BuildWriteData(SELECTOR_D, (ushort)(address + 26), data, data.Length / 2)
            };
            if (userFrame >= 0 && userFrame <= 15)
                AddPoseWrite(commands, (ushort)(address + 44), userTool >= 0 && userTool <= 15 ? new short[] { 0, userFrame, userTool } : new short[] { 0, userFrame });
            else
            {
                AddPoseWrite(commands, (ushort)(address + 44), new short[] { 0 });
                // 이전 구현: AddPoseWrite(commands, (ushort)(address + 44), new short[] { 0, userTool });
                // R-007: +45는 UF다. UT만 유효하면 +46에 한 워드만 쓰고 기존 UF를 보존한다.
                if (userTool >= 0 && userTool <= 15)
                    AddPoseWrite(commands, (ushort)(address + 46), new short[] { userTool });
            }

            return commands.ToArray();
        }

        private static void AddPoseWrite(List<byte[]> commands, ushort address, short[] values) => commands.Add(BuildWriteData(SELECTOR_D, address, new ProtocolValueConverter().GetBytes(values), values.Length));
    }
}
