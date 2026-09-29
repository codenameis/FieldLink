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

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>S7Session 데이터의 프로토콜 표현을 생성하고 해석합니다.</summary>
    public static class S7SessionCodec
    {
        /// <summary>PLC 기종과 선택적인 TSAP 값으로 연결 프레임을 만듭니다.</summary>
        /// <param name = "model">문자열 메모리 형식을 결정하는 Siemens PLC 기종입니다.</param>
        /// <param name = "rackSlot">rackSlot에 사용할 입력값입니다.</param>
        /// <param name = "localTsap">localTsap에 사용할 입력값입니다.</param>
        /// <param name = "remoteTsap">remoteTsap에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <remarks>rackSlot을 지정하면 원본 Rack/Slot 속성처럼 기종 기본값을 덮어씁니다.</remarks>
        public static byte[] BuildConnection(SiemensPLCS model, byte? rackSlot = null, ushort? localTsap = null, ushort? remoteTsap = null)
        {
            byte[] frame = S7SessionCommandBuilder.BuildDefaultConnection();
            switch (model)
            {
                case SiemensPLCS.S1200:
                    frame[21] = 0;
                    break;
                case SiemensPLCS.S300:
                    frame[21] = 2;
                    break;
                case SiemensPLCS.S400:
                    frame[21] = 3;
                    frame[17] = 0;
                    break;
                case SiemensPLCS.S1500:
                    frame[21] = 0;
                    break;
                case SiemensPLCS.S200Smart:
                    frame = S7SessionCommandBuilder.BuildS200SmartConnection();
                    break;
                case SiemensPLCS.S200:
                    frame = S7SessionCommandBuilder.BuildS200Connection();
                    break;
                default:
                    frame[18] = 0;
                    break;
            }

            bool small = model == SiemensPLCS.S200 || model == SiemensPLCS.S200Smart;
            if (rackSlot.HasValue && !small)
                frame[21] = rackSlot.Value;
            int local = small ? 13 : 16, remote = small ? 17 : 20;
            if (localTsap.HasValue)
            {
                frame[local] = (byte)(localTsap.Value >> 8);
                frame[local + 1] = (byte)localTsap.Value;
            }

            if (remoteTsap.HasValue)
            {
                frame[remote] = (byte)(remoteTsap.Value >> 8);
                frame[remote + 1] = (byte)remoteTsap.Value;
            }

            return frame;
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "model">문자열 메모리 형식을 결정하는 Siemens PLC 기종입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildSetup(SiemensPLCS model) => model == SiemensPLCS.S200 ? S7SessionCommandBuilder.BuildS200Setup() : model == SiemensPLCS.S200Smart ? S7SessionCommandBuilder.BuildS200SmartSetup() : S7SessionCommandBuilder.BuildDefaultSetup();
        /// <summary>Setup Communication 응답에서 단일 항목 바이트 쓰기의 최대 데이터 길이를 계산합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <exception cref="InvalidDataException">협상 응답이 잘못되었거나 장치 오류가 반환되었습니다.</exception>
        /// <remarks>TPKT/COTP 7바이트를 제외한 S7 쓰기 헤더는 28바이트입니다. 다중 항목은 추가 오버헤드를 별도로 계산해야 합니다.</remarks>
        public static int ParsePayloadLimit(byte[] response)
        {
            OperationResult validation = S7ResponseValidation.ValidateEnvelope(response);
            if (!validation.IsSuccess)
                throw new InvalidDataException(validation.Message + " (0x" + validation.ErrorCode.ToString("X4") + ")");
            if (response.Length != 27 || response[14] != 8 || response[16] != 0 || response[19] != 0xF0 ||
                response[20] != 0 || (response[21] == 0 && response[22] == 0) ||
                (response[23] == 0 && response[24] == 0))
                throw new InvalidDataException("Setup Communication 응답의 매개변수가 올바르지 않습니다.");
            int negotiatedPdu = response[25] * 256 + response[26];
            if (negotiatedPdu <= 28)
                throw new InvalidDataException("협상된 PDU에 쓰기 데이터 공간이 없습니다.");
            // 협상값을 임의로 최소 200으로 올리면 PLC가 허용한 PDU보다 큰 요청을 만들 수 있다.
            return negotiatedPdu - 28;
        }
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string ParseOrderNumber(byte[] response) => Encoding.ASCII.GetString(response, 71, 20);
    }
}
