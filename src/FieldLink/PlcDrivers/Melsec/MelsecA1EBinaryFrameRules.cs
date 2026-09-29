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

namespace FieldLink.PlcDrivers.Melsec
{

    /// <summary>MelsecA1EBinaryFrameRules의 프레임 길이·형식·응답 식별 규칙입니다. 요청별 버퍼를 보관하지 않습니다.</summary>
    public class MelsecA1EBinaryFrameRules : ProtocolFrameRules
    {

        /// <inheritdoc />
        public override int HeaderLength => 2;

        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request)
        {
            RequireRequest(request, 12);
            if (header[1] == 0x5B)
                return 2; // 비정상적인 코드 + 0x00
            else if (header[1] == 0x00)
            {
                switch (header[0])
                {
                    case 0x80:
                        return request[10] != 0x00 ? (request[10] + 1) / 2 : 128; // 부제 제목에 답하기 위해
                    case 0x81:
                        return request[10] * 2; // 글자 단위가 대량으로 읽히고, 부제목에 답합니다.
                    case 0x82: // 단위 대량으로 작성한 후, 부제 제목에 응답하십시오.
                    case 0x83:
                        return 0; // 글자 단위로 작성한 후, 부제목에 응답하십시오.
                    default:
                        return 0;
                }
            }
            else
                return 0;
        //A 호환 1E 프로토콜에서, 값이 쓰여진 후, 예외가 발생하지 않으면, 하위 제목 + 종료 코드 ((0x00) 을 반환한다.
        //프로토콜 헤더에서 이미 읽었으므로 이후에 읽을 길이는 0입니다(contentLength=0).
        }

        /// <inheritdoc />
        protected override bool ValidateHeader(byte[] header, byte[] request)
        {
            if (request != null && request.Length >= 12)
                return ((header[0] - request[0]) == 0x80);
            return false;
        }
    }
}
