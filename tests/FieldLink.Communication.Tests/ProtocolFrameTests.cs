using FieldLink.PlcDrivers.Modbus;
using System.Text;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.AllenBradley;
using FieldLink.PlcDrivers.Beckhoff;
using FieldLink.PlcDrivers.FATEK;
using FieldLink.PlcDrivers.Fuji;
using FieldLink.PlcDrivers.GE;
using FieldLink.PlcDrivers.Geniitek;
using FieldLink.PlcDrivers.IDCard;
using FieldLink.PlcDrivers.Keyence;
using FieldLink.PlcDrivers.LSIS;
using FieldLink.PlcDrivers.Melsec;
using FieldLink.PlcDrivers.Omron;
using FieldLink.PlcDrivers.OpenProtocol;
using FieldLink.PlcDrivers.Siemens;
using FieldLink.PlcDrivers.Toyota;
using FieldLink.PlcDrivers.Turck;
using FieldLink.PlcDrivers.Vigor;
using FieldLink.PlcDrivers.YASKAWA;
using FieldLink.PlcDrivers.Yokogawa;
using FieldLink.Robot.EFORT.Protocols;
using FieldLink.Robot.FANUC.Protocols;
using FieldLink.Robot.KUKA.Protocols;

namespace FieldLink.Communication.Tests;

internal static partial class ProtocolFrameTests
{
    // 길이 필드만 검사하는 합성 헤더다. 유효한 장치 응답 전체를 나타내지는 않는다.
    private static (IProtocolFrameRules Parser, byte[] Header, int Length)[] LengthCases() =>
    [
        (new AdsTcpFrameRules(), H("000004010000"), 260),
        (new EtherNetIpFrameRules(), Header(24, (2, "0401")), 260),
        (new AllenBradleySlcFrameRules(), Header(28, (2, "0104")), 260),
        (new FujiCommandSettingTypeFrameRules(), H("0000000004"), 4),
        (new FujiSpbFrameRules(), A(":010A"), 22),
        (new FujiSphFrameRules(), Header(20, (18, "0401")), 260),
        (new GeSrtpFrameRules(), Header(56, (4, "0401")), 260),
        (new VibrationSensorShortFrameRules(), Header(9, (0, "AA")), 0),
        (new VibrationSensorLongFrameRules(), Header(12, (0, "AA557F"), (10, "0100")), 260),
        (new SamFrameRules(), H("AAAAAA96690104"), 260),
        (new LsisFastEnetFrameRules(), Header(20, (0, "4C"), (14, "3412"), (16, "0401")), 260),
        (new MelsecQnA3EBinaryFrameRules(), H("D00000FFFF03000401"), 260),
        (new MelsecQnA3EAsciiFrameRules(), A("D00000FF03FF000104"), 260),
        (new S7FrameRules(), H("03000108"), 260),
        (new SiemensPpiServerFrameRules(), H("680404680102"), 4),
        (new OpenProtocolFrameRules(), A("0024"), 21),
        (new TurckReaderFrameRules(), H("AA0007"), 4),
        (new ToyoPucFrameRules(), H("00000401"), 260),
        (new MemobusFrameRules(), Header(12, (6, "1001")), 260),
        (new YokogawaLinkBinaryFrameRules(), H("11000104"), 260),
        (new ModbusTcpFrameRules(), H("1234000000060103"), 4),
        (new FinsTcpFrameRules(), H("46494E530000010C0000000200000000"), 260),
        (new EfortFrameRules(), Header(18, (16, "1601")), 260),
        (new EfortLegacyFrameRules(), Header(17, (15, "1501")), 260),
        (new FanucFrameRules(), Header(56, (4, "0401")), 260),
        (new KukaVarProxyFrameRules(), H("12340104"), 260)
    ];

    internal static Task FixedHeaderLengthsAndPartialHeadersAsync()
    {
        foreach (var item in LengthCases())
        {
            IProtocolFrameRules parser = item.Parser;
            TestAssert.Equal(parser.HeaderLength, item.Header.Length);
            TestAssert.Equal(item.Length, parser.GetBodyLength(item.Header));
            var boundary = new HeaderLengthFrame(item.Header.Length, bytes =>
            {
                return parser.HeaderLength + parser.GetBodyLength(bytes.ToArray());
            });
            for (int count = 0; count < item.Header.Length; count++)
                TestAssert.True(boundary.GetFrameLength(new ArraySegment<byte>(item.Header, 0, count)) == null,
                    $"{parser.GetType().Name}: 헤더 {count}바이트를 완성 프레임으로 해석했습니다.");
            TestAssert.Equal(item.Header.Length + item.Length, boundary.GetFrameLength(new ArraySegment<byte>(item.Header))!.Value);
        }
        return Task.CompletedTask;
    }

    internal static Task SignaturesRejectIncorrectHeadersAsync()
    {
        Type[] checkedTypes = [typeof(S7FrameRules), typeof(MelsecQnA3EBinaryFrameRules), typeof(MelsecQnA3EAsciiFrameRules),
            typeof(SamFrameRules), typeof(VibrationSensorShortFrameRules), typeof(VibrationSensorLongFrameRules),
            typeof(LsisFastEnetFrameRules), typeof(TurckReaderFrameRules), typeof(FinsTcpFrameRules)];
        foreach (var item in LengthCases().Where(item => checkedTypes.Contains(item.Parser.GetType())))
        {
            TestAssert.True(item.Parser.IsHeaderValid(item.Header), item.Parser.GetType().Name);
            item.Header[0] ^= 0xFF;
            TestAssert.True(!item.Parser.IsHeaderValid(item.Header), item.Parser.GetType().Name);
        }
        return Task.CompletedTask;
    }

    internal static async Task LengthLimitsAndFinsResynchronizationAsync()
    {
        // 이전 기대값: 잘못된 ADS 길이는 0, 10001은 10000으로 보정했다.
        // FINS도 7은 8로, 10001은 10000으로 보정했다. R-003 수정으로 거부 또는 실제 길이를 기대한다.
        byte[] adsHeader = H("0000FFFFFFFF");
        var ads = new AdsTcpFrameRules();
        await TestAssert.ThrowsAsync<InvalidDataException>(() => Task.FromResult(ads.GetBodyLength(adsHeader)));
        adsHeader = H("000011270000");
        TestAssert.Equal(10001, ads.GetBodyLength(adsHeader));
        byte[] finsHeader = Header(16, (4, "00000007"));
        var fins = new FinsTcpFrameRules();
        await TestAssert.ThrowsAsync<InvalidDataException>(() => Task.FromResult(fins.GetBodyLength(finsHeader)));
        finsHeader = Header(16, (4, "00002711"));
        TestAssert.Equal(9993, fins.GetBodyLength(finsHeader));
        TestAssert.Equal(3, fins.FindHeaderOffset(H("FF000046494E5300000010")));
        TestAssert.Equal(0, new S7FrameRules().GetBodyLength(H("03000003")));
        // 원본 호환 동작: 숫자가 아닌 헤더는 본문 17바이트로 대체된다. 형식 검증 성공을 뜻하지 않는다.
        TestAssert.Equal(17, new OpenProtocolFrameRules().GetBodyLength(A("abcd")));
        TestAssert.Equal(0, new MemobusFrameRules().GetBodyLength(new byte[12]));
    }

    internal static Task AdsAndFinsLengthsKeepTheirDeclaredBoundariesAsync()
    {
        foreach (int length in new[] { 10000, 10001, 12000 })
        {
            byte[] adsHeader = [0, 0, (byte)length, (byte)(length >> 8), 0, 0];
            var ads = new AdsTcpFrameRules();
            TestAssert.Equal(length, ads.GetBodyLength(adsHeader));
            byte[] finsHeader = Header(16, (4, length.ToString("X8")));
            var fins = new FinsTcpFrameRules();
            TestAssert.Equal(length - 8, fins.GetBodyLength(finsHeader));
        }
        // 빈 본문을 가지는 외피도 정확한 길이를 보존한다. 명령별 유효성은 응답 파서의 책임이다.
        TestAssert.Equal(0, new AdsTcpFrameRules().GetBodyLength(new byte[6]));
        TestAssert.Equal(0, new FinsTcpFrameRules().GetBodyLength(Header(16, (4, "00000008"))));
        return Task.CompletedTask;
    }

    internal static async Task AdsAndFinsRejectLengthOverflowAsync()
    {
        foreach (string length in new[] { "FAFFFF7F", "FFFFFF7F", "00000080", "FFFFFFFF" })
        {
            byte[] adsHeader = H("0000" + length);
            var ads = new AdsTcpFrameRules();
            await TestAssert.ThrowsAsync<InvalidDataException>(() => Task.FromResult(ads.GetBodyLength(adsHeader)));
        }
        foreach (string length in new[] { "00000000", "00000007", "7FFFFFF8", "80000000", "FFFFFFFF" })
        {
            byte[] finsHeader = Header(16, (4, length));
            var fins = new FinsTcpFrameRules();
            await TestAssert.ThrowsAsync<InvalidDataException>(() => Task.FromResult(fins.GetBodyLength(finsHeader)));
        }
    }

    internal static Task MelsecA1ELengthDependsOnRequestAsync()
    {
        byte[] binaryHeader = H("8000");
        byte[] binaryRequest = Header(12, (0, "00"), (10, "03"));
        var binary = new MelsecA1EBinaryFrameRules();
        TestAssert.True(binary.IsHeaderValid(binaryHeader, binaryRequest));
        TestAssert.Equal(2, binary.GetBodyLength(binaryHeader, binaryRequest)); // 홀수 비트 3개를 2바이트에 저장
        binaryRequest[0] = 1; binaryHeader[0] = 0x81;
        TestAssert.Equal(6, binary.GetBodyLength(binaryHeader, binaryRequest));
        binaryHeader[1] = 0x5B;
        TestAssert.Equal(2, binary.GetBodyLength(binaryHeader, binaryRequest));
        binaryHeader[0] = 0x82;
        TestAssert.True(!binary.IsHeaderValid(binaryHeader, binaryRequest));
        byte[] asciiHeader = A("8000");
        byte[] asciiRequest = A("00FF000A44200000006403");
        var ascii = new MelsecA1EAsciiFrameRules();
        TestAssert.True(ascii.IsHeaderValid(asciiHeader, asciiRequest));
        TestAssert.Equal(4, ascii.GetBodyLength(asciiHeader, asciiRequest));
        asciiHeader = A("8100");
        TestAssert.Equal(12, ascii.GetBodyLength(asciiHeader, asciiRequest));
        asciiRequest[20] = (byte)'0'; asciiRequest[21] = (byte)'0';
        TestAssert.Equal(1024, ascii.GetBodyLength(asciiHeader, asciiRequest)); // ASCII 개수 00은 256워드
        asciiHeader = A("815B");
        TestAssert.Equal(4, ascii.GetBodyLength(asciiHeader, asciiRequest));
        return Task.CompletedTask;
    }

    internal static Task FetchWriteLengthAndIdentityAsync()
    {
        byte[] parserHeader = Header(16, (0, "5335"), (3, "07"), (5, "06"));
        byte[] parserRequest = Header(16, (8, "01"), (12, "0003"));
        var parser = new FetchWriteFrameRules();
        TestAssert.True(parser.IsHeaderValid(parserHeader, parserRequest));
        TestAssert.Equal<int?>(7, parser.GetSequenceId(parserHeader));
        TestAssert.Equal(6, parser.GetBodyLength(parserHeader, parserRequest));
        parserRequest[8] = 2;
        TestAssert.Equal(3, parser.GetBodyLength(parserHeader, parserRequest));
        parserHeader[8] = 1;
        TestAssert.Equal(0, parser.GetBodyLength(parserHeader, parserRequest));
        parserHeader[0] = 0;
        TestAssert.True(!parser.IsHeaderValid(parserHeader, parserRequest));
        return Task.CompletedTask;
    }

    internal static Task TransactionIdsAndIntermediateAcknowledgmentsAsync()
    {
        byte[] modbusHeader = H("1234000000060103");
        var modbus = new ModbusTcpFrameRules();
        TestAssert.Equal<int?>(0x1234, modbus.GetSequenceId(modbusHeader));
        TestAssert.Equal(ResponseDisposition.Accept, modbus.ClassifyResponse(modbusHeader, H("1234000000060103")));
        TestAssert.Equal(ResponseDisposition.Ignore, modbus.ClassifyResponse(modbusHeader, H("1235000000060103")));
        modbus = new ModbusTcpFrameRules(validateTransactionId: false);
        TestAssert.Equal(ResponseDisposition.Accept, modbus.ClassifyResponse(modbusHeader, H("1235000000060103")));
        byte[] lsHeader = Header(20, (14, "3412"));
        var ls = new LsisFastEnetFrameRules();
        TestAssert.Equal<int?>(0x1234, ls.GetSequenceId(lsHeader));
        foreach (var item in new (IProtocolFrameRules Parser, int Sid)[] { (new FinsTcpFrameRules(), 25), (new FinsUdpFrameRules(), 9) })
        {
            byte[] request = new byte[item.Sid + 1], response = new byte[item.Sid + 1];
            request[item.Sid] = response[item.Sid] = 0x23;
            TestAssert.Equal(ResponseDisposition.Accept, item.Parser.ClassifyResponse(request, response));
            response[item.Sid]++;
            TestAssert.Equal(ResponseDisposition.Ignore, item.Parser.ClassifyResponse(request, response));
        }
        var turck = new TurckReaderFrameRules();
        // ACK는 07/07 길이·상태 필드와 읽기 명령 68을 함께 갖는다.
        TestAssert.Equal(ResponseDisposition.Ignore, turck.ClassifyResponse(H("AA000868"), H("AA070768000000")));
        TestAssert.Equal(ResponseDisposition.Accept, turck.ClassifyResponse(H("AA000868"), H("AA00080800000000")));
        return Task.CompletedTask;
    }

    internal static Task SerialTerminatorAndTrailerCompletenessAsync()
    {
        CompleteOnlyAtEnd(new FatekProgramFrameRules(), A("\u0002014601234ABCDD1\u0003"));
        CompleteOnlyAtEnd(new FujiSpbFrameRules(), A(":0109000000001234\r\n"));
        CompleteOnlyAtEnd(new KeyenceNanoSerialFrameRules(), A("4660 43981\r\n"));
        CompleteOnlyAtEnd(new KeyenceNanoSerialFrameRules(), A("OK\r\n"));
        CompleteOnlyAtEnd(new MelsecFxLinksFrameRules(1, true), A("\u000200FF1234\u000345"));
        CompleteOnlyAtEnd(new MelsecFxLinksFrameRules(1, false), A("\u000200FF1234\u0003"));
        CompleteOnlyAtEnd(new MelsecFxLinksFrameRules(4, true), A("\u000200FF1234\u000345\r\n"));
        CompleteOnlyAtEnd(new MelsecFxSerialFrameRules(), A("\u00021234\u0003CD"));
        byte[] badChecksum = A("\u00021234\u0003CE");
        TestAssert.True(!new MelsecFxSerialFrameRules().IsComplete([], badChecksum));
        CompleteOnlyAtEnd(new SiemensPpiFrameRules(), H("68040468010203040516"));
        CompleteOnlyAtEnd(new VigorSerialFrameRules(), H("10020106101003100010030000"));
        return Task.CompletedTask;
    }

    internal static Task SpecifiedDelimiterConfigurationAsync()
    {
        var single = new DelimitedFrame([3], trailingByteCount: 2);
        var pair = new DelimitedFrame([13, 10], trailingByteCount: 4);
        TestAssert.Equal(4, single.GetFrameLength(new ArraySegment<byte>(H("0103"))));
        TestAssert.Equal(7, pair.GetFrameLength(new ArraySegment<byte>(H("010D0A"))));
        TestAssert.True(pair.GetFrameLength(new ArraySegment<byte>(H("010D"))) == null);
        return Task.CompletedTask;
    }

    internal static Task EveryFrameParserHasAnExplicitCaseAsync()
    {
        Type[] specialCases = [typeof(MelsecA1EBinaryFrameRules), typeof(MelsecA1EAsciiFrameRules), typeof(FetchWriteFrameRules),
            typeof(FinsUdpFrameRules), typeof(FatekProgramFrameRules), typeof(KeyenceNanoSerialFrameRules), typeof(MelsecFxLinksFrameRules),
            typeof(MelsecFxSerialFrameRules), typeof(SiemensPpiFrameRules), typeof(VigorSerialFrameRules)];
        Type[] covered = LengthCases().Select(item => item.Parser.GetType()).Concat(specialCases).Distinct().ToArray();
        Type[] actual = typeof(IProtocolFrameRules).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(IProtocolFrameRules).IsAssignableFrom(type)).ToArray();
        TestAssert.True(new HashSet<Type>(actual).SetEquals(covered),
            "미등록 프레임 파서: " + string.Join(", ", actual.Except(covered).Select(type => type.Name)));
        return Task.CompletedTask;
    }

    private static void CompleteOnlyAtEnd(IProtocolFrameRules parser, byte[] frame)
    {
        for (int count = 0; count <= frame.Length; count++)
        {
            byte[] received = TestBytes.Slice(frame, 0, count);
            TestAssert.Equal(count == frame.Length, parser.IsComplete([], received));
        }
    }

    private static byte[] Header(int length, params (int Offset, string Hex)[] fields)
    {
        byte[] result = new byte[length];
        foreach (var field in fields) H(field.Hex).CopyTo(result, field.Offset);
        return result;
    }
    private static byte[] H(string hex) => TestBytes.FromHexString(hex);
    private static byte[] A(string text) => Encoding.ASCII.GetBytes(text);
}
