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

namespace FieldLink.PlcDrivers.OpenProtocol
{
    /// <summary>JobData 프로토콜 값입니다.</summary>
    public class TighteningJob
    {
        /// <summary>작업 ID</summary>
        public int JobID { get; set; }
        /// <summary>작업 이름</summary>
        public string JobName { get; set; }
        /// <summary>강제 주문: 0=자유 주문, 1=강제 주문, 2=자유 및 강제 주문</summary>
        public int ForcedOrder { get; set; }
        /// <summary>첫 번째 꽉 잡기 위한 최대 시간</summary>
        public int MaxTimeForFirstTightening { get; set; }
        /// <summary>작업 완료 최대 시간</summary>
        public int MaxTimeToCompleteJob { get; set; }
        /// <summary>작업 배치 모드</summary>
        public int JobBatchMode { get; set; }
        /// <summary>잠금 작업 완료</summary>
        public bool LockAtJobDone { get; set; }
        /// <summary>라인 컨트롤 사용</summary>
        public bool UseLineControl { get; set; }
        /// <summary>반복 작업</summary>
        public bool RepeatJob { get; set; }
        /// <summary>도구 느슨: 0= 활성화, 1= 비활성화, 2= NOK 경착에서만 활성화</summary>
        public int ToolLoosening { get; set; }
        /// <summary>작업 수리용으로 예약. 0=E, 1=G</summary>
        public int Reserved { get; set; }
        /// <summary>파라미터 세트 목록</summary>
        public List<TighteningJobStep> JobList { get; set; }
    }
}
