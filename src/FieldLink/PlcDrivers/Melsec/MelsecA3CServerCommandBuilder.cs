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
using static FieldLink.PlcDrivers.Melsec.MelsecA3CServerValueConverter;
using static FieldLink.PlcDrivers.Melsec.MelsecA3CServerResponseParser;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecA3CServer 요청 프레임을 생성합니다.</summary>
    public static class MelsecA3CServerCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "status">status에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommand(A3CResponseOptions context, ushort status, byte[] data)
        {
            if (data == null)
                data = new byte[0];
            if (data.Length == 0)
            {
                // 쓰기 동작
                if (context.Format == 1)
                {
                    if (status == 0)
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes("\u0006F90000FF00");
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 3);
                        return buffer;
                    }
                    else
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes("\u0015F90000FF000000");
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 3);
                        ProtocolBytes.BuildAsciiBytesFrom(status).CopyTo(buffer, buffer.Length - 4);
                        return buffer;
                    }
                }
                else if (context.Format == 2)
                {
                    if (status == 0)
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes("\u000600F90000FF00");
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 5);
                        return buffer;
                    }
                    else
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes("\u001500F90000FF000000");
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 5);
                        ProtocolBytes.BuildAsciiBytesFrom(status).CopyTo(buffer, buffer.Length - 4);
                        return buffer;
                    }
                }
                else if (context.Format == 3)
                {
                    if (status == 0)
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes("\u0002F90000FF00QACK\u0003");
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 3);
                        return buffer;
                    }
                    else
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes("\u0002F90000FF00QNAK0000\u0003");
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 3);
                        ProtocolBytes.BuildAsciiBytesFrom(status).CopyTo(buffer, buffer.Length - 5);
                        return buffer;
                    }
                }
                else if (context.Format == 4)
                {
                    if (status == 0)
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes("\u0006F90000FF00");
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 3);
                        return buffer;
                    }
                    else
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes("\u0015F90000FF000000\u000D\u000A");
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 3);
                        ProtocolBytes.BuildAsciiBytesFrom(status).CopyTo(buffer, buffer.Length - 6);
                        return buffer;
                    }
                }

                return null;
            }
            else
            {
                // 읽기 동작
                if (context.Format == 1)
                {
                    if (status != 0)
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes("\u0015F90000FF000000");
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 3);
                        ProtocolBytes.BuildAsciiBytesFrom(status).CopyTo(buffer, buffer.Length - 4);
                        return buffer;
                    }
                    else
                    {
                        byte[] buffer = new byte[(context.SumCheck ? 14 : 12) + data.Length];
                        Encoding.ASCII.GetBytes("\u0002F90000FF00").CopyTo(buffer, 0);
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 3);
                        data.CopyTo(buffer, 11);
                        buffer[buffer.Length - (context.SumCheck ? 3 : 1)] = 0x03;
                        if (context.SumCheck)
                            SetSumCheck(context, buffer, 1, 2);
                        return buffer;
                    }
                }
                else if (context.Format == 2)
                {
                    if (status != 0)
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes("\u001500F90000FF000000");
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 5);
                        ProtocolBytes.BuildAsciiBytesFrom(status).CopyTo(buffer, buffer.Length - 4);
                        return buffer;
                    }
                    else
                    {
                        byte[] buffer = new byte[(context.SumCheck ? 16 : 14) + data.Length];
                        Encoding.ASCII.GetBytes("\u000200F90000FF00").CopyTo(buffer, 0);
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 5);
                        data.CopyTo(buffer, 13);
                        buffer[buffer.Length - (context.SumCheck ? 3 : 1)] = 0x03;
                        if (context.SumCheck)
                            SetSumCheck(context, buffer, 1, 2);
                        return buffer;
                    }
                }
                else if (context.Format == 3)
                {
                    if (status != 0)
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes("\u0002F90000FF00QNAK0000\u0003");
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 3);
                        ProtocolBytes.BuildAsciiBytesFrom(status).CopyTo(buffer, buffer.Length - 5);
                        return buffer;
                    }
                    else
                    {
                        byte[] buffer = new byte[(context.SumCheck ? 18 : 16) + data.Length];
                        Encoding.ASCII.GetBytes("\u0002F90000FF00QACK").CopyTo(buffer, 0);
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 3);
                        buffer[buffer.Length - (context.SumCheck ? 3 : 1)] = 0x03;
                        data.CopyTo(buffer, 15);
                        if (context.SumCheck)
                            SetSumCheck(context, buffer, 1, 2);
                        return buffer;
                    }
                }
                else if (context.Format == 4)
                {
                    if (status != 0)
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes("\u0015F90000FF000000\u000D\u000A");
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 3);
                        ProtocolBytes.BuildAsciiBytesFrom(status).CopyTo(buffer, buffer.Length - 6);
                        return buffer;
                    }
                    else
                    {
                        byte[] buffer = new byte[(context.SumCheck ? 16 : 14) + data.Length];
                        Encoding.ASCII.GetBytes("\u0002F90000FF00").CopyTo(buffer, 0);
                        ProtocolBytes.BuildAsciiBytesFrom(context.Station).CopyTo(buffer, 3);
                        buffer[buffer.Length - (context.SumCheck ? 5 : 3)] = 0x03;
                        data.CopyTo(buffer, 11);
                        if (context.SumCheck)
                            SetSumCheck(context, buffer, 1, 4);
                        buffer[buffer.Length - 2] = 0x0D;
                        buffer[buffer.Length - 1] = 0x0A;
                        return buffer;
                    }
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
