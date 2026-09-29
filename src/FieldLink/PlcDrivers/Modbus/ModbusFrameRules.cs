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

namespace FieldLink.PlcDrivers.Modbus
{
    /// <summary>ModbusFrameRules 프로토콜 값입니다.</summary>
    public static class ModbusFrameRules
    {
        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "modbusAscii">modbusAscii에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool CheckAsciiReceiveDataComplete(byte[] modbusAscii)
        {
            return CheckAsciiReceiveDataComplete(modbusAscii, modbusAscii.Length);
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "modbusAscii">modbusAscii에 사용할 입력값입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool CheckAsciiReceiveDataComplete(byte[] modbusAscii, int length)
        {
            if (length > 5)
                return modbusAscii[0] == 0x3A && modbusAscii[length - 2] == 0x0D && modbusAscii[length - 1] == 0x0A;
            else
                return false;
        }

        /// <summary>ReadCoil 프로토콜 값입니다.</summary>
        public const byte ReadCoil = 0x01;
        /// <summary>ReadDiscrete 프로토콜 값입니다.</summary>
        public const byte ReadDiscrete = 0x02;
        /// <summary>ReadRegister 프로토콜 값입니다.</summary>
        public const byte ReadRegister = 0x03;
        /// <summary>ReadInputRegister 프로토콜 값입니다.</summary>
        public const byte ReadInputRegister = 0x04;
        /// <summary>WriteOneCoil 프로토콜 값입니다.</summary>
        public const byte WriteOneCoil = 0x05;
        /// <summary>WriteOneRegister 프로토콜 값입니다.</summary>
        public const byte WriteOneRegister = 0x06;
        /// <summary>WriteCoil 프로토콜 값입니다.</summary>
        public const byte WriteCoil = 0x0F;
        /// <summary>WriteRegister 프로토콜 값입니다.</summary>
        public const byte WriteRegister = 0x10;
        /// <summary>WriteMaskRegister 프로토콜 값입니다.</summary>
        public const byte WriteMaskRegister = 0x16;
        /// <summary>ReadWrite 프로토콜 값입니다.</summary>
        public const byte ReadWrite = 0x17;
        /// <summary>FunctionCodeNotSupport 프로토콜 값입니다.</summary>
        public const byte FunctionCodeNotSupport = 0x01;
        /// <summary>FunctionCodeOverBound 프로토콜 값입니다.</summary>
        public const byte FunctionCodeOverBound = 0x02;
        /// <summary>FunctionCodeQuantityOver 프로토콜 값입니다.</summary>
        public const byte FunctionCodeQuantityOver = 0x03;
        /// <summary>FunctionCodeReadWriteException 프로토콜 값입니다.</summary>
        public const byte FunctionCodeReadWriteException = 0x04;
        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ExtractActualData(byte[] response)
        {
            try
            {
                if (response[1] >= 0x80)
                    return new OperationResult<byte[]>(err: response[2], msg: ModbusFrameRules.GetDescriptionByErrorCode(response[2]));
                else if (response.Length > 3)
                    return OperationResult.CreateSuccessResult(ProtocolBytes.ArrayRemoveBegin(response, 3));
                else
                    return OperationResult.CreateSuccessResult(new byte[0]);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>(ex.Message);
            }
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "modbus">modbus에 사용할 입력값입니다.</param>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandToTcp(byte[] modbus, ushort id)
        {
            if (modbus == null)
                throw new ArgumentNullException(nameof(modbus));
            // Unit ID (1) + PDU (1..253), Application Protocol V1.1b3 §4.1.
            if (modbus.Length < 2 || modbus.Length > 254)
                throw new ArgumentOutOfRangeException(nameof(modbus), "Modbus TCP 코어 길이는 2~254바이트여야 합니다.");
            byte[] buffer = new byte[modbus.Length + 6];
            buffer[0] = BitConverter.GetBytes(id)[1];
            buffer[1] = BitConverter.GetBytes(id)[0];
            buffer[4] = BitConverter.GetBytes(modbus.Length)[1];
            buffer[5] = BitConverter.GetBytes(modbus.Length)[0];
            modbus.CopyTo(buffer, 6);
            return buffer;
        }

        /// <summary>ExplodeTcpCommandToCore 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "modbusTcp">modbusTcp에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] ExplodeTcpCommandToCore(byte[] modbusTcp) => modbusTcp.RemoveBegin(6);
        /// <summary>GetDescriptionByErrorCode 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetDescriptionByErrorCode(byte code)
        {
            switch (code)
            {
                case ModbusFrameRules.FunctionCodeNotSupport:
                    return ProtocolMessages.ModbusTcpFunctionCodeNotSupport;
                case ModbusFrameRules.FunctionCodeOverBound:
                    return ProtocolMessages.ModbusTcpFunctionCodeOverBound;
                case ModbusFrameRules.FunctionCodeQuantityOver:
                    return ProtocolMessages.ModbusTcpFunctionCodeQuantityOver;
                case ModbusFrameRules.FunctionCodeReadWriteException:
                    return ProtocolMessages.ModbusTcpFunctionCodeReadWriteException;
                default:
                    return ProtocolMessages.UnknownError;
            }
        }
    }
}
