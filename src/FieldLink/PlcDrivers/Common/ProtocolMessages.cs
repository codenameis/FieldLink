using System;

namespace FieldLink.PlcDrivers.Common
{
    /// <summary>ProtocolMessages 프로토콜 값입니다.</summary>
    internal static class ProtocolMessages
    {
        /// <summary>AddressFormatWrong 프로토콜 값입니다.</summary>
        internal static string AddressFormatWrong => "주소를 해석하는 중 오류가 발생했습니다: ";
        /// <summary>AllenBradley04 프로토콜 값입니다.</summary>
        internal static string AllenBradley04 => "IOI 형식이 올바르지 않거나 일치하는 태그가 없습니다.";
        /// <summary>AllenBradley05 프로토콜 값입니다.</summary>
        internal static string AllenBradley05 => "참조한 항목 또는 인스턴스를 찾을 수 없습니다.";
        /// <summary>AllenBradley06 프로토콜 값입니다.</summary>
        internal static string AllenBradley06 => "요청 데이터가 응답 버퍼의 용량을 초과하여 일부만 전송되었습니다.";
        /// <summary>AllenBradley0A 프로토콜 값입니다.</summary>
        internal static string AllenBradley0A => "속성을 처리하는 중 오류가 발생했습니다.";
        /// <summary>AllenBradley0C 프로토콜 값입니다.</summary>
        internal static string AllenBradley0C => "읽기 또는 쓰기 작업 중 오류가 발생했습니다. 프로그램을 불러오는 중에도 발생할 수 있습니다.";
        /// <summary>AllenBradley13 프로토콜 값입니다.</summary>
        internal static string AllenBradley13 => "서비스 실행에 필요한 명령 데이터 또는 매개변수가 부족합니다.";
        /// <summary>AllenBradley1C 프로토콜 값입니다.</summary>
        internal static string AllenBradley1C => "제공된 속성 수가 지정한 속성 수보다 적습니다.";
        /// <summary>AllenBradley1E 프로토콜 값입니다.</summary>
        internal static string AllenBradley1E => "여러 서비스 중 하나의 요청이 실패했습니다.";
        /// <summary>AllenBradley20 프로토콜 값입니다.</summary>
        internal static string AllenBradley20 => "명령의 매개변수 데이터 타입이 실제 매개변수와 일치하지 않습니다.";
        /// <summary>AllenBradley26 프로토콜 값입니다.</summary>
        internal static string AllenBradley26 => "IOI의 워드 길이가 처리한 IOI 길이와 일치하지 않습니다.";
        /// <summary>AllenBradleySessionStatus01 프로토콜 값입니다.</summary>
        internal static string AllenBradleySessionStatus01 => "유효하지 않거나 지원하지 않는 Encapsulation 명령입니다.";
        /// <summary>AllenBradleySessionStatus02 프로토콜 값입니다.</summary>
        internal static string AllenBradleySessionStatus02 => "Encapsulation 명령을 처리할 수신 메모리가 부족합니다.";
        /// <summary>AllenBradleySessionStatus03 프로토콜 값입니다.</summary>
        internal static string AllenBradleySessionStatus03 => "Encapsulation 메시지의 데이터 형식 또는 값이 올바르지 않습니다.";
        /// <summary>AllenBradleySessionStatus64 프로토콜 값입니다.</summary>
        internal static string AllenBradleySessionStatus64 => "Encapsulation 메시지의 세션 핸들이 유효하지 않습니다.";
        /// <summary>AllenBradleySessionStatus65 프로토콜 값입니다.</summary>
        internal static string AllenBradleySessionStatus65 => "메시지 길이가 유효하지 않습니다.";
        /// <summary>AllenBradleySessionStatus69 프로토콜 값입니다.</summary>
        internal static string AllenBradleySessionStatus69 => "지원하지 않는 Encapsulation 프로토콜 버전입니다.";
        /// <summary>DataTransformError 프로토콜 값입니다.</summary>
        internal static string DataTransformError => "데이터 변환에 실패했습니다. 원본 데이터: ";
        /// <summary>장치 또는 파서가 반환한 오류 코드입니다.</summary>
        internal static string ErrorCode => "오류 코드: ";
        /// <summary>FatekStatus02 프로토콜 값입니다.</summary>
        internal static string FatekStatus02 => "유효하지 않은 값입니다.";
        /// <summary>FatekStatus03 프로토콜 값입니다.</summary>
        internal static string FatekStatus03 => "쓰기가 금지되어 있습니다.";
        /// <summary>FatekStatus04 프로토콜 값입니다.</summary>
        internal static string FatekStatus04 => "유효하지 않은 명령 코드입니다.";
        /// <summary>FatekStatus05 프로토콜 값입니다.</summary>
        internal static string FatekStatus05 => "래더 체크섬이 일치하지 않아 RUN 명령을 실행할 수 없습니다.";
        /// <summary>FatekStatus06 프로토콜 값입니다.</summary>
        internal static string FatekStatus06 => "PLC ID와 래더 ID가 일치하지 않아 RUN 명령을 실행할 수 없습니다.";
        /// <summary>FatekStatus07 프로토콜 값입니다.</summary>
        internal static string FatekStatus07 => "프로그램 구문 오류로 RUN 명령을 실행할 수 없습니다.";
        /// <summary>FatekStatus09 프로토콜 값입니다.</summary>
        internal static string FatekStatus09 => "PLC가 실행할 수 없는 래더 명령이 있어 RUN 명령을 실행할 수 없습니다.";
        /// <summary>FatekStatus10 프로토콜 값입니다.</summary>
        internal static string FatekStatus10 => "유효하지 않은 주소입니다.";
        /// <summary>FujiSpbStatus01 프로토콜 값입니다.</summary>
        internal static string FujiSpbStatus01 => "ROM에 쓰기를 요청했습니다.";
        /// <summary>FujiSpbStatus02 프로토콜 값입니다.</summary>
        internal static string FujiSpbStatus02 => "정의되지 않았거나 처리할 수 없는 명령을 수신했습니다.";
        /// <summary>FujiSpbStatus03 프로토콜 값입니다.</summary>
        internal static string FujiSpbStatus03 => "데이터 영역의 매개변수가 올바르지 않습니다.";
        /// <summary>FujiSpbStatus04 프로토콜 값입니다.</summary>
        internal static string FujiSpbStatus04 => "다른 프로그래머의 전송 인터록으로 처리할 수 없습니다.";
        /// <summary>FujiSpbStatus05 프로토콜 값입니다.</summary>
        internal static string FujiSpbStatus05 => "모듈 번호가 올바르지 않습니다.";
        /// <summary>FujiSpbStatus06 프로토콜 값입니다.</summary>
        internal static string FujiSpbStatus06 => "검색 항목을 찾을 수 없습니다.";
        /// <summary>FujiSpbStatus07 프로토콜 값입니다.</summary>
        internal static string FujiSpbStatus07 => "쓰기 주소가 모듈 범위를 초과합니다.";
        /// <summary>FujiSpbStatus09 프로토콜 값입니다.</summary>
        internal static string FujiSpbStatus09 => "프로그램 오류로 RUN 명령을 실행할 수 없습니다.";
        /// <summary>FujiSpbStatus0C 프로토콜 값입니다.</summary>
        internal static string FujiSpbStatus0C => "암호가 일치하지 않습니다.";
        /// <summary>GeSRTPAddressCannotBeZero 프로토콜 값입니다.</summary>
        internal static string GeSRTPAddressCannotBeZero => "시작 주소는 0이 될 수 없습니다. 주소는 1부터 시작합니다.";
        /// <summary>GeSRTPNotSupportBitReadWrite 프로토콜 값입니다.</summary>
        internal static string GeSRTPNotSupportBitReadWrite => "해당 주소 영역은 비트 단위 읽기와 쓰기를 지원하지 않습니다.";
        /// <summary>KeyenceNanoE0 프로토콜 값입니다.</summary>
        internal static string KeyenceNanoE0 => "1. 장치 번호, 뱅크 번호, 유닛 번호 또는 주소가 범위를 초과합니다.\r\n2. 프로그램에서 사용하지 않는 타이머, 카운터, CTH 또는 CTC 번호입니다.\r\n3. 모니터를 등록하지 않고 모니터 읽기를 요청했습니다.";
        /// <summary>KeyenceNanoE1 프로토콜 값입니다.</summary>
        internal static string KeyenceNanoE1 => "1. CPU 유닛이 지원하지 않는 명령입니다.\r\n2. 명령 지정 방법이 올바르지 않습니다.\r\n3. 통신을 수립하기 전에 CR 이외의 명령을 전송했습니다.";
        /// <summary>KeyenceNanoE2 프로토콜 값입니다.</summary>
        internal static string KeyenceNanoE2 => "1. CPU 유닛에 프로그램이 없는 상태에서 M1(RUN 전환)을 요청했습니다.\r\n2. RUN/PROG 스위치가 PROG인 상태에서 M1을 요청했습니다.";
        /// <summary>KeyenceNanoE4 프로토콜 값입니다.</summary>
        internal static string KeyenceNanoE4 => "쓰기 금지된 프로그램의 타이머, 카운터 또는 CTC 설정값을 변경하려고 했습니다.";
        /// <summary>KeyenceNanoE5 프로토콜 값입니다.</summary>
        internal static string KeyenceNanoE5 => "CPU 유닛의 오류를 해제하기 전에 M1(RUN 전환)을 요청했습니다.";
        /// <summary>KeyenceNanoE6 프로토콜 값입니다.</summary>
        internal static string KeyenceNanoE6 => "RDC 명령으로 선택한 장치에서 읽기를 요청했습니다.";
        /// <summary>KeyenceSR2000Error00 프로토콜 값입니다.</summary>
        internal static string KeyenceSR2000Error00 => "정의되지 않은 명령을 수신했습니다.";
        /// <summary>KeyenceSR2000Error01 프로토콜 값입니다.</summary>
        internal static string KeyenceSR2000Error01 => "명령 형식 또는 매개변수 수가 올바르지 않습니다.";
        /// <summary>KeyenceSR2000Error02 프로토콜 값입니다.</summary>
        internal static string KeyenceSR2000Error02 => "첫 번째 매개변수가 설정 범위를 벗어났습니다.";
        /// <summary>KeyenceSR2000Error03 프로토콜 값입니다.</summary>
        internal static string KeyenceSR2000Error03 => "두 번째 매개변수가 설정 범위를 벗어났습니다.";
        /// <summary>KeyenceSR2000Error04 프로토콜 값입니다.</summary>
        internal static string KeyenceSR2000Error04 => "두 번째 매개변수가 16진수 형식이 아닙니다.";
        /// <summary>KeyenceSR2000Error05 프로토콜 값입니다.</summary>
        internal static string KeyenceSR2000Error05 => "두 번째 매개변수의 16진수 값이 설정 범위를 초과합니다.";
        /// <summary>KeyenceSR2000Error10 프로토콜 값입니다.</summary>
        internal static string KeyenceSR2000Error10 => "프리셋 데이터가 둘 이상이거나 올바르지 않습니다.";
        /// <summary>KeyenceSR2000Error11 프로토콜 값입니다.</summary>
        internal static string KeyenceSR2000Error11 => "영역 지정 데이터가 올바르지 않습니다.";
        /// <summary>KeyenceSR2000Error12 프로토콜 값입니다.</summary>
        internal static string KeyenceSR2000Error12 => "지정한 파일이 없습니다.";
        /// <summary>KeyenceSR2000Error13 프로토콜 값입니다.</summary>
        internal static string KeyenceSR2000Error13 => "%Tmm-LON,bb 명령의 mm 값이 설정 범위를 초과합니다.";
        /// <summary>KeyenceSR2000Error14 프로토콜 값입니다.</summary>
        internal static string KeyenceSR2000Error14 => "%Tmm-KEYENCE 명령으로 통신을 확인할 수 없습니다.";
        /// <summary>KeyenceSR2000Error20 프로토콜 값입니다.</summary>
        internal static string KeyenceSR2000Error20 => "현재 모드에서는 이 명령을 실행할 수 없습니다.";
        /// <summary>KeyenceSR2000Error21 프로토콜 값입니다.</summary>
        internal static string KeyenceSR2000Error21 => "버퍼가 가득 차서 명령을 실행할 수 없습니다.";
        /// <summary>KeyenceSR2000Error22 프로토콜 값입니다.</summary>
        internal static string KeyenceSR2000Error22 => "매개변수를 불러오거나 저장하는 중 오류가 발생했습니다.";
        /// <summary>KeyenceSR2000Error23 프로토콜 값입니다.</summary>
        internal static string KeyenceSR2000Error23 => "AutoID Network Navigator가 연결되어 있어 RS-232C 명령을 수신할 수 없습니다.";
        /// <summary>KeyenceSR2000Error99 프로토콜 값입니다.</summary>
        internal static string KeyenceSR2000Error99 => "SR-2000 장치에 이상이 의심됩니다. KEYENCE에 문의하십시오.";
        /// <summary>LsisCnet0003 프로토콜 값입니다.</summary>
        internal static string LsisCnet0003 => "개별 읽기 또는 쓰기 요청의 블록 수가 16개를 초과합니다.";
        /// <summary>LsisCnet0004 프로토콜 값입니다.</summary>
        internal static string LsisCnet0004 => "변수 이름의 길이가 최대 16자를 초과합니다.";
        /// <summary>LsisCnet0007 프로토콜 값입니다.</summary>
        internal static string LsisCnet0007 => "X, B, W, D, L 이외의 데이터 타입을 수신했습니다.";
        /// <summary>LsisCnet0011 프로토콜 값입니다.</summary>
        internal static string LsisCnet0011 => "1. 데이터 길이 영역이 올바르지 않습니다.\r\n2. 변수 이름이 %로 시작하지 않습니다.\r\n3. 변수 영역 값이 올바르지 않습니다.\r\n4. 비트 쓰기에 00 또는 01 이외의 값을 지정했습니다.";
        /// <summary>LsisCnet0090 프로토콜 값입니다.</summary>
        internal static string LsisCnet0090 => "등록하지 않은 모니터 실행을 요청했습니다.";
        /// <summary>LsisCnet0190 프로토콜 값입니다.</summary>
        internal static string LsisCnet0190 => "등록 번호가 허용 범위를 초과합니다.";
        /// <summary>LsisCnet0290 프로토콜 값입니다.</summary>
        internal static string LsisCnet0290 => "등록 번호가 허용 범위를 초과합니다.";
        /// <summary>LsisCnet1132 프로토콜 값입니다.</summary>
        internal static string LsisCnet1132 => "지원하는 장치 기호 이외의 문자가 포함되어 있습니다.";
        /// <summary>LsisCnet1232 프로토콜 값입니다.</summary>
        internal static string LsisCnet1232 => "한 번에 읽거나 쓸 수 있는 최대 60워드를 초과합니다.";
        /// <summary>LsisCnet1234 프로토콜 값입니다.</summary>
        internal static string LsisCnet1234 => "불필요한 데이터가 추가되어 있습니다.";
        /// <summary>LsisCnet1332 프로토콜 값입니다.</summary>
        internal static string LsisCnet1332 => "개별 읽기 또는 쓰기의 모든 블록은 데이터 타입이 같아야 합니다.";
        /// <summary>LsisCnet1432 프로토콜 값입니다.</summary>
        internal static string LsisCnet1432 => "데이터 값을 16진수로 변환할 수 없습니다.";
        /// <summary>LsisCnet7132 프로토콜 값입니다.</summary>
        internal static string LsisCnet7132 => "요청 범위가 장치의 지원 영역을 초과합니다.";
        /// <summary>MelsecCurrentTypeNotSupportedBitOperate 프로토콜 값입니다.</summary>
        internal static string MelsecCurrentTypeNotSupportedBitOperate => "해당 데이터 타입은 비트 단위 읽기와 쓰기를 지원하지 않습니다.";
        /// <summary>MelsecCurrentTypeNotSupportedWordOperate 프로토콜 값입니다.</summary>
        internal static string MelsecCurrentTypeNotSupportedWordOperate => "해당 데이터 타입은 워드 단위 읽기와 쓰기를 지원하지 않습니다.";
        /// <summary>MelsecError02 프로토콜 값입니다.</summary>
        internal static string MelsecError02 => "읽기 또는 쓰기 대상 장치의 범위가 올바르지 않습니다.";
        /// <summary>MelsecError51 프로토콜 값입니다.</summary>
        internal static string MelsecError51 => "랜덤 액세스 버퍼의 시작 주소가 0~6143 범위를 벗어났습니다. 시작 주소를 확인하십시오.";
        /// <summary>MelsecError52 프로토콜 값입니다.</summary>
        internal static string MelsecError52 => "1. 랜덤 액세스 버퍼의 시작 주소와 워드 수의 합이 0~6143 범위를 벗어났습니다.\r\n2. 지정한 워드 수를 한 프레임으로 전송할 수 없습니다.";
        /// <summary>MelsecError54 프로토콜 값입니다.</summary>
        internal static string MelsecError54 => "ASCII 통신에서 수신한 문자를 바이너리 코드로 변환할 수 없습니다.";
        /// <summary>MelsecError55 프로토콜 값입니다.</summary>
        internal static string MelsecError55 => "RUN 중 쓰기가 허용되지 않은 상태에서 실행 중인 PLC에 쓰기를 요청했습니다.";
        /// <summary>MelsecError56 프로토콜 값입니다.</summary>
        internal static string MelsecError56 => "지정한 장치가 올바르지 않습니다.";
        /// <summary>MelsecError58 프로토콜 값입니다.</summary>
        internal static string MelsecError58 => "명령의 시작 장치 번호 또는 시작 스텝 번호가 허용 범위를 벗어났거나 요청 점수와의 합이 허용 범위를 초과합니다.";
        /// <summary>MelsecError59 프로토콜 값입니다.</summary>
        internal static string MelsecError59 => "확장 파일 레지스터를 지정할 수 없습니다.";
        /// <summary>MelsecErrorC04D 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC04D => "자동 개방 UDP 또는 무순서 고정 버퍼 통신의 애플리케이션 데이터 길이가 올바르지 않습니다.";
        /// <summary>MelsecErrorC050 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC050 => "이더넷 모듈의 ASCII 통신에서 바이너리로 변환할 수 없는 데이터를 수신했습니다.";
        /// <summary>MelsecErrorC051_54 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC051_54 => "읽기 또는 쓰기 점수가 허용 범위를 벗어났습니다.";
        /// <summary>MelsecErrorC055 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC055 => "파일 데이터 읽기 또는 쓰기 점수가 허용 범위를 벗어났습니다.";
        /// <summary>MelsecErrorC056 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC056 => "읽기 또는 쓰기 요청이 최대 주소를 초과했습니다.";
        /// <summary>MelsecErrorC057 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC057 => "요청 데이터 길이가 문자 영역의 데이터 수와 일치하지 않습니다.";
        /// <summary>MelsecErrorC058 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC058 => "ASCII를 바이너리로 변환한 뒤의 길이가 문자 영역의 데이터 수와 일치하지 않습니다.";
        /// <summary>MelsecErrorC059 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC059 => "명령 또는 서브명령 지정이 올바르지 않습니다.";
        /// <summary>MelsecErrorC05A_B 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC05A_B => "이더넷 모듈이 지정한 장치의 데이터를 읽거나 쓸 수 없습니다.";
        /// <summary>MelsecErrorC05C 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC05C => "워드 장치에 비트 단위 읽기 또는 쓰기를 요청했습니다.";
        /// <summary>MelsecErrorC05D 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC05D => "모니터가 등록되지 않았습니다.";
        /// <summary>MelsecErrorC05E 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC05E => "이더넷 모듈과 PLC CPU 사이의 통신 시간이 CPU 워치독 시간을 초과했습니다.";
        /// <summary>MelsecErrorC05F 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC05F => "대상 PLC에서 요청을 실행할 수 없습니다.";
        /// <summary>MelsecErrorC060 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC060 => "비트 장치에 올바르지 않은 값을 지정하는 등 요청 내용에 오류가 있습니다.";
        /// <summary>MelsecErrorC061 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC061 => "요청 데이터 길이가 문자 영역의 데이터 수와 일치하지 않습니다.";
        /// <summary>MelsecErrorC062 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC062 => "온라인 변경이 금지된 상태에서 MC 프로토콜로 원격 I/O 국에 쓰기를 요청했습니다.";
        /// <summary>MelsecErrorC070 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC070 => "대상 국의 장치 메모리 범위를 지정할 수 없습니다.";
        /// <summary>MelsecErrorC072 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC072 => "워드 장치에 비트 단위 쓰기를 요청했습니다.";
        /// <summary>MelsecErrorC074 프로토콜 값입니다.</summary>
        internal static string MelsecErrorC074 => "대상 PLC에서 요청을 실행할 수 없습니다. 네트워크 번호와 PC 번호를 확인하십시오.";
        /// <summary>MelsecFxAckNagative 프로토콜 값입니다.</summary>
        internal static string MelsecFxAckNagative => "PLC가 유효하지 않은 데이터를 반환했습니다.";
        /// <summary>MelsecFxAckWrong 프로토콜 값입니다.</summary>
        internal static string MelsecFxAckWrong => "PLC 응답 신호 오류: ";
        /// <summary>MelsecFxCrcCheckFailed 프로토콜 값입니다.</summary>
        internal static string MelsecFxCrcCheckFailed => "PLC 응답 프레임의 체크섬 검증에 실패했습니다.";
        /// <summary>MelsecFxLinksError02 프로토콜 값입니다.</summary>
        internal static string MelsecFxLinksError02 => "수신 체크섬이 데이터로 계산한 체크섬과 일치하지 않습니다.";
        /// <summary>MelsecFxLinksError03 프로토콜 값입니다.</summary>
        internal static string MelsecFxLinksError03 => "통신 제어 순서가 매개변수 설정 또는 지정한 순서와 다르거나 명령이 존재하지 않습니다.";
        /// <summary>MelsecFxLinksError06 프로토콜 값입니다.</summary>
        internal static string MelsecFxLinksError06 => "문자 영역 A, B 또는 C 오류입니다. 제어 순서 설정, 장치 번호의 존재 여부, 장치 번호 길이(5자 또는 7자)를 확인하십시오.";
        /// <summary>MelsecFxLinksError07 프로토콜 값입니다.</summary>
        internal static string MelsecFxLinksError07 => "장치에 기록할 데이터가 16진수 ASCII 형식이 아닙니다.";
        /// <summary>MelsecFxLinksError0A 프로토콜 값입니다.</summary>
        internal static string MelsecFxLinksError0A => "지정한 PC 번호의 국이 없습니다.";
        /// <summary>MelsecFxLinksError10 프로토콜 값입니다.</summary>
        internal static string MelsecFxLinksError10 => "지정한 PC 번호의 국이 없습니다.";
        /// <summary>MelsecFxLinksError18 프로토콜 값입니다.</summary>
        internal static string MelsecFxLinksError18 => "하드웨어의 RUN/STOP 스위치 등에서 운전 상태를 결정하므로 원격 RUN/STOP을 실행할 수 없습니다.";
        /// <summary>MelsecFxReceiveZero 프로토콜 값입니다.</summary>
        internal static string MelsecFxReceiveZero => "수신 데이터 길이가 0입니다.";
        /// <summary>MelsecPleaseReferToManualDocument 프로토콜 값입니다.</summary>
        internal static string MelsecPleaseReferToManualDocument => "상세 경보 내용은 Mitsubishi 통신 매뉴얼을 확인하십시오.";
        /// <summary>Memobus01 프로토콜 값입니다.</summary>
        internal static string Memobus01 => "SFC 코드가 올바르지 않습니다.";
        /// <summary>Memobus02 프로토콜 값입니다.</summary>
        internal static string Memobus02 => "참조 번호가 올바르지 않습니다.";
        /// <summary>Memobus03 프로토콜 값입니다.</summary>
        internal static string Memobus03 => "데이터 수가 올바르지 않습니다.";
        /// <summary>Memobus40 프로토콜 값입니다.</summary>
        internal static string Memobus40 => "레지스터 타입이 올바르지 않습니다.";
        /// <summary>Memobus41 프로토콜 값입니다.</summary>
        internal static string Memobus41 => "데이터 타입이 올바르지 않습니다.";
        /// <summary>Memobus42 프로토콜 값입니다.</summary>
        internal static string Memobus42 => "해당 국의 레지스터 타입이 올바르지 않습니다.";
        /// <summary>ModbusTcpFunctionCodeNotSupport 프로토콜 값입니다.</summary>
        internal static string ModbusTcpFunctionCodeNotSupport => "지원하지 않는 기능 코드입니다.";
        /// <summary>ModbusTcpFunctionCodeOverBound 프로토콜 값입니다.</summary>
        internal static string ModbusTcpFunctionCodeOverBound => "데이터 읽기 주소가 허용 범위를 벗어났습니다.";
        /// <summary>ModbusTcpFunctionCodeQuantityOver 프로토콜 값입니다.</summary>
        internal static string ModbusTcpFunctionCodeQuantityOver => "읽기 수량이 최대값을 초과합니다.";
        /// <summary>ModbusTcpFunctionCodeReadWriteException 프로토콜 값입니다.</summary>
        internal static string ModbusTcpFunctionCodeReadWriteException => "장치의 읽기 또는 쓰기 처리에 실패했습니다.";
        /// <summary>NotSupportedDataType 프로토콜 값입니다.</summary>
        internal static string NotSupportedDataType => "지원하지 않는 주소 형식이거나 주소가 올바르지 않습니다.";
        /// <summary>NotSupportedFunction 프로토콜 값입니다.</summary>
        internal static string NotSupportedFunction => "지원하지 않는 기능입니다.";
        /// <summary>OmronReceiveDataError 프로토콜 값입니다.</summary>
        internal static string OmronReceiveDataError => "응답 데이터에 오류가 있습니다.";
        /// <summary>OmronStatus0 프로토콜 값입니다.</summary>
        internal static string OmronStatus0 => "정상 응답입니다.";
        /// <summary>OmronStatus1 프로토콜 값입니다.</summary>
        internal static string OmronStatus1 => "메시지 헤더가 FINS 형식이 아닙니다.";
        /// <summary>OmronStatus2 프로토콜 값입니다.</summary>
        internal static string OmronStatus2 => "데이터 길이가 너무 깁니다.";
        /// <summary>OmronStatus20 프로토콜 값입니다.</summary>
        internal static string OmronStatus20 => "최대 연결 수를 초과했습니다.";
        /// <summary>OmronStatus21 프로토콜 값입니다.</summary>
        internal static string OmronStatus21 => "지정한 노드가 이미 연결되어 있습니다.";
        /// <summary>OmronStatus22 프로토콜 값입니다.</summary>
        internal static string OmronStatus22 => "PLC에 설정되지 않은 보호 네트워크 노드로 연결을 시도했습니다.";
        /// <summary>OmronStatus23 프로토콜 값입니다.</summary>
        internal static string OmronStatus23 => "클라이언트의 네트워크 노드 번호가 허용 범위를 초과합니다.";
        /// <summary>OmronStatus24 프로토콜 값입니다.</summary>
        internal static string OmronStatus24 => "클라이언트의 네트워크 노드 번호가 이미 사용 중입니다.";
        /// <summary>OmronStatus25 프로토콜 값입니다.</summary>
        internal static string OmronStatus25 => "모든 네트워크 노드가 사용 중입니다.";
        /// <summary>OmronStatus3 프로토콜 값입니다.</summary>
        internal static string OmronStatus3 => "지원하지 않는 명령입니다.";
        /// <summary>PanasonicAddressParameterCannotBeNull 프로토콜 값입니다.</summary>
        internal static string PanasonicAddressParameterCannotBeNull => "주소 매개변수는 비어 있을 수 없습니다.";
        /// <summary>PanasonicMc4031 프로토콜 값입니다.</summary>
        internal static string PanasonicMc4031 => "시작 장치 주소와 쓰기 점수의 합이 주소 범위를 초과합니다.";
        /// <summary>PanasonicMcC051 프로토콜 값입니다.</summary>
        internal static string PanasonicMcC051 => "장치 점수가 지정 범위를 벗어났습니다.";
        /// <summary>PanasonicMcC056 프로토콜 값입니다.</summary>
        internal static string PanasonicMcC056 => "시작 장치 주소가 지정 범위를 벗어났습니다.";
        /// <summary>PanasonicMcC059 프로토콜 값입니다.</summary>
        internal static string PanasonicMcC059 => "수신한 명령이 MC 프로토콜 명령표에 없습니다.";
        /// <summary>PanasonicMcC05B 프로토콜 값입니다.</summary>
        internal static string PanasonicMcC05B => "장치 코드가 지정 범위를 벗어났습니다.";
        /// <summary>PanasonicMcC05C 프로토콜 값입니다.</summary>
        internal static string PanasonicMcC05C => "비트 단위 서브명령(0001)에 워드 장치를 지정했습니다.";
        /// <summary>PanasonicMcC05F 프로토콜 값입니다.</summary>
        internal static string PanasonicMcC05F => "네트워크 번호, PC 번호, 대상 유닛 I/O 번호 또는 수신 쓰기 데이터 수가 올바르지 않습니다.";
        /// <summary>PanasonicMcC060 프로토콜 값입니다.</summary>
        internal static string PanasonicMcC060 => "접점 쓰기 데이터가 0 또는 1이 아닙니다.";
        /// <summary>PanasonicMcC061 프로토콜 값입니다.</summary>
        internal static string PanasonicMcC061 => "시작 문자 검사 또는 프레임 수신에 필요한 최소 바이트 수에 미달합니다.";
        /// <summary>PanasonicMewStatus20 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus20 => "알 수 없는 오류입니다.";
        /// <summary>PanasonicMewStatus21 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus21 => "NAK 오류: 원격 유닛을 식별할 수 없거나 데이터에 오류가 있습니다.";
        /// <summary>PanasonicMewStatus22 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus22 => "WACK 오류: 원격 유닛의 수신 버퍼가 가득 찼습니다.";
        /// <summary>PanasonicMewStatus23 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus23 => "국 번호 중복: 원격 유닛 번호(01~16)가 로컬 유닛과 같습니다.";
        /// <summary>PanasonicMewStatus24 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus24 => "전송 형식 불일치, 프레임 초과 또는 데이터 오류입니다.";
        /// <summary>PanasonicMewStatus25 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus25 => "전송 하드웨어가 동작을 중지했습니다.";
        /// <summary>PanasonicMewStatus26 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus26 => "원격 유닛 번호가 01~63 범위를 벗어났습니다.";
        /// <summary>PanasonicMewStatus27 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus27 => "수신 프레임이 넘쳤거나 모듈 사이의 프레임 길이가 다릅니다.";
        /// <summary>PanasonicMewStatus28 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus28 => "원격 유닛이 없거나 응답 시간이 초과되었습니다.";
        /// <summary>PanasonicMewStatus29 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus29 => "닫힌 버퍼로 송수신을 요청했습니다.";
        /// <summary>PanasonicMewStatus30 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus30 => "전송 금지 상태가 지속되어 시간이 초과되었습니다.";
        /// <summary>PanasonicMewStatus40 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus40 => "명령 데이터 전송 중 BCC 오류가 발생했습니다.";
        /// <summary>PanasonicMewStatus41 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus41 => "명령 정보가 전송 형식과 일치하지 않습니다.";
        /// <summary>PanasonicMewStatus42 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus42 => "지원하지 않는 명령이거나 대상 국에서 지원하지 않는 요청입니다.";
        /// <summary>PanasonicMewStatus43 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus43 => "전송 요청이 보류된 상태에서 추가 명령을 전송했습니다.";
        /// <summary>PanasonicMewStatus50 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus50 => "존재하지 않는 링크 번호를 설정했습니다.";
        /// <summary>PanasonicMewStatus51 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus51 => "다른 유닛에 명령을 전송하는 중 로컬 송신 버퍼가 가득 찼습니다.";
        /// <summary>PanasonicMewStatus52 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus52 => "다른 유닛으로 전송할 수 없습니다.";
        /// <summary>PanasonicMewStatus53 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus53 => "다른 명령을 처리 중이므로 현재 요청을 처리할 수 없습니다.";
        /// <summary>PanasonicMewStatus60 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus60 => "명령에 사용할 수 없는 코드가 있거나 X, Y, D 등의 영역 매개변수가 누락되었습니다.";
        /// <summary>PanasonicMewStatus61 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus61 => "접점 번호, 영역 번호, BCD/HEX 형식 또는 데이터 범위가 올바르지 않습니다.";
        /// <summary>PanasonicMewStatus62 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus62 => "모니터 또는 추적 등록 없이 실행했거나 등록 가능한 데이터 수를 초과했습니다.";
        /// <summary>PanasonicMewStatus63 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus63 => "현재 PLC 운전 모드에서는 이 명령을 처리할 수 없습니다.";
        /// <summary>PanasonicMewStatus64 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus64 => "1. 내장 ROM, 주 메모리 또는 SD 카드에 이상이 있습니다.\r\n2. ROM 전송 내용이 용량을 초과합니다.\r\n3. 읽기 또는 쓰기 오류가 발생했습니다.";
        /// <summary>PanasonicMewStatus65 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus65 => "메모리 보호 상태에서 프로그램 영역 또는 시스템 레지스터에 쓰기를 요청했습니다.";
        /// <summary>PanasonicMewStatus66 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus66 => "프로그램 주소, 절대 주소, BCD/HEX 형식 또는 주소 범위가 올바르지 않습니다.";
        /// <summary>PanasonicMewStatus67 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus67 => "읽을 데이터가 없습니다. 주석 레지스터 등에 데이터가 기록되어 있는지 확인하십시오.";
        /// <summary>PanasonicMewStatus68 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus68 => "RUN 중 변경할 수 없는 명령(ED, SUB, RET, INT, IRET, SSTP, STPE)을 수정하려고 했습니다. 제어 유닛에는 기록되지 않았습니다.";
        /// <summary>PanasonicMewStatus71 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus71 => "진행 중인 명령과 동시에 처리할 수 없는 명령을 실행했습니다.";
        /// <summary>PanasonicMewStatus78 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus78 => "SD 카드가 설치되지 않았습니다.";
        /// <summary>PanasonicMewStatus80 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus80 => "보호 데이터의 CRC 코드가 올바르지 않습니다.";
        /// <summary>PanasonicMewStatus81 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus81 => "유효한 데이터가 없습니다.";
        /// <summary>PanasonicMewStatus90 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus90 => "로그 추적 중 처리할 수 없는 명령을 실행했습니다.";
        /// <summary>PanasonicMewStatus92 프로토콜 값입니다.</summary>
        internal static string PanasonicMewStatus92 => "지원하는 Panasonic 산업용 SD 카드가 아닙니다.";
        /// <summary>PanasonicReceiveLengthMustLargerThan9 프로토콜 값입니다.</summary>
        internal static string PanasonicReceiveLengthMustLargerThan9 => "수신 데이터 길이는 9바이트보다 커야 합니다.";
        /// <summary>ReceiveDataLengthTooShort 프로토콜 값입니다.</summary>
        internal static string ReceiveDataLengthTooShort => "수신 데이터 길이가 너무 짧습니다: ";
        /// <summary>SAMHeadCheckFailed 프로토콜 값입니다.</summary>
        internal static string SAMHeadCheckFailed => "SAM 프레임 헤더 검증에 실패했습니다.";
        /// <summary>SAMLengthCheckFailed 프로토콜 값입니다.</summary>
        internal static string SAMLengthCheckFailed => "SAM 데이터 길이 검증에 실패했습니다.";
        /// <summary>SAMReceiveLengthMustLargerThan8 프로토콜 값입니다.</summary>
        internal static string SAMReceiveLengthMustLargerThan8 => "SAM 수신 데이터 길이는 8바이트보다 커야 합니다.";
        /// <summary>SAMStatus10 프로토콜 값입니다.</summary>
        internal static string SAMStatus10 => "수신 데이터의 체크섬이 올바르지 않습니다.";
        /// <summary>SAMStatus11 프로토콜 값입니다.</summary>
        internal static string SAMStatus11 => "수신 데이터 길이가 올바르지 않습니다.";
        /// <summary>SAMStatus21 프로토콜 값입니다.</summary>
        internal static string SAMStatus21 => "수신 명령이 올바르지 않습니다.";
        /// <summary>SAMStatus23 프로토콜 값입니다.</summary>
        internal static string SAMStatus23 => "허용되지 않은 작업입니다.";
        /// <summary>SAMStatus24 프로토콜 값입니다.</summary>
        internal static string SAMStatus24 => "식별할 수 없는 오류입니다.";
        /// <summary>SAMStatus31 프로토콜 값입니다.</summary>
        internal static string SAMStatus31 => "카드의 SAM 인증에 실패했습니다.";
        /// <summary>SAMStatus32 프로토콜 값입니다.</summary>
        internal static string SAMStatus32 => "SAM의 카드 인증에 실패했습니다.";
        /// <summary>SAMStatus33 프로토콜 값입니다.</summary>
        internal static string SAMStatus33 => "정보 검증에 실패했습니다.";
        /// <summary>SAMStatus40 프로토콜 값입니다.</summary>
        internal static string SAMStatus40 => "식별할 수 없는 카드 종류입니다.";
        /// <summary>SAMStatus41 프로토콜 값입니다.</summary>
        internal static string SAMStatus41 => "신분증 또는 카드 작업에 실패했습니다.";
        /// <summary>SAMStatus47 프로토콜 값입니다.</summary>
        internal static string SAMStatus47 => "난수 생성에 실패했습니다.";
        /// <summary>SAMStatus60 프로토콜 값입니다.</summary>
        internal static string SAMStatus60 => "SAM 자체 검사에 실패했습니다.";
        /// <summary>SAMStatus66 프로토콜 값입니다.</summary>
        internal static string SAMStatus66 => "SAM 사용 권한이 없습니다.";
        /// <summary>SAMStatus80 프로토콜 값입니다.</summary>
        internal static string SAMStatus80 => "카드를 찾지 못했습니다.";
        /// <summary>SAMStatus81 프로토콜 값입니다.</summary>
        internal static string SAMStatus81 => "카드 선택에 실패했습니다.";
        /// <summary>SAMStatus91 프로토콜 값입니다.</summary>
        internal static string SAMStatus91 => "카드에 내용이 없습니다.";
        /// <summary>SAMSumCheckFailed 프로토콜 값입니다.</summary>
        internal static string SAMSumCheckFailed => "SAM 데이터의 체크섬 검증에 실패했습니다.";
        /// <summary>SiemensDataLengthCheckFailed 프로토콜 값입니다.</summary>
        internal static string SiemensDataLengthCheckFailed => "블록 길이 검증에 실패했습니다. PUT/GET 허용과 DB 블록 최적화 해제 여부를 확인하십시오.";
        /// <summary>SiemensDBAddressNotAllowedLargerThan255 프로토콜 값입니다.</summary>
        internal static string SiemensDBAddressNotAllowedLargerThan255 => "DB 블록 번호는 255보다 클 수 없습니다.";
        /// <summary>SiemensError0006 프로토콜 값입니다.</summary>
        internal static string SiemensError0006 => "현재 작업의 데이터 타입을 지원하지 않습니다.";
        /// <summary>SiemensError000A 프로토콜 값입니다.</summary>
        internal static string SiemensError000A => "요청한 객체가 없습니다. 존재하지 않는 DB 블록을 요청한 경우 발생합니다.";
        /// <summary>SiemensReadLengthCannotLargerThan19 프로토콜 값입니다.</summary>
        internal static string SiemensReadLengthCannotLargerThan19 => "한 번에 읽는 주소 항목 수는 19개를 초과할 수 없습니다.";
        /// <summary>SiemensReadLengthMustBeEvenNumber 프로토콜 값입니다.</summary>
        internal static string SiemensReadLengthMustBeEvenNumber => "읽기 데이터 길이는 짝수여야 합니다.";
        /// <summary>SiemensReadLengthOverPlcAssign 프로토콜 값입니다.</summary>
        internal static string SiemensReadLengthOverPlcAssign => "읽기 범위가 PLC에 설정된 범위를 초과합니다.";
        /// <summary>SiemensWriteError 프로토콜 값입니다.</summary>
        internal static string SiemensWriteError => "데이터 쓰기 오류입니다. 오류 코드: ";
        /// <summary>SuccessText 프로토콜 값입니다.</summary>
        internal static string SuccessText => "성공";
        /// <summary>TextDescription 프로토콜 값입니다.</summary>
        internal static string TextDescription => "설명: ";
        /// <summary>ToyoPuc11 프로토콜 값입니다.</summary>
        internal static string ToyoPuc11 => "CPU 모듈의 하드웨어 이상으로 처리할 수 없습니다.";
        /// <summary>ToyoPuc20 프로토콜 값입니다.</summary>
        internal static string ToyoPuc20 => "중계 명령의 고정 ENQ 값이 05가 아닙니다.";
        /// <summary>ToyoPuc21 프로토콜 값입니다.</summary>
        internal static string ToyoPuc21 => "전송 바이트 수가 올바르지 않습니다.";
        /// <summary>ToyoPuc23 프로토콜 값입니다.</summary>
        internal static string ToyoPuc23 => "명령 코드가 올바르지 않습니다.";
        /// <summary>ToyoPuc24 프로토콜 값입니다.</summary>
        internal static string ToyoPuc24 => "서브명령 코드가 올바르지 않습니다.";
        /// <summary>ToyoPuc25 프로토콜 값입니다.</summary>
        internal static string ToyoPuc25 => "명령 형식의 데이터 바이트가 올바르지 않습니다.";
        /// <summary>ToyoPuc34 프로토콜 값입니다.</summary>
        internal static string ToyoPuc34 => "접근 금지 설정으로 접근할 수 없습니다.";
        /// <summary>ToyoPuc3E 프로토콜 값입니다.</summary>
        internal static string ToyoPuc3E => "리셋 중이므로 명령을 실행할 수 없습니다.";
        /// <summary>ToyoPuc3F 프로토콜 값입니다.</summary>
        internal static string ToyoPuc3F => "정지 상태이므로 명령을 실행할 수 없습니다.";
        /// <summary>ToyoPuc40 프로토콜 값입니다.</summary>
        internal static string ToyoPuc40 => "시작 주소 또는 시작 주소와 데이터 수의 합이 주소 범위를 벗어났습니다.";
        /// <summary>ToyoPuc41 프로토콜 값입니다.</summary>
        internal static string ToyoPuc41 => "워드 또는 바이트 수가 허용 범위를 벗어났습니다.";
        /// <summary>TwoParametersLengthIsNotSame 프로토콜 값입니다.</summary>
        internal static string TwoParametersLengthIsNotSame => "두 매개변수의 길이가 일치하지 않습니다.";
        /// <summary>UnknownError 프로토콜 값입니다.</summary>
        internal static string UnknownError => "알 수 없는 오류입니다.";
        /// <summary>Vigor02 프로토콜 값입니다.</summary>
        internal static string Vigor02 => "통신 데이터의 합계 체크섬에 오류가 있습니다.";
        /// <summary>Vigor04 프로토콜 값입니다.</summary>
        internal static string Vigor04 => "데이터 바이트 수 또는 항목 수가 0입니다.";
        /// <summary>Vigor06 프로토콜 값입니다.</summary>
        internal static string Vigor06 => "데이터 주소가 범위를 벗어났습니다.";
        /// <summary>Vigor08 프로토콜 값입니다.</summary>
        internal static string Vigor08 => "ASCII 변환에 실패했습니다.";
        /// <summary>Vigor31 프로토콜 값입니다.</summary>
        internal static string Vigor31 => "명령이 존재하지 않습니다.";
        /// <summary>YamatakeDigitronCPL40 프로토콜 값입니다.</summary>
        internal static string YamatakeDigitronCPL40 => "명령 형식이 올바르지 않습니다.";
        /// <summary>YamatakeDigitronCPL41 프로토콜 값입니다.</summary>
        internal static string YamatakeDigitronCPL41 => "데이터 수가 16개를 초과합니다. RS 명령의 데이터 수도 포함됩니다.";
        /// <summary>YamatakeDigitronCPL42 프로토콜 값입니다.</summary>
        internal static string YamatakeDigitronCPL42 => "주소가 범위를 벗어나 모든 메시지를 폐기했습니다.";
        /// <summary>YamatakeDigitronCPL43 프로토콜 값입니다.</summary>
        internal static string YamatakeDigitronCPL43 => "데이터 영역의 값이 올바르지 않아 모든 메시지를 폐기했습니다.";
        /// <summary>YamatakeDigitronCPL44 프로토콜 값입니다.</summary>
        internal static string YamatakeDigitronCPL44 => "데이터 값이 범위를 초과했습니다. 현재 주소를 제외한 나머지는 계속 처리합니다.";
        /// <summary>YamatakeDigitronCPL45 프로토콜 값입니다.</summary>
        internal static string YamatakeDigitronCPL45 => "장치 상태 또는 주소 쓰기 금지 설정으로 기록할 수 없습니다.";
        /// <summary>YamatakeDigitronCPL46 프로토콜 값입니다.</summary>
        internal static string YamatakeDigitronCPL46 => "CPL 통신 쓰기가 금지되어 있습니다. 기능 설정 C27에서 통신 쓰기를 허용하십시오.";
        /// <summary>YamatakeDigitronCPL47 프로토콜 값입니다.</summary>
        internal static string YamatakeDigitronCPL47 => "우선순위가 더 높은 설정이 유효하므로 통신으로 모드를 변경할 수 없습니다.";
        /// <summary>YamatakeDigitronCPL48 프로토콜 값입니다.</summary>
        internal static string YamatakeDigitronCPL48 => "프로그래머가 기록 중입니다. 기록이 끝나고 기본 화면으로 돌아온 뒤 다시 요청하십시오.";
        /// <summary>YamatakeDigitronCPL99 프로토콜 값입니다.</summary>
        internal static string YamatakeDigitronCPL99 => "정의되지 않은 명령입니다.";
        /// <summary>YokogawaLinkError01 프로토콜 값입니다.</summary>
        internal static string YokogawaLinkError01 => "CPU 번호가 1~4 범위를 벗어났습니다.";
        /// <summary>YokogawaLinkError02 프로토콜 값입니다.</summary>
        internal static string YokogawaLinkError02 => "명령이 존재하지 않거나 실행할 수 없습니다.";
        /// <summary>YokogawaLinkError03 프로토콜 값입니다.</summary>
        internal static string YokogawaLinkError03 => "장치 이름이 없거나 워드 단위 접근에 릴레이 장치를 잘못 지정했습니다.";
        /// <summary>YokogawaLinkError04 프로토콜 값입니다.</summary>
        internal static string YokogawaLinkError04 => "설정값이 범위를 벗어났습니다. 비트 값은 0/1, 워드 값은 0000~FFFF이어야 하며 시작 주소도 유효한 범위여야 합니다.";
        /// <summary>YokogawaLinkError05 프로토콜 값입니다.</summary>
        internal static string YokogawaLinkError05 => "비트 또는 워드 수가 사양을 초과했거나 데이터 수와 장치 매개변수 수가 일치하지 않습니다.";
        /// <summary>YokogawaLinkError06 프로토콜 값입니다.</summary>
        internal static string YokogawaLinkError06 => "모니터 명령(BRS, WRS)을 지정하지 않고 모니터 실행을 요청했습니다.";
        /// <summary>YokogawaLinkError07 프로토콜 값입니다.</summary>
        internal static string YokogawaLinkError07 => "BASIC CPU가 아닙니다.";
        /// <summary>YokogawaLinkError08 프로토콜 값입니다.</summary>
        internal static string YokogawaLinkError08 => "그 밖의 원인으로 매개변수가 유효하지 않습니다.";
        /// <summary>YokogawaLinkError41 프로토콜 값입니다.</summary>
        internal static string YokogawaLinkError41 => "통신 중 오류가 발생했습니다.";
        /// <summary>YokogawaLinkError42 프로토콜 값입니다.</summary>
        internal static string YokogawaLinkError42 => "비트 누락 또는 문자 변경으로 체크섬이 일치하지 않습니다.";
        /// <summary>YokogawaLinkError43 프로토콜 값입니다.</summary>
        internal static string YokogawaLinkError43 => "수신 데이터 수가 지정값을 초과했습니다.";
        /// <summary>YokogawaLinkError44 프로토콜 값입니다.</summary>
        internal static string YokogawaLinkError44 => "문자 수신 제한 시간(5초)이 초과되었습니다. 종료 문자 또는 ETX가 수신되지 않았습니다.";
        /// <summary>YokogawaLinkError51 프로토콜 값입니다.</summary>
        internal static string YokogawaLinkError51 => "CPU 응답 시간이 초과되었습니다. 전원, 시퀀스 CPU 하드웨어, 명령 수신 상태 또는 서비스 시간을 확인하십시오.";
        /// <summary>YokogawaLinkError52 프로토콜 값입니다.</summary>
        internal static string YokogawaLinkError52 => "CPU가 처리 중 오류를 검출했습니다.";
        /// <summary>YokogawaLinkErrorF1 프로토콜 값입니다.</summary>
        internal static string YokogawaLinkErrorF1 => "Load(PLD) 또는 Save(PSV) 이외의 명령 실행 중 Cancel(PLC)을 요청했거나 내부 오류가 발생했습니다.";
    }
}
