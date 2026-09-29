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
using static FieldLink.PlcDrivers.Melsec.MelsecFxLinksServerResponseParser;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecFxLinksServer 요청 프레임을 생성합니다.</summary>
    public static class MelsecFxLinksServerCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "status">status에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "format">format에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommand(FxLinksResponseOptions context, byte status, byte[] data, int format)
        {
            if (data == null)
                data = new byte[0];
            if (data.Length == 0)
            {
                // 쓰기 동작
                if (format == 1)
                {
                    if (status == 0)
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes("\u0006F9FF");
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 1);
                        return buffer;
                    }
                    else
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes("\u001500FF00");
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 1);
                        ProtocolBytes.BuildAsciiBytesFrom(status).CopyTo(buffer, 5);
                        return buffer;
                    }
                }
                else if (format == 4)
                {
                    byte[] buffer = PackCommand(context, status, data, 1);
                    return ProtocolBytes.SpliceArray(buffer, new byte[] { AsciiControl.CR, AsciiControl.LF });
                }

                return null;
            }
            else
            {
                // 읽기 동작
                if (format == 1)
                {
                    if (status != 0)
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes("\u001500FF00");
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 1);
                        ProtocolBytes.BuildAsciiBytesFrom(status).CopyTo(buffer, 5);
                        return buffer;
                    }
                    else
                    {
                        byte[] buffer = new byte[(context.SumCheck ? 8 : 6) + data.Length];
                        buffer[0] = AsciiControl.STX;
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 1);
                        Encoding.ASCII.GetBytes("FF").CopyTo(buffer, 3);
                        data.CopyTo(buffer, 5);
                        buffer[buffer.Length - (context.SumCheck ? 3 : 1)] = AsciiControl.ETX;
                        if (context.SumCheck)
                            AdditiveChecksum.WriteAscii(buffer, 1, buffer.Length - 2);
                        return buffer;
                    }
                }
                else if (format == 4)
                {
                    byte[] buffer = PackCommand(context, status, data, 1);
                    return ProtocolBytes.SpliceArray(buffer, new byte[] { AsciiControl.CR, AsciiControl.LF });
                }

                return null;
            }
        }
    }
}
