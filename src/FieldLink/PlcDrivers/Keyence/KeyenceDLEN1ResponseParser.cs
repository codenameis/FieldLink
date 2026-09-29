using FieldLink.PlcDrivers.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FieldLink.PlcDrivers.Keyence
{
    /// <summary>KeyenceDLEN1 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class KeyenceDLEN1ResponseParser
    {
        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckResponse(byte[] content)
        {
            if (content.Length >= 9)
            {
                if (Encoding.ASCII.GetString(content, 0, 2) == "ER")
                {
                    int err = Convert.ToInt32(Encoding.ASCII.GetString(content, 6, 3));
                    switch (err)
                    {
                        case 9:
                            return new OperationResult(err, "기록할 데이터가 유효 범위를 벗어났거나 센서가 지정한 ID 또는 데이터 번호에 대한 쓰기를 지원하지 않습니다.");
                        case 12:
                            return new OperationResult(err, "동작 명령을 실행할 수 없거나 센서가 지정한 ID 또는 데이터 번호에 대한 쓰기를 지원하지 않습니다.");
                        case 14:
                            return new OperationResult(err, "지정한 주소는 쓰기가 금지되어 있거나 현재 기록할 수 없습니다.");
                        case 16:
                            return new OperationResult(err, "지정한 데이터 번호는 읽기가 금지되어 있거나 현재 읽을 수 없습니다.");
                        case 20:
                            return new OperationResult(err, "데이터 번호가 유효 범위를 벗어났습니다.");
                        case 22:
                            return new OperationResult(err, "ID가 유효 범위를 벗어났습니다.");
                        case 31:
                            return new OperationResult(err, "센서가 지정한 ID 또는 데이터 번호의 읽기/쓰기를 지원하지 않거나, 현재 모드에서 쓰기가 금지되어 있거나, 통신 초기화 중입니다.");
                        case 254:
                            return new OperationResult(err, "시스템 오류 상태입니다. 기동이 완료될 때까지 기다리고 D-bus 커넥터 등의 연결 상태를 확인하십시오.");
                        case 255:
                            return new OperationResult(err, "명령 형식이 올바르지 않습니다.");
                    }

                    return new OperationResult(err, ProtocolMessages.UnknownError);
                }
            }

            return OperationResult.CreateSuccessResult();
        }
    }
}
