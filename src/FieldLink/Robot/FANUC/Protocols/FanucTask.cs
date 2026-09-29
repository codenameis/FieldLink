using FieldLink.PlcDrivers.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FieldLink.Robot.FANUC.Protocols
{
    /// <summary>FANUC의 36바이트 프로그램 작업 레코드입니다.</summary>
    public class FanucTask
    {
        /// <summary>현재 프로그램 이름입니다.</summary>
        public string ProgramName { get; set; }
        /// <summary>실행 행 번호입니다.</summary>
        public short LineNumber { get; set; }
        /// <summary>프로그램 실행 상태 값입니다.</summary>
        public short State { get; set; }
        /// <summary>호출한 상위 프로그램 이름입니다.</summary>
        public string ParentProgramName { get; set; }

        /// <summary>지정 위치의 프로그램 이름·행·상태·상위 프로그램을 읽습니다.</summary>
        public void LoadByContent(IProtocolValueConverter byteTransform, byte[] content, int index, Encoding encoding)
        {
            ProgramName = encoding.GetString(content, index, 16).Trim('\u0000');
            LineNumber = BitConverter.ToInt16(content, index + 16);
            State = BitConverter.ToInt16(content, index + 18);
            ParentProgramName = encoding.GetString(content, index + 20, 16).Trim('\u0000');
        }

        /// <inheritdoc/>
        public override string ToString() => $"ProgramName[{ProgramName}] LineNumber[{LineNumber}] State[{State}] ParentProgramName[{ParentProgramName}]";
        /// <summary>지정 위치의 작업 레코드를 새 객체로 해석합니다.</summary>
        public static FanucTask ParseFrom(IProtocolValueConverter byteTransform, byte[] content, int index, Encoding encoding)
        {
            FanucTask fanucTask = new FanucTask();
            fanucTask.LoadByContent(byteTransform, content, index, encoding);
            return fanucTask;
        }
    }
}
