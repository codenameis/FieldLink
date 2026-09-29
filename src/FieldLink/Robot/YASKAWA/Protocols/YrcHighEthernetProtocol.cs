using FieldLink.PlcDrivers.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FieldLink.Robot.YASKAWA.Protocols
{
    /// <summary>YRC 고속 이더넷의 32바이트 헤더와 상태 코드를 변환합니다.</summary>
    public static class YrcHighEthernetProtocol
    {
        /// <summary>처리 구분·요청 ID·명령·주소·속성·서비스·본문으로 프레임을 구성합니다.</summary>
        public static byte[] BuildCommand(byte handle, byte requestID, ushort command, ushort dataAddress, byte dataAttribute, byte dataHandle, byte[] dataPart)
        {
            if (dataPart != null && dataPart.Length > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(dataPart), "YRC 본문 길이는 16비트 길이 필드 범위를 초과할 수 없습니다.");
            MemoryStream ms = new MemoryStream();
            ms.Write(Encoding.ASCII.GetBytes("YERC")); // 식별자
            ms.Write(new byte[] { 0x20, 0x00, 0x00, 0x00 }); // 데이터부 크기
            ms.Write(new byte[] { 0x03, handle, 0x00, requestID }); // 예약1/분할 처리
            ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // 데이터 블록 번호
            ms.Write(Encoding.ASCII.GetBytes("99999999")); // 약속 2
            ms.Write(BitConverter.GetBytes(command)); // 명령 번호
            ms.Write(BitConverter.GetBytes(dataAddress)); // 데이터 배열 번호
            ms.Write(new byte[] { dataAttribute, dataHandle, 0x00, 0x00 }); // 단위 번호, 처리 특성, PADDING
            if (dataPart != null)
                ms.Write(dataPart); // 데이터부
            byte[] buffer = ms.ToArray();
            buffer[6] = BitConverter.GetBytes(buffer.Length - 32)[0];
            buffer[7] = BitConverter.GetBytes(buffer.Length - 32)[1];
            return buffer;
        }

        /// <summary>로봇 제어 요청에 대한 공통 헤더·길이·ID·응답 서비스를 검사합니다. 명령별 본문 해석보다 먼저 호출합니다.</summary>
        public static OperationResult ValidateResponse(byte[] request, byte[] response)
        {
            if (request == null || request.Length < 32)
                throw new ArgumentException("YRC 요청 헤더가 필요합니다.", nameof(request));
            if (response == null || response.Length < 32)
                return new OperationResult("YRC 응답 헤더가 부족합니다.");
            // R-018: 원본은 상태 코드만 검사했다. 정상 상태라도 다른 형식·요청의 응답이면 수용하지 않는다.
            if (response[0] != 'Y' || response[1] != 'E' || response[2] != 'R' || response[3] != 'C' ||
                response[4] != 32 || response[5] != 0)
                return new OperationResult("YRC 식별자 또는 헤더 길이가 올바르지 않습니다.");
            if (response.Length - 32 != response[6] + response[7] * 256)
                return new OperationResult("YRC 선언 본문 길이가 실제 응답과 다릅니다.");
            if (response[8] != 3 || response[9] != request[9] || response[10] != 1 || response[11] != request[11])
                return new OperationResult("YRC 응답 구분 또는 요청 ID가 일치하지 않습니다.");
            for (int i = 12; i < 16; i++)
                if (response[i] != 0)
                    return new OperationResult("로봇 제어 응답의 블록 번호는 0이어야 합니다.");
            for (int i = 16; i < 24; i++)
                if (response[i] != '9')
                    return new OperationResult("YRC 예약 영역이 올바르지 않습니다.");
            if (response[24] != (byte)(request[29] | 0x80))
                return new OperationResult("YRC 응답 서비스가 요청과 일치하지 않습니다.");
            return OperationResult.CreateSuccessResult();
        }

        /// <summary>상태와 추가 상태 코드를 검사합니다. 전체 요청 대응은 ValidateResponse로 별도 검사합니다.</summary>
        public static OperationResult CheckResponseContent(byte[] response)
        {
            if (response == null || response.Length < 32)
                return new OperationResult("YRC 응답 헤더가 부족합니다.");
            if (response[25] != 0x00)
            {
                byte status = response[25];
                int affix = 0;
                if (status == 0x1f) // 추가된 상태의 변수를 확인합니다.
                {
                    if (response[26] == 0x01)
                        affix = response[28];
                    else
                        affix = BitConverter.ToUInt16(response, 28);
                }

                return new OperationResult(status, GetErrorText(status, affix));
            }

            return OperationResult.CreateSuccessResult();
        }

        /// <summary>상태 코드와 추가 상태 코드의 한국어 설명을 반환합니다.</summary>
        public static string GetErrorText(byte status, int affix)
        {
            switch (status)
            {
                case 0x08:
                    return "요청한 명령이 정의되지 않았습니다.";
                case 0x09:
                    return "유효하지 않은 데이터 단위 번호입니다.";
                case 0x28:
                    return "지정한 데이터 배열 번호에 해당 명령이 없습니다.";
                case 0x1f:
                {
                    switch (affix)
                    {
                        case 0x1010:
                            return "명령 오류";
                        case 0x1011:
                            return "명령 피연산자 오류";
                        case 0x1012:
                            return "명령 값이 범위를 벗어났습니다.";
                        case 0x1013:
                            return "명령 길이 오류";
                        case 0x1020:
                            return "장치의 파일 수가 너무 많습니다.";
                        case 0x2010:
                            return "로봇 동작 중";
                        case 0x2020:
                            return "티치 펜던트 HOLD 정지 중";
                        case 0x2030:
                            return "플레이백 패널 HOLD 정지 중";
                        case 0x2040:
                            return "외부 HOLD 중";
                        case 0x2050:
                            return "명령 HOLD 중";
                        case 0x2060:
                            return "오류 알람 발생 중";
                        case 0x2070:
                            return "서보 OFF 상태";
                        case 0x2080:
                            return "모드가 다릅니다.";
                        case 0x2090:
                            return "다른 기능에서 파일에 접근 중입니다.";
                        case 0x2100:
                            return "명령 모드가 설정되지 않았습니다.";
                        case 0x2110:
                            return "데이터에 접근할 수 없습니다.";
                        case 0x2120:
                            return "데이터를 읽을 수 없습니다.";
                        case 0x2130:
                            return "편집 중";
                        case 0x2150:
                            return "좌표 변환 기능 실행 중";
                        case 0x3010:
                            return "서보 전원을 켜십시오.";
                        case 0x3040:
                            return "원점 위치를 확인하십시오.";
                        case 0x3050:
                            return "위치를 확인하십시오.";
                        case 0x3070:
                            return "현재 값을 생성할 수 없습니다.";
                        case 0x3220:
                            return "패널 잠금 모드 또는 사이클 금지 신호를 받았습니다.";
                        case 0x3230:
                            return "패널 잠금 시작 금지 신호를 받았습니다.";
                        case 0x3350:
                            return "사용자 좌표가 티칭되지 않았습니다.";
                        case 0x3360:
                            return "사용자 좌표 파일이 손상되었습니다.";
                        case 0x3370:
                            return "제어 축 그룹이 다릅니다.";
                        case 0x3380:
                            return "베이스 축 데이터가 다릅니다.";
                        case 0x3390:
                            return "상대 JOB을 변환할 수 없습니다.";
                        case 0x3400:
                            return "주 프로그램 호출 금지: 매개변수";
                        case 0x3410:
                            return "주 프로그램 호출 금지: 동작 표시등 켜짐";
                        case 0x3420:
                            return "주 프로그램 호출 금지: 티칭 잠금";
                        case 0x3430:
                            return "로봇 간 보정이 정의되지 않았습니다.";
                        case 0x3450:
                            return "서보 전원을 켤 수 없습니다.";
                        case 0x3460:
                            return "좌표계를 설정할 수 없습니다.";
                        case 0x4010:
                            return "프로그램 등록 메모리가 부족합니다.";
                        case 0x4012:
                            return "포지셔너 데이터 등록 메모리가 부족합니다.";
                        case 0x4020:
                            return "프로그램 편집이 금지되어 있습니다.";
                        case 0x4030:
                            return "같은 이름의 프로그램이 있습니다.";
                        case 0x4040:
                            return "지정한 프로그램이 없습니다.";
                        case 0x4060:
                            return "실행할 프로그램을 설정하십시오.";
                        case 0x4120:
                            return "위치 데이터가 손상되었습니다.";
                        case 0x4130:
                            return "위치 데이터가 없습니다.";
                        case 0x4140:
                            return "위치 변수 형식이 다릅니다.";
                        case 0x4150:
                            return "주 프로그램이 아닌 프로그램의 END 명령입니다.";
                        case 0x4170:
                            return "명령 데이터가 손상되었습니다.";
                        case 0x4190:
                            return "프로그램 이름에 부적절한 문자가 있습니다.";
                        case 0x4200:
                            return "레이블 이름에 부적절한 문자가 있습니다.";
                        case 0x4230:
                            return "이 시스템에서 사용할 수 없는 명령이 있습니다.";
                        case 0x4420:
                            return "변환할 프로그램에 스텝이 없습니다.";
                        case 0x4430:
                            return "프로그램이 이미 모두 변환되었습니다.";
                        case 0x4480:
                            return "사용자 좌표를 티칭하십시오.";
                        case 0x4490:
                            return "상대 JOB 또는 독립 제어 기능이 허용되지 않았습니다.";
                        case 0x5110:
                            return "명령 구문 오류";
                        case 0x5120:
                            return "포지셔너 데이터 오류";
                        case 0x5130:
                            return "NOP 또는 END 명령이 없습니다.";
                        case 0x5170:
                            return "작성 형식 오류";
                        case 0x5180:
                            return "데이터 개수가 올바르지 않습니다.";
                        case 0x5200:
                            return "데이터 범위를 벗어났습니다.";
                        case 0x5310:
                            return "명령 이외의 구문 오류";
                        case 0x5340:
                            return "아날로그 명령 지정 오류";
                        case 0x5370:
                            return "조건 데이터 기록 오류";
                        case 0x5390:
                            return "프로그램 데이터 기록 오류";
                        case 0x5430:
                            return "시스템 데이터가 일치하지 않습니다.";
                        case 0x5480:
                            return "용접기 형식이 일치하지 않습니다.";
                        case 0x6010:
                            return "로봇 또는 지그 축 동작 중";
                        case 0x6020:
                            return "지정한 장치의 용량이 부족합니다.";
                        case 0x6030:
                            return "지정한 장치에 접근할 수 없습니다.";
                        case 0x6040:
                            return "예상하지 못한 자동 백업 요청";
                        case 0x6050:
                            return "CMOS 크기가 RAM 영역을 벗어났습니다.";
                        case 0x6060:
                            return "전원 투입 시 메모리를 확보할 수 없습니다.";
                        case 0x6070:
                            return "백업 파일 정보 접근 오류";
                        case 0x6080:
                            return "백업 파일 정리 중 삭제 실패";
                        case 0x6090:
                            return "백업 파일 정리 중 이름 변경 실패";
                        case 0x6100:
                            return "드라이브 이름이 규정 길이를 초과했습니다.";
                        case 0x6110:
                            return "장치가 다릅니다.";
                        case 0x6120:
                            return "시스템 오류";
                        case 0x6130:
                            return "자동 백업을 설정할 수 없습니다.";
                        case 0x6140:
                            return "자동 백업 중에는 수동 백업할 수 없습니다.";
                        case 0xA000:
                            return "정의되지 않은 명령";
                        case 0xA001:
                            return "인스턴스 번호 오류";
                        case 0xA002:
                            return "속성 번호 오류";
                        case 0xA100:
                            return "응답 데이터 크기가 하드웨어 제한을 벗어났습니다.";
                        case 0xA101:
                        case 0xA102:
                            return "응답 데이터 크기가 소프트웨어 제한을 벗어났습니다.";
                        case 0xB001:
                            return "정의되지 않은 위치 변수";
                        case 0xB002:
                            return "사용이 금지된 데이터";
                        case 0xB003:
                            return "요청 데이터 크기 오류";
                        case 0xB004:
                            return "데이터 범위 초과";
                        case 0xB005:
                            return "데이터가 설정되지 않았습니다.";
                        case 0xB006:
                            return "지정한 용도가 등록되지 않았습니다.";
                        case 0xB007:
                            return "지정한 기종이 등록되지 않았습니다.";
                        case 0xB008:
                            return "제어 축 그룹 설정 오류";
                        case 0xB009:
                            return "속도 설정 오류";
                        case 0xB00A:
                            return "동작 속도가 설정되지 않았습니다.";
                        case 0xB00B:
                            return "동작 좌표계 설정 오류";
                        case 0xB00C:
                            return "자세 구성 설정 오류";
                        case 0xB00D:
                            return "공구 번호 설정 오류";
                        case 0xB00E:
                            return "사용자 번호 설정 오류";
                        default:
                            return ProtocolMessages.UnknownError;
                    }
                }

                default:
                    return ProtocolMessages.UnknownError;
            }
        }
    }
}
