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

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>OmronHostLinkCModeServer 요청 프레임을 생성합니다.</summary>
    public static class OmronHostLinkCModeServerCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "status">status에 사용할 입력값입니다.</param>
        /// <param name = "finsCore">finsCore에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommand(HostLinkCModeResponseOptions context, int status, byte[] finsCore, byte[] data)
        {
            if (data == null)
                data = new byte[0];
            data = ProtocolBytes.BytesToAsciiBytes(data);
            byte[] back = new byte[11 + data.Length];
            Encoding.ASCII.GetBytes("@0000").CopyTo(back, 0);
            Encoding.ASCII.GetBytes(context.UnitNumber.ToString("X2")).CopyTo(back, 1);
            Array.Copy(finsCore, 3, back, 3, 2); // 명령도 복사했습니다
            Encoding.ASCII.GetBytes(status.ToString("X2")).CopyTo(back, 5); // 상태 정보
            if (data.Length > 0)
                data.CopyTo(back, 7);
            // FCS 계산
            int tmp = back[0];
            for (int i = 1; i < back.Length - 4; i++)
            {
                tmp ^= back[i];
            }

            ProtocolBytes.BuildAsciiBytesFrom((byte)tmp).CopyTo(back, back.Length - 4);
            back[back.Length - 2] = (byte)'*';
            back[back.Length - 1] = 0x0D;
            return back;
        }
    }
}
