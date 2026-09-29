using FieldLink.PlcDrivers.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FieldLink.Robot.Estun.Protocols
{
    /// <summary>Estun의 100워드 상태 영역을 보관합니다. 입력 배열은 최소 200바이트여야 합니다.</summary>
    public class EstunControllerState
    {
        /// <summary>Estun의 100워드 상태 영역을 보관합니다. 입력 배열은 최소 200바이트여야 합니다.</summary>
        public EstunControllerState()
        {
        }

        /// <summary>Estun의 100워드 상태 영역을 보관합니다. 입력 배열은 최소 200바이트여야 합니다.</summary>
        public EstunControllerState(byte[] source, IProtocolValueConverter byteTransform)
        {
            LoadBySourceData(source, byteTransform);
        }

        /// <summary>
        /// 현재 수동 동작 모드를 가져오거나 설정합니다.
        /// </summary>
        public bool ManualMode { get; set; }
        /// <summary>
        /// 현재 자동화 모드를 가져오거나 설정합니다.
        /// </summary>
        public bool AutoMode { get; set; }
        /// <summary>
        /// 현재 원격 운영 모드를 가져오거나 설정합니다.
        /// </summary>
        public bool RemoteMode { get; set; }
        /// <summary>로봇 활성 상태입니다.</summary>
        public bool EnableStatus { get; set; }
        /// <summary>
        /// 실행 상태를 가져오거나 설정합니다.
        /// </summary>
        public bool RunStatus { get; set; }
        /// <summary>오류 발생 상태입니다.</summary>
        public bool ErrorStatus { get; set; }
        /// <summary>
        /// 프로그램 실행 상태를 가져오거나 설정
        /// </summary>
        public bool ProgramRunStatus { get; set; }
        /// <summary>로봇 이동 상태입니다.</summary>
        public bool RobotMoving { get; set; }
        /// <summary>
        /// 현재 로드된 프로젝트 이름을 가져오거나 설정합니다.
        /// </summary>
        public string ProjectName { get; set; }
        /// <summary>
        /// SimDout, 64비트 길이
        /// </summary>
        public bool[] DO { get; set; }
        /// <summary>16비트 명령 실행 상태 값입니다.</summary>
        public ushort RobotCommandStatus { get; set; }
        /// <summary>아날로그 출력 16개입니다.</summary>
        public float[] AO { get; set; }
        /// <summary>전역 속도 설정 값입니다.</summary>
        public short GlobalSpeedValue { get; set; }
        /// <summary>
        /// SimDI, 총 64비트
        /// </summary>
        public bool[] DI { get; set; }
        /// <summary>아날로그 입력 16개입니다.</summary>
        public float[] AI { get; set; }
        /// <summary>읽기·쓰기 제어 플래그입니다.</summary>
        public short ReadWriteFlag { get; set; }

        /// <summary>200바이트 상태 본문을 지정 변환기로 해석합니다. 문자열과 디지털 I/O는 워드 내부 바이트를 뒤집습니다.</summary>
        public void LoadBySourceData(byte[] source, IProtocolValueConverter byteTransform)
        {
            ManualMode = source[7].GetBoolByIndex(0);
            AutoMode = source[7].GetBoolByIndex(1);
            RemoteMode = source[7].GetBoolByIndex(2);
            EnableStatus = source[7].GetBoolByIndex(3);
            RunStatus = source[7].GetBoolByIndex(4);
            ErrorStatus = source[7].GetBoolByIndex(5);
            ProgramRunStatus = source[7].GetBoolByIndex(6);
            RobotMoving = source[7].GetBoolByIndex(7);
            GlobalSpeedValue = byteTransform.ReadInt16(source, 2);
            ProjectName = Encoding.ASCII.GetString(ProtocolBytes.BytesReverseByWord(source.SelectMiddle(8, 20))).TrimEnd('\u0000');
            DO = ProtocolBytes.BytesReverseByWord(source.SelectMiddle(28, 8)).ToBoolArray();
            RobotCommandStatus = byteTransform.ReadUInt16(source, 36);
            AO = byteTransform.ReadSingle(source, 38, 16);
            DI = ProtocolBytes.BytesReverseByWord(source.SelectMiddle(126, 8)).ToBoolArray();
            AI = byteTransform.ReadSingle(source, 134, 16);
            ReadWriteFlag = byteTransform.ReadInt16(source, 198);
        }
    }
}
