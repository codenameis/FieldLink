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
    /// <summary>옴의 CPU의 단위 정보 데이터 클래스</summary>
    public class OmronCpuUnitData
    {
        /// <summary>기본 객체를 생성합니다.</summary>
        public OmronCpuUnitData()
        {
        }

        /// <summary>원본 데이터에 따라 CPU 유닛 정보를 인스턴스화합니다.</summary>
        /// <param name = "data">원본 바이트 수</param>
        public OmronCpuUnitData(byte[] data)
        {
            Model = Encoding.ASCII.GetString(data, 0, 20).Trim(new char[] { ' ' });
            Version = Encoding.ASCII.GetString(data, 20, 10).Trim(new char[] { ' ', '\0' });
            LargestEMNumber = data[41];
            ProgramAreaSize = data[80] * 256 + data[81];
            IOMSize = data[82] * 1024;
            DMSize = data[83] * 256 + data[84];
            TCSize = data[85] * 1024;
            EMSize = data[86];
        }

        /// <summary>CPU 유닛 모델</summary>
        public string Model { get; set; }
        /// <summary>CPU 유닛 내부 시스템 버전</summary>
        public string Version { get; set; }
        /// <summary>가장 큰 숫자, 0에서 19까지, CPU 유닛의 EM 영역에서.</summary>
        public int LargestEMNumber { get; set; }
        /// <summary>사용할 수 있는 프로그램 영역의 최대 크기, 단위: k 워드</summary>
        public int ProgramAreaSize { get; set; }
        /// <summary>비트 명령을 사용할 수 있는 영역(CIO, WR, HR, AR, 타이머/카운터 완료 플래그, TN)의 크기입니다. 항상 23이며 단위는 바이트입니다.</summary>
        public int IOMSize { get; set; }
        /// <summary>DM 지역에서의 워드 총수 (항상 32,768)</summary>
        public int DMSize { get; set; }
        /// <summary>EM 지역의 뱅크들 중에서, 파일 메모리가 없는 뱅크들의 수는</summary>
        /// <remarks>Banks (1 bank = 32,768 words)</remarks>
        public int EMSize { get; set; }
        /// <summary>사용 가능한 최대 타이머/ 카운터 수 (항상 8)</summary>
        public int TCSize { get; set; }
    }
}
