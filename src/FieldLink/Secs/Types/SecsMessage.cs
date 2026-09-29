using System;
using System.Text;

namespace FieldLink.Secs.Types
{
    /// <summary>HSMS의 10바이트 헤더와 SECS-II 본문입니다. 본문 배열은 생성 시 복사합니다.</summary>
    public sealed class SecsMessage
    {
        private readonly byte[] data;
        /// <summary>데이터 또는 제어 메시지를 생성합니다. 제어 메시지에서는 function이 상태 코드를 담을 수 있습니다.</summary>
        public SecsMessage(ushort deviceID, byte stream, byte function, uint messageID,
            byte[] data = null, bool replyExpected = false, byte sessionType = 0, byte presentationType = 0)
        {
            if (stream > 127)
                throw new ArgumentOutOfRangeException(nameof(stream));
            DeviceID = deviceID;
            StreamNo = stream;
            FunctionNo = function;
            MessageID = messageID;
            W = replyExpected;
            SessionType = sessionType;
            PresentationType = presentationType;
            this.data = data == null ? new byte[0] : (byte[])data.Clone();
        }
        /// <summary>16비트 HSMS 세션 ID입니다. SECS-I의 방향 비트를 적용하지 않습니다.</summary>
        public ushort DeviceID { get; }
        /// <summary>스트림 번호입니다.</summary>
        public byte StreamNo { get; }
        /// <summary>함수 번호 또는 제어 응답 상태입니다.</summary>
        public byte FunctionNo { get; }
        /// <summary>응답을 요구하는 W 비트입니다.</summary>
        public bool W { get; }
        /// <summary>요청과 응답을 연결하는 System Bytes입니다.</summary>
        public uint MessageID { get; }
        /// <summary>HSMS SType입니다. 데이터는 0입니다.</summary>
        public byte SessionType { get; }
        /// <summary>HSMS PType입니다. SECS-II는 0입니다.</summary>
        public byte PresentationType { get; }
        /// <summary>독립적인 본문 복사본입니다.</summary>
        public byte[] Data => (byte[])data.Clone();
        internal byte[] Body => data;
        /// <summary>시스템 기본 문자열 인코딩으로 본문을 해석합니다.</summary>
        public SecsValue GetItemValues() => GetItemValues(Encoding.Default);
        /// <summary>지정한 문자열 인코딩으로 본문을 해석합니다.</summary>
        public SecsValue GetItemValues(Encoding encoding) => SecsValue.ParseFromSource(data, encoding);
        /// <inheritdoc />
        public override string ToString() => "S" + StreamNo + "F" + FunctionNo + (W ? " W" : "") + " #" + MessageID;
    }
}
