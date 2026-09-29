using FieldLink.PlcDrivers.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static FieldLink.PlcDrivers.Modbus.ModbusFrameRules;

namespace FieldLink.PlcDrivers.Modbus
{
    /// <summary>Modbus 주소와 읽기·쓰기 코어 명령을 구성합니다. 전송 외피와 송수신은 분리합니다.</summary>
    public static class ModbusCommandBuilder
    {
        /// <summary>1 기반 주소 설정을 0 기반 전송 주소로 변환합니다.</summary>
        private static void CheckModbusAddressStart(ModbusAddress mAddress, bool isStartWithZero)
        {
            if (!isStartWithZero)
            {
                if (mAddress.AddressStart < 1)
                    throw new Exception("1 기반 주소는 1 이상이어야 합니다.");
                mAddress.AddressStart = (ushort)(mAddress.AddressStart - 1);
            }
        }

        /// <summary>워드 120개 또는 비트 2000개 단위로 분할한 읽기 코어 명령을 구성합니다.</summary>
        public static OperationResult<byte[][]> BuildReadModbusCommand(string address, ushort length, byte station, bool isStartWithZero, byte defaultFunction)
        {
            try
            {
                ModbusAddress mAddress = new ModbusAddress(address, station, defaultFunction);
                CheckModbusAddressStart(mAddress, isStartWithZero);
                return BuildReadModbusCommand(mAddress, length);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[][]>(ex.Message);
            }
        }

        /// <summary>읽기와 쓰기를 함께 수행하는 코어 명령을 구성합니다.</summary>
        public static OperationResult<byte[]> BuildReadWriteModbusCommand(string readAddress, ushort length, string writeAddress, byte[] value, byte station, bool isStartWithZero, byte defaultFunction)
        {
            try
            {
                ModbusAddress mAddress = new ModbusAddress(readAddress, station, defaultFunction);
                CheckModbusAddressStart(mAddress, isStartWithZero);
                ModbusAddress mAddress2 = new ModbusAddress(writeAddress, station, defaultFunction);
                CheckModbusAddressStart(mAddress2, isStartWithZero);
                // 이전 구현은 mAddress2.Station을 검사하지 않고 mAddress.Station만 전송했다.
                // R-015: FC23의 읽기·쓰기는 같은 장치에 대한 단일 요청이다. 다른 국번으로의 쓰기를 묵인하지 않는다.
                if (mAddress.Station != mAddress2.Station)
                    return new OperationResult<byte[]>("복합 읽기·쓰기 주소의 국번이 서로 다릅니다.");
                byte[] buffer = new byte[11 + value.Length];
                buffer[0] = (byte)mAddress.Station;
                buffer[1] = (byte)mAddress.Function;
                buffer[2] = BitConverter.GetBytes(mAddress.AddressStart)[1];
                buffer[3] = BitConverter.GetBytes(mAddress.AddressStart)[0];
                buffer[4] = BitConverter.GetBytes(length)[1];
                buffer[5] = BitConverter.GetBytes(length)[0];
                buffer[6] = BitConverter.GetBytes(mAddress2.AddressStart)[1];
                buffer[7] = BitConverter.GetBytes(mAddress2.AddressStart)[0];
                buffer[8] = (byte)(value.Length / 2 / 256);
                buffer[9] = (byte)(value.Length / 2 % 256);
                buffer[10] = (byte)(value.Length);
                value.CopyTo(buffer, 11);
                return OperationResult.CreateSuccessResult(buffer);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>(ex.Message);
            }
        }

        /// <summary>워드 120개 또는 비트 2000개 단위로 분할한 읽기 코어 명령을 구성합니다.</summary>
        public static OperationResult<byte[][]> BuildReadModbusCommand(ModbusAddress mAddress, ushort length)
        {
            List<byte[]> commands = new List<byte[]>();
            OperationResult<int[], int[]> bytes = AddressParameters.SplitReadLength(mAddress.AddressStart, length, (mAddress.Function == ReadCoil || mAddress.Function == ReadDiscrete) ? 2000 : 120);
            for (int i = 0; i < bytes.Content1.Length; i++)
            {
                byte[] buffer = new byte[6];
                buffer[0] = (byte)mAddress.Station;
                buffer[1] = (byte)mAddress.Function;
                buffer[2] = BitConverter.GetBytes(bytes.Content1[i])[1];
                buffer[3] = BitConverter.GetBytes(bytes.Content1[i])[0];
                buffer[4] = BitConverter.GetBytes(bytes.Content2[i])[1];
                buffer[5] = BitConverter.GetBytes(bytes.Content2[i])[0];
                commands.Add(buffer);
            }

            return OperationResult.CreateSuccessResult(commands.ToArray());
        }

        /// <summary>단일 또는 다중 접점 쓰기 코어 명령을 구성합니다.</summary>
        public static OperationResult<byte[]> BuildWriteBoolModbusCommand(string address, bool[] values, byte station, bool isStartWithZero, byte defaultFunction)
        {
            try
            {
                ModbusAddress mAddress = new ModbusAddress(address, station, defaultFunction);
                CheckModbusAddressStart(mAddress, isStartWithZero);
                if (mAddress.Function == ModbusFrameRules.ReadCoil)
                    mAddress.Function = defaultFunction;
                return BuildWriteBoolModbusCommand(mAddress, values);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>(ex.Message);
            }
        }

        /// <summary>단일 또는 다중 접점 쓰기 코어 명령을 구성합니다.</summary>
        public static OperationResult<byte[]> BuildWriteBoolModbusCommand(string address, bool value, byte station, bool isStartWithZero, byte defaultFunction)
        {
            try
            {
                if (address.IndexOf('.') <= 0)
                {
                    ModbusAddress mAddress = new ModbusAddress(address, station, defaultFunction);
                    CheckModbusAddressStart(mAddress, isStartWithZero);
                    if (mAddress.Function == ModbusFrameRules.ReadCoil)
                        mAddress.Function = defaultFunction;
                    return BuildWriteBoolModbusCommand(mAddress, value);
                }
                else
                {
                    int bitIndex = Convert.ToInt32(address.Substring(address.IndexOf('.') + 1));
                    if (bitIndex < 0 || bitIndex > 15)
                        return new OperationResult<byte[]>("레지스터의 비트 인덱스는 0~15여야 합니다.");
                    int orMask = 1 << bitIndex;
                    int andMask = ~orMask;
                    if (!value)
                        orMask = 0;
                    return BuildWriteMaskModbusCommand(address.Substring(0, address.IndexOf('.')), (ushort)andMask, (ushort)orMask, station, isStartWithZero, ModbusFrameRules.WriteMaskRegister);
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>(ex.Message);
            }
        }

        /// <summary>단일 또는 다중 접점 쓰기 코어 명령을 구성합니다.</summary>
        public static OperationResult<byte[]> BuildWriteBoolModbusCommand(ModbusAddress mAddress, bool[] values)
        {
            try
            {
                byte[] data = ProtocolBytes.BoolArrayToByte(values);
                byte[] content = new byte[7 + data.Length];
                content[0] = (byte)mAddress.Station;
                if (mAddress.WriteFunction < 0)
                    content[1] = (byte)mAddress.Function;
                else
                    content[1] = (byte)mAddress.WriteFunction;
                content[2] = BitConverter.GetBytes(mAddress.AddressStart)[1];
                content[3] = BitConverter.GetBytes(mAddress.AddressStart)[0];
                content[4] = (byte)(values.Length / 256);
                content[5] = (byte)(values.Length % 256);
                content[6] = (byte)(data.Length);
                data.CopyTo(content, 7);
                return OperationResult.CreateSuccessResult(content);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>(ex.Message);
            }
        }

        /// <summary>단일 또는 다중 접점 쓰기 코어 명령을 구성합니다.</summary>
        public static OperationResult<byte[]> BuildWriteBoolModbusCommand(ModbusAddress mAddress, bool value)
        {
            byte[] content = new byte[6];
            content[0] = (byte)mAddress.Station;
            if (mAddress.WriteFunction < 0)
                content[1] = (byte)mAddress.Function;
            else
                content[1] = (byte)mAddress.WriteFunction;
            content[2] = BitConverter.GetBytes(mAddress.AddressStart)[1];
            content[3] = BitConverter.GetBytes(mAddress.AddressStart)[0];
            if (value)
            {
                content[4] = 0xFF;
                content[5] = 0x00;
            }
            else
            {
                content[4] = 0x00;
                content[5] = 0x00;
            }

            return OperationResult.CreateSuccessResult(content);
        }

        /// <summary>레지스터 쓰기 코어 명령을 구성합니다.</summary>
        public static OperationResult<byte[]> BuildWriteWordModbusCommand(string address, byte[] values, byte station, bool isStartWithZero, byte defaultFunction)
        {
            try
            {
                ModbusAddress mAddress = new ModbusAddress(address, station, defaultFunction);
                if (mAddress.Function == ModbusFrameRules.ReadRegister)
                    mAddress.Function = defaultFunction;
                CheckModbusAddressStart(mAddress, isStartWithZero);
                return BuildWriteWordModbusCommand(mAddress, values);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>(ex.Message);
            }
        }

        /// <summary>레지스터 쓰기 코어 명령을 구성합니다.</summary>
        public static OperationResult<byte[]> BuildWriteWordModbusCommand(string address, short value, byte station, bool isStartWithZero, byte defaultFunction, IProtocolValueConverter byteTransform)
        {
            try
            {
                ModbusAddress mAddress = new ModbusAddress(address, station, defaultFunction);
                if (mAddress.Function == ModbusFrameRules.ReadRegister)
                    mAddress.Function = defaultFunction;
                if (mAddress.WriteFunction == ModbusFrameRules.WriteRegister || mAddress.Function == ModbusFrameRules.WriteRegister)
                {
                    CheckModbusAddressStart(mAddress, isStartWithZero);
                    byte[] buffer = byteTransform.GetBytes(value);
                    return BuildWriteWordModbusCommand(mAddress, buffer);
                }
                else
                {
                    CheckModbusAddressStart(mAddress, isStartWithZero);
                    return BuildWriteOneRegisterModbusCommand(mAddress, value, byteTransform);
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>(ex.Message);
            }
        }

        /// <summary>레지스터 쓰기 코어 명령을 구성합니다.</summary>
        public static OperationResult<byte[]> BuildWriteWordModbusCommand(string address, ushort value, byte station, bool isStartWithZero, byte defaultFunction, IProtocolValueConverter byteTransform)
        {
            try
            {
                ModbusAddress mAddress = new ModbusAddress(address, station, defaultFunction);
                if (mAddress.Function == ModbusFrameRules.ReadRegister)
                    mAddress.Function = defaultFunction;
                if (mAddress.WriteFunction == ModbusFrameRules.WriteRegister || mAddress.Function == ModbusFrameRules.WriteRegister)
                {
                    CheckModbusAddressStart(mAddress, isStartWithZero);
                    byte[] buffer = byteTransform.GetBytes(value);
                    return BuildWriteWordModbusCommand(mAddress, buffer);
                }
                else
                {
                    CheckModbusAddressStart(mAddress, isStartWithZero);
                    return BuildWriteOneRegisterModbusCommand(mAddress, value, byteTransform);
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>(ex.Message);
            }
        }

        /// <summary>AND·OR 마스크를 적용하는 쓰기 코어 명령을 구성합니다.</summary>
        public static OperationResult<byte[]> BuildWriteMaskModbusCommand(string address, ushort andMask, ushort orMask, byte station, bool isStartWithZero, byte defaultFunction)
        {
            try
            {
                ModbusAddress mAddress = new ModbusAddress(address, station, defaultFunction);
                if (mAddress.Function == ModbusFrameRules.ReadRegister)
                    mAddress.Function = defaultFunction;
                CheckModbusAddressStart(mAddress, isStartWithZero);
                return BuildWriteMaskModbusCommand(mAddress, andMask, orMask);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>(ex.Message);
            }
        }

        /// <summary>레지스터 쓰기 코어 명령을 구성합니다.</summary>
        public static OperationResult<byte[]> BuildWriteWordModbusCommand(ModbusAddress mAddress, byte[] values)
        {
            byte[] content = new byte[7 + values.Length];
            content[0] = (byte)mAddress.Station;
            if (mAddress.WriteFunction < 0)
                content[1] = (byte)mAddress.Function;
            else
                content[1] = (byte)mAddress.WriteFunction;
            content[2] = BitConverter.GetBytes(mAddress.AddressStart)[1];
            content[3] = BitConverter.GetBytes(mAddress.AddressStart)[0];
            content[4] = (byte)(values.Length / 2 / 256);
            content[5] = (byte)(values.Length / 2 % 256);
            content[6] = (byte)(values.Length);
            values.CopyTo(content, 7);
            return OperationResult.CreateSuccessResult(content);
        }

        /// <summary>AND·OR 마스크를 적용하는 쓰기 코어 명령을 구성합니다.</summary>
        public static OperationResult<byte[]> BuildWriteMaskModbusCommand(ModbusAddress mAddress, ushort andMask, ushort orMask)
        {
            byte[] content = new byte[8];
            content[0] = (byte)mAddress.Station;
            content[1] = (byte)mAddress.Function;
            content[2] = BitConverter.GetBytes(mAddress.AddressStart)[1];
            content[3] = BitConverter.GetBytes(mAddress.AddressStart)[0];
            content[4] = BitConverter.GetBytes(andMask)[1];
            content[5] = BitConverter.GetBytes(andMask)[0];
            content[6] = BitConverter.GetBytes(orMask)[1];
            content[7] = BitConverter.GetBytes(orMask)[0];
            return OperationResult.CreateSuccessResult(content);
        }

        /// <summary>한 레지스터를 쓰는 코어 명령을 구성합니다.</summary>
        public static OperationResult<byte[]> BuildWriteOneRegisterModbusCommand(ModbusAddress mAddress, short value, IProtocolValueConverter byteTransform)
        {
            byte[] content = new byte[6];
            content[0] = (byte)mAddress.Station;
            if (mAddress.WriteFunction < 0)
                content[1] = (byte)mAddress.Function;
            else
                content[1] = (byte)mAddress.WriteFunction;
            content[2] = BitConverter.GetBytes(mAddress.AddressStart)[1];
            content[3] = BitConverter.GetBytes(mAddress.AddressStart)[0];
            content[4] = byteTransform.GetBytes(value)[0];
            content[5] = byteTransform.GetBytes(value)[1];
            return OperationResult.CreateSuccessResult(content);
        }

        /// <summary>한 레지스터를 쓰는 코어 명령을 구성합니다.</summary>
        public static OperationResult<byte[]> BuildWriteOneRegisterModbusCommand(ModbusAddress mAddress, ushort value, IProtocolValueConverter byteTransform)
        {
            byte[] content = new byte[6];
            content[0] = (byte)mAddress.Station;
            if (mAddress.WriteFunction < 0)
                content[1] = (byte)mAddress.Function;
            else
                content[1] = (byte)mAddress.WriteFunction;
            content[2] = BitConverter.GetBytes(mAddress.AddressStart)[1];
            content[3] = BitConverter.GetBytes(mAddress.AddressStart)[0];
            content[4] = byteTransform.GetBytes(value)[0];
            content[5] = byteTransform.GetBytes(value)[1];
            return OperationResult.CreateSuccessResult(content);
        }

        /// <summary>국번·기능·쓰기 기능 옵션을 포함한 주소를 해석합니다.</summary>
        public static OperationResult<ModbusAddress> AnalysisAddress(string address, byte defaultStation, bool isStartWithZero, byte defaultFunction)
        {
            try
            {
                ModbusAddress mAddress = new ModbusAddress(address, defaultStation, defaultFunction);
                if (!isStartWithZero)
                {
                    if (mAddress.AddressStart < 1)
                        throw new Exception("1 기반 주소는 1 이상이어야 합니다.");
                    mAddress.AddressStart = (ushort)(mAddress.AddressStart - 1);
                }

                return OperationResult.CreateSuccessResult(mAddress);
            }
            catch (Exception ex)
            {
                return new OperationResult<ModbusAddress>()
                {
                    Message = ex.Message
                };
            }
        }
    }
}
