using FieldLink.PlcDrivers.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FieldLink.Robot.YASKAWA.Protocols
{
    /// <summary>YRC TCP 오류 문자열의 코드 설명을 해석합니다.</summary>
    public static class YrcErrorParser
    {
        /// <summary>십진 오류 번호에 대응하는 한국어 설명을 반환합니다.</summary>
        public static string GetErrorMessage(int err)
        {
            switch (err)
            {
                case 1010:
                    return "명령 오류";
                case 1011:
                    return "명령 피연산자 오류";
                case 1012:
                    return "명령 값이 범위를 벗어났습니다.";
                case 1013:
                    return "명령 길이 오류";
                case 1020:
                    return "장치의 파일 수가 너무 많습니다.";
                case 2010:
                    return "로봇 동작 중";
                case 2020:
                    return "티치 펜던트 HOLD 정지 중";
                case 2030:
                    return "플레이백 패널 HOLD 정지 중";
                case 2040:
                    return "외부 HOLD 중";
                case 2050:
                    return "명령 HOLD 중";
                case 2060:
                    return "오류 알람 발생 중";
                case 2070:
                    return "서보 ON 상태";
                case 2080:
                    return "모드가 다릅니다.";
                case 2090:
                    return "다른 기능에서 파일에 접근 중입니다.";
                case 2100:
                    return "원격 명령이 설정되지 않았습니다.";
                case 2110:
                    return "데이터에 접근할 수 없습니다.";
                case 2120:
                    return "데이터를 읽을 수 없습니다.";
                case 2130:
                    return "편집 중";
                case 2150:
                    return "좌표 변환 기능 실행 중";
                case 3010:
                    return "서보 전원을 켜십시오.";
                case 3040:
                    return "원점 위치를 확인하십시오.";
                case 3050:
                    return "위치를 확인하십시오.";
                case 3070:
                    return "현재 값을 생성할 수 없습니다.";
                case 3220:
                    return "패널 잠금 모드 또는 사이클 금지 신호를 받았습니다.";
                case 3230:
                    return "패널 잠금 시작 금지 신호를 받았습니다.";
                case 3350:
                    return "사용자 좌표를 티칭할 수 없습니다.";
                case 3360:
                    return "사용자 좌표 파일이 손상되었습니다.";
                case 3370:
                    return "제어 축 그룹이 다릅니다.";
                case 3380:
                    return "베이스 축 데이터가 다릅니다.";
                case 3390:
                    return "상대 JOB을 변환할 수 없습니다.";
                case 3400:
                    return "주 프로그램 호출 금지: 매개변수";
                case 3410:
                    return "주 프로그램 호출 금지: 동작 표시등 켜짐";
                case 3420:
                    return "주 프로그램 호출 금지: 티칭 잠금";
                case 3430:
                    return "로봇 간 보정이 정의되지 않았습니다.";
                case 3450:
                    return "서보 전원을 켤 수 없습니다.";
                case 3460:
                    return "좌표계를 설정할 수 없습니다.";
                case 4010:
                    return "프로그램 등록 메모리가 부족합니다.";
                case 4012:
                    return "포지셔너 데이터 등록 메모리가 부족합니다.";
                case 4020:
                    return "프로그램 편집이 금지되어 있습니다.";
                case 4030:
                    return "같은 이름의 프로그램이 있습니다.";
                case 4040:
                    return "지정한 프로그램이 없습니다.";
                case 4060:
                    return "실행할 프로그램을 설정하십시오.";
                case 4120:
                    return "위치 데이터가 손상되었습니다.";
                case 4130:
                    return "위치 데이터가 없습니다.";
                case 4140:
                    return "위치 변수 형식이 다릅니다.";
                case 4150:
                    return "주 프로그램이 아닌 프로그램의 END 명령입니다.";
                case 4170:
                    return "명령 데이터가 손상되었습니다.";
                case 4190:
                    return "프로그램 이름에 부적절한 문자가 있습니다.";
                case 4200:
                    return "레이블 이름에 부적절한 문자가 있습니다.";
                case 4230:
                    return "이 시스템에서 사용할 수 없는 명령이 있습니다.";
                case 4420:
                    return "변환할 프로그램에 스텝이 있습니다.";
                case 4430:
                    return "프로그램이 이미 모두 변환되었습니다.";
                case 4480:
                    return "사용자 좌표를 티칭하십시오.";
                case 4490:
                    return "상대 JOB 또는 독립 제어 기능이 허용되지 않았습니다.";
                case 5110:
                    return "명령 구문 오류";
                case 5120:
                    return "포지셔너 데이터 오류";
                case 5130:
                    return "NOP 또는 END 명령이 없습니다.";
                case 5170:
                    return "작성 형식 오류";
                case 5180:
                    return "데이터 개수가 올바르지 않습니다.";
                case 5200:
                    return "데이터 범위를 벗어났습니다.";
                case 5310:
                    return "명령 이외의 구문 오류";
                case 5340:
                    return "아날로그 명령 지정 오류";
                case 5370:
                    return "조건 데이터 기록 오류";
                case 5390:
                    return "프로그램 데이터 기록 오류";
                case 5430:
                    return "시스템 데이터가 일치하지 않습니다.";
                case 5480:
                    return "용접기 형식이 일치하지 않습니다.";
                default:
                    return ProtocolMessages.UnknownError;
            }
        }

        /// <summary>문자열 끝의 괄호 오류 번호를 찾아 설명을 추가합니다. 원본 정규식과 기본 실패 코드를 유지합니다.</summary>
        public static OperationResult<string> ExtraErrorMessage(string errText)
        {
            // 문자열 끝의 괄호 오류 번호를 찾는다. 
            Match match = Regex.Match(errText, @"\([0-9]+\)\.$");
            if (match.Success)
            {
                string err = match.Value.Substring(1, match.Value.Length - 3);
                if (int.TryParse(err, out int errCode))
                {
                    return new OperationResult<string>(errText + Environment.NewLine + YrcErrorParser.GetErrorMessage(errCode));
                }
                else
                {
                    return new OperationResult<string>(errText);
                }
            }
            else
            {
                return new OperationResult<string>(errText);
            }
        }
    }
}
