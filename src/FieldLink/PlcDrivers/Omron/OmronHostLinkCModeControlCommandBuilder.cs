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
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeAddressParser;
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeCommandBuilder;
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeResponseParser;
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeControlResponseParser;

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>OmronHostLinkCModeControl 요청 프레임을 생성합니다.</summary>
    public static class OmronHostLinkCModeControlCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "unitNumber">unitNumber에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadPlcType(byte unitNumber)
        {
            return PackCommand(Encoding.ASCII.GetBytes("MM"), unitNumber);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "unitNumber">unitNumber에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadPlcMode(byte unitNumber)
        {
            return PackCommand(Encoding.ASCII.GetBytes("MS"), unitNumber);
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "unitNumber">unitNumber에 사용할 입력값입니다.</param>
        /// <param name = "mode">mode에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildChangePlcMode(byte unitNumber, byte mode)
        {
            return PackCommand(Encoding.ASCII.GetBytes("SC" + mode.ToString("X2")), unitNumber);
        }
    }
}
