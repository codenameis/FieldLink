using System;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Robot.YASKAWA.Protocols
{
    /// <summary>YRC 고속 이더넷의 명령별 요청과 본문 파서를 구성합니다.</summary>
    public sealed partial class YrcEthernetRequestBuilder
    {
        /// <summary>사용자 정의 로봇 제어 명령을 구성합니다. 파일 제어 프레임은 YrcHighEthernetProtocol을 사용합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildCommand(ushort command, ushort address, byte attribute, byte service, byte[] data) => CreateRobotControlRequest(command, address, attribute, service, data);
        // 이전 이름: ReadCommand. 읽기를 실행하지 않고 읽기·쓰기·제어 요청을 구성하므로 책임을 명확히 한다.
        // Handling division 1(로봇 제어)과 공개 명령 API 및 생성 바이트는 그대로 유지한다.
        private YrcEthernetRequest<byte[]> CreateRobotControlRequest(ushort command, ushort address, byte attribute, byte service, byte[] data) => new YrcEthernetRequest<byte[]>(YrcHighEthernetProtocol.BuildCommand(1, 0, command, address, attribute, service, data), ParsePayload);
        private static OperationResult<byte[]> ParsePayload(byte[] response)
        {
            var check = YrcHighEthernetProtocol.CheckResponseContent(response);
            if (!check.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(check);
            return OperationResult.CreateSuccessResult(response.Length > 32 ? response.RemoveBegin(32) : new byte[0]);
        }

        private static YrcEthernetRequest<T> Map<T>(YrcEthernetRequest<byte[]> request, Func<byte[], T> convert) => request.Then(result =>
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<T>(result);
            // 원본의 변환 보조 메서드가 사용된 경로만 예외를 실패 결과로 바꾼다.
            try
            {
                return OperationResult.CreateSuccessResult(convert(result.Content));
            }
            catch (Exception error)
            {
                return new OperationResult<T>("데이터 변환 오류: " + error.Message);
            }
        });
        private static YrcEthernetRequest<T> MapUnchecked<T>(YrcEthernetRequest<byte[]> request, Func<byte[], T> convert) => request.Then(result => result.IsSuccess ? OperationResult.CreateSuccessResult(convert(result.Content)) : OperationResult.CreateFailedResult<T>(result));
        private static YrcEthernetRequest<byte> First(YrcEthernetRequest<byte[]> request) => Map(request, data => data[0]);
        /// <summary>최근 알람 4건의 요청을 순서대로 반환합니다. 각 응답이 성공한 뒤 다음 요청을 전송해야 합니다.</summary>
        public YrcEthernetRequest<YrcAlarm>[] BuildReadAlarms()
        {
            var requests = new YrcEthernetRequest<YrcAlarm>[4];
            for (int i = 0; i < requests.Length; i++)
                requests[i] = MapUnchecked(CreateRobotControlRequest(0x70, (ushort)(i + 1), 0, 1, null), data => data.Length == 0 ? null : new YrcAlarm(byteTransform, data, encoding));
            return requests;
        }

        /// <summary>이력 알람 요청입니다. 원본처럼 같은 alarmType을 length번 조회합니다. 인덱스를 증가시키지 않습니다.</summary>
        public YrcEthernetRequest<YrcAlarm>[] BuildReadHistoryAlarms(ushort alarmType, short length)
        {
            var requests = new YrcEthernetRequest<YrcAlarm>[length];
            for (int i = 0; i < requests.Length; i++)
                requests[i] = MapUnchecked(CreateRobotControlRequest(0x71, alarmType, 0, 1, null), data => data.Length == 0 ? null : new YrcAlarm(byteTransform, data, encoding));
            return requests;
        }
    }
}
