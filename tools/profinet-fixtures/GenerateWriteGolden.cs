using System;
using System.Linq;
using Newtonsoft.Json.Linq;

partial class Golden
{
    // 유효한 요청은 참고 구현에서도 성공해야 기준 데이터로 채택한다.
    // 제품 빌더의 결과로 기대 프레임을 생성하지 않는다.
    static void Good(string owner, string method, params object[] args)
    {
        Console.WriteLine("Write case: " + owner + "." + method);
        Add(owner, method, args);
        JToken expected = JToken.FromObject(cases.Last())["Expected"];
        if (expected is JObject result && (result["Exception"] != null || (result["IsSuccess"] != null && !result["IsSuccess"].Value<bool>())))
            throw new InvalidOperationException(owner + "." + method + ": 유효한 기준 요청 생성 실패 " + expected);
    }

    static void AddWriteCases()
    {
        byte[] words = { 0x34, 0x12, 0xCD, 0xAB };
        bool[] bits = { true, false, true };
        Good("AllenBradleyHelper", "PackRequestWrite", "Tag[2]", (ushort)0xC3, words, 2, false);
        Good("AllenBradleyHelper", "PackRequestWriteSegment", "Tag[2]", (ushort)0xC3, words, 4, 2, false);
        Good("AllenBradleyHelper", "PackRequestReadModifyWrite", "Flags[1]", (uint)4, (uint)0xFFFFFFFB, false);
        Good("AllenBradleyHelper", "PackExecutePCCCWrite", 7, "N7:100", words);
        Good("AllenBradleySLCNet", "BuildWriteCommand", "N7:100", words);
        Good("AllenBradleySLCNet", "BuildWriteCommand", "B3:0/5", true);
        Good("AdsHelper", "BuildWriteCommand", "M100", words, false);
        Good("AdsHelper", "BuildWriteCommand", "M100", bits, true);
        Good("AdsHelper", "BuildReadWriteCommand", "M100", 4, false, words);
        Good("AdsHelper", "BuildWriteControlCommand", (short)5, (short)0, new byte[0]);
        Good("FatekProgramHelper", "BuildWriteByteCommand", (byte)1, "D100", words);
        Good("FatekProgramHelper", "BuildWriteBoolCommand", (byte)2, "M10", bits);
        Good("FujiCommandSettingType", "BuildWriteCommand", "D100", words);
        Good("FujiSPBHelper", "BuildWriteByteCommand", (byte)1, "D100", words);
        Good("FujiSPBHelper", "BuildWriteBoolCommand", (byte)1, "M10", true);
        Good("FujiSPHNet", "BuildWriteCommand", (byte)0xFE, "M1.100", words);
        Good("GeHelper", "BuildWriteCommand", (long)7, "R1", words);
        Good("GeHelper", "BuildWriteCommand", (long)7, "M1", bits);
        Good("KeyenceNanoHelper", "BuildWriteCommand", "DM100", words);
        Good("KeyenceNanoHelper", "BuildWriteCommand", "MR100", bits);
        Good("KeyenceNanoHelper", "BuildWriteExpansionMemoryCommand", (byte)1, (ushort)100, words);
        Good("LSCnetHelper", "BuildWriteByteCommand", (byte)1, "D100", words);
        Good("LSCpuHelper", "BuildWriteByteCommand", (byte)1, "D100", words);
        Good("MelsecA1ENet", "BuildWriteWordCommand", "D100", words, (byte)0xFF);
        Good("MelsecA1ENet", "BuildWriteBoolCommand", "M100", bits, (byte)0xFF);
        Good("MelsecA1EAsciiNet", "BuildWriteWordCommand", "D100", words, (byte)0xFF);
        Good("MelsecA1EAsciiNet", "BuildWriteBoolCommand", "M100", bits, (byte)0xFF);
        Good("MelsecFxLinksHelper", "BuildWriteByteCommand", (byte)1, "D100", words, (byte)0);
        Good("MelsecFxLinksHelper", "BuildWriteBoolCommand", (byte)1, "M100", bits, (byte)0);
        Good("MelsecFxSerialHelper", "BuildWriteWordCommand", "D100", words, true);
        Good("MelsecFxSerialHelper", "BuildWriteWordCommand", "D100", words, false);
        Good("MelsecFxSerialHelper", "BuildWriteBoolPacket", "M100", false);
        Good("OmronHostLinkCModeHelper", "BuildWriteWordCommand", "D100", words);
        Good("PanasonicHelper", "BuildWriteCommand", (byte)1, "D100", words);
        Good("PanasonicHelper", "BuildWriteOneCoil", (byte)1, "R100", true);
        Good("PanasonicHelper", "BuildWriteCoils", (byte)1, new[] { "R100", "R101", "R102" }, bits);
        Good("SiemensFetchWriteNet", "BuildWriteCommand", "DB1.0", words);
        Good("SiemensPPIHelper", "BuildWriteCommand", (byte)2, "DB1.0", words);
        Good("SiemensPPIHelper", "BuildWriteCommand", (byte)2, "M100.0", bits);
        Good("SiemensMPI", "BuildWriteCommand", (byte)2, "M100", words);
        Good("ToyoPuc", "BuildWriteWordCommand", "D100", words);
        Good("ToyoPuc", "BuildWriteBoolCommand", "M101", true);
        Good("ReaderNet", "BuildWriteCommand", (byte)1, (byte)1, (byte)4, words);
        Good("VigorVsHelper", "BuildWriteWordCommand", (byte)1, "D100", words);
        Good("VigorVsHelper", "BuildWriteBoolCommand", (byte)1, "M100", bits);
        Good("XinJEHelper", "BuildWriteWordCommand", (byte)1, "D100", words);
        Good("XinJEHelper", "BuildWriteBoolCommand", (byte)1, "M100", bits);
        Good("DigitronCPLHelper", "BuildWriteCommand", (byte)1, "100", words);
        Good("MemobusHelper", "BuildWriteCommand", (byte)0x20, (byte)0x10, (byte)1, (byte)0, (ushort)100, words);
        Good("MemobusHelper", "BuildWriteCommand", (byte)0x20, (byte)0x0F, (byte)1, (byte)0, (ushort)100, bits);
        Good("YokogawaLinkTcp", "BuildWriteWordCommand", (byte)1, "D100", words);
        Good("YokogawaLinkTcp", "BuildWriteBoolCommand", (byte)1, "M100", bits);
        Good("YokogawaLinkTcp", "BuildWriteRandomWordCommand", (byte)1, new[] { "D100", "D200" }, words);
        Good("YokogawaLinkTcp", "BuildWriteSpecialModule", (byte)1, (byte)0, (byte)1, (ushort)100, words);
    }
}
