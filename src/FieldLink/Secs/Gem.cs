using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Secs.Types;

namespace FieldLink.Secs
{
    /// <summary>SECS 요청 계약을 사용하는 기본 GEM 명령입니다. 장비 거절 코드와 잘못된 응답을 구분합니다.</summary>
    public sealed class Gem
    {
        private readonly ISecs secs;
        private readonly Encoding encoding;
        /// <summary>요청 전송과 문자열 인코딩을 주입합니다. 전송의 수명은 소유하지 않습니다.</summary>
        public Gem(ISecs secs, Encoding encoding = null)
        {
            this.secs = secs ?? throw new ArgumentNullException(nameof(secs));
            this.encoding = encoding ?? Encoding.Default;
        }
        /// <summary>S1F1/S1F2로 장비 모델과 버전을 읽습니다.</summary>
        public async Task<OnlineData> AreYouThereAsync(CancellationToken cancellationToken = default(CancellationToken)) =>
            (OnlineData)await ReadAsync(1, 1, new SecsValue(), cancellationToken).ConfigureAwait(false);
        /// <summary>S1F11/S1F12로 상태 변수 이름 목록을 읽습니다. null은 본문 없는 원본 호출 규칙입니다.</summary>
        public async Task<VariableName[]> StatusVariableNamelistAsync(int[] variableIDs = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var value = await ReadAsync(1, 11, variableIDs == null ? new SecsValue() : new SecsValue(variableIDs), cancellationToken).ConfigureAwait(false);
            return value.ToVariableNames();
        }
        /// <summary>S1F13/S1F14로 GEM 통신을 수립합니다. COMMACK 거절은 SecsProtocolException입니다.</summary>
        public async Task<OnlineData> EstablishCommunicationsAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var value = await ReadAsync(1, 13, new SecsValue(), cancellationToken).ConfigureAwait(false);
            var list = SecsValue.RequireList(value);
            if (list.Length != 2)
                throw new InvalidDataException("S1F14 requires COMMACK and equipment identification.");
            byte code = Acknowledgement(list[0]);
            if (code != 0)
                throw new SecsProtocolException("GEM communication establishment was denied.", code);
            return (OnlineData)list[1];
        }
        /// <summary>S1F15/S1F16의 OFLACK를 반환합니다. 0 이외의 장비 코드를 보존합니다.</summary>
        public async Task<byte> OfflineRequestAsync(CancellationToken cancellationToken = default(CancellationToken)) =>
            Acknowledgement(await ReadAsync(1, 15, new SecsValue(), cancellationToken).ConfigureAwait(false));
        /// <summary>S1F17/S1F18의 ONLACK를 반환합니다. 0 이외의 장비 코드를 보존합니다.</summary>
        public async Task<byte> OnlineRequestAsync(CancellationToken cancellationToken = default(CancellationToken)) =>
            Acknowledgement(await ReadAsync(1, 17, new SecsValue(), cancellationToken).ConfigureAwait(false));
        /// <summary>S2F13/S2F14로 장비 상수를 요청합니다. null 목록은 빈 List로 보냅니다.</summary>
        public Task<SecsValue> EquipmentConstantRequestAsync(object[] constantIDs = null,
            CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadAsync(2, 13, new SecsValue(constantIDs), cancellationToken);
        private async Task<SecsValue> ReadAsync(byte stream, byte function, SecsValue data, CancellationToken token)
        {
            var reply = await secs.RequestAsync(stream, function, data, token).ConfigureAwait(false);
            if (reply == null || reply.SessionType != 0 || reply.PresentationType != 0 || reply.W ||
                reply.StreamNo != stream || reply.FunctionNo != function + 1)
                throw new InvalidDataException("Unexpected GEM response header.");
            return reply.GetItemValues(encoding);
        }
        private static byte Acknowledgement(SecsValue value)
        {
            if (value.ItemType != SecsItemType.Binary || value.Length != 1)
                throw new InvalidDataException("Expected one binary acknowledgement byte.");
            return ((byte[])value.Value)[0];
        }
    }
}
