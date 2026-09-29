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

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>로크웰 PLC의 주소 정보</summary>
    public class AllenBradleySlcAddress : DeviceAddress
    {
        /// <summary>데이터를 가져오기 위한 코드, 또는 읽기를 대기하도록 설정</summary>
        public byte DataCode { get; set; }
        /// <summary>PLC의 DB 블록 데이터 정보를 가져오거나 설정합니다.</summary>
        public ushort DbBlock { get; set; }

        /// <summary>정해진 주소 정보에서 실제 장치 주소 정보로 분해</summary>
        /// <param name = "address">주소 정보</param>
        /// <param name = "length">데이터 길이</param>
        public override void Parse(string address, ushort length)
        {
            OperationResult<AllenBradleySlcAddress> addressData = ParseFrom(address, length);
            if (!addressData.IsSuccess)
                throw new FormatException(addressData.Message);
            AddressStart = addressData.Content.AddressStart;
            Length = addressData.Content.Length;
            DataCode = addressData.Content.DataCode;
            DbBlock = addressData.Content.DbBlock;
        }

        /// <summary>현재 프로토콜 값의 문자열 표현을 반환합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public override string ToString()
        {
            switch (DataCode)
            {
                case 0x8E:
                    return $"A{DbBlock}:{AddressStart}";
                case 0x85:
                    return $"B{DbBlock}:{AddressStart}";
                case 0x89:
                    return $"N{DbBlock}:{AddressStart}";
                case 0x8A:
                    return $"F{DbBlock}:{AddressStart}";
                case 0x8D:
                    return $"ST{DbBlock}:{AddressStart}";
                case 0x84:
                    return $"S{DbBlock}:{AddressStart}";
                case 0x87:
                    return $"C{DbBlock}:{AddressStart}";
                case 0x83:
                    return $"I{DbBlock}:{AddressStart}";
                case 0x82:
                    return $"O{DbBlock}:{AddressStart}";
                case 0x88:
                    return $"R{DbBlock}:{AddressStart}";
                case 0x86:
                    return $"T{DbBlock}:{AddressStart}";
                case 0x91:
                    return $"L{DbBlock}:{AddressStart}";
            }

            return AddressStart.ToString();
        }

        /// <summary>실제 로크웰의 주소에서 A9:0과 같은 주소 객체를 파싱합니다.</summary>
        /// <param name = "address">실제 주소 데이터 정보, 예를 들어 A9:0</param>
        /// <returns>성공 여부를 나타내는 결과 객체</returns>
        public static OperationResult<AllenBradleySlcAddress> ParseFrom(string address)
        {
            return ParseFrom(address, 0);
        }

        /// <summary>실제 로크웰의 주소에서 A9:0과 같은 주소 객체를 파싱합니다.</summary>
        /// <param name = "address">실제 주소 데이터 정보, 예를 들어 A9:0</param>
        /// <param name = "length">읽을 데이터 길이</param>
        /// <returns>성공 여부를 나타내는 결과 객체</returns>
        public static OperationResult<AllenBradleySlcAddress> ParseFrom(string address, ushort length)
        {
            if (!address.Contains(":"))
                return new OperationResult<AllenBradleySlcAddress>("Address can't find ':', example : A9:0");
            string[] adds = address.Split(new char[] { ':' });
            try
            {
                AllenBradleySlcAddress allenBradleySLC = new AllenBradleySlcAddress();
                //OperateResult<byte, ushort, ushort> result = new OperateResult<byte, ushort, ushort>( );
                // 그리고 0x8f BCD가 있습니다.
                switch (adds[0][0])
                {
                    case 'A':
                        allenBradleySLC.DataCode = 0x8E;
                        break; // ASCII
                    case 'B':
                        allenBradleySLC.DataCode = 0x85;
                        break; // 비트
                    case 'N':
                        allenBradleySLC.DataCode = 0x89;
                        break; // 정수
                    case 'F':
                        allenBradleySLC.DataCode = 0x8A;
                        break; // 부동 소수점
                    case 'S':
                    {
                        if (adds[0].Length > 1 && adds[0][1] == 'T')
                            allenBradleySLC.DataCode = 0x8D; // string
                        else
                            allenBradleySLC.DataCode = 0x84; // 상태
                        break;
                    }

                    case 'C':
                        allenBradleySLC.DataCode = 0x87;
                        break; // 카운터
                    case 'I':
                        allenBradleySLC.DataCode = 0x83;
                        break; // 입력
                    case 'O':
                        allenBradleySLC.DataCode = 0x82;
                        break; // 출력
                    case 'R':
                        allenBradleySLC.DataCode = 0x88;
                        break; // 제어
                    case 'T':
                        allenBradleySLC.DataCode = 0x86;
                        break; // 타이머
                    case 'L':
                        allenBradleySLC.DataCode = 0x91;
                        break; // 긴 정수
                    default:
                        throw new Exception("Address code wrong, must be A,B,N,F,S,C,I,O,R,T,ST,L");
                };
                switch (allenBradleySLC.DataCode)
                {
                    case 0x84:
                        allenBradleySLC.DbBlock = adds[0].Length == 1 ? (ushort)2 : ushort.Parse(adds[0].Substring(1));
                        break;
                    case 0x82:
                        allenBradleySLC.DbBlock = adds[0].Length == 1 ? (ushort)0 : ushort.Parse(adds[0].Substring(1));
                        break;
                    case 0x83:
                        allenBradleySLC.DbBlock = adds[0].Length == 1 ? (ushort)1 : ushort.Parse(adds[0].Substring(1));
                        break;
                    case 0x8D:
                        allenBradleySLC.DbBlock = adds[0].Length == 2 ? (ushort)1 : ushort.Parse(adds[0].Substring(2));
                        break;
                    default:
                        allenBradleySLC.DbBlock = ushort.Parse(adds[0].Substring(1));
                        break;
                }

                allenBradleySLC.AddressStart = ushort.Parse(adds[1]);
                return OperationResult.CreateSuccessResult(allenBradleySLC);
            }
            catch (Exception ex)
            {
                return new OperationResult<AllenBradleySlcAddress>("Wrong Address format: " + ex.Message);
            }
        }
    }
}
