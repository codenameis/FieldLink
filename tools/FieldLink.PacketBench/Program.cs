using System;
using System.Windows.Forms;

namespace FieldLink.PacketBench;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "--selftest", StringComparison.OrdinalIgnoreCase))
        {
            // UI 없이 가상 PLC <-> 클라이언트 왕복을 확인한다. 결과는 실행 폴더의 selftest-result.txt 에 남는다.
            return SelfTest.Run();
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
        return 0;
    }
}
