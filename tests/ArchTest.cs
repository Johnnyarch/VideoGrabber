using System;

namespace VideoGrabber
{
    static class ArchTest
    {
        static void Main(string[] args)
        {
            Console.WriteLine("== 本机架构检测 ==");
            Console.WriteLine("IsArm64 = " + Util.IsArm64() + "（本测试机应为 False）");
            Console.WriteLine("TargetMachine = 0x" + Util.TargetMachine().ToString("X4") + "（本测试机应为 0x8664）");
            Console.WriteLine("YtDlpAssetName = " + Util.YtDlpAssetName());
            Console.WriteLine("FfmpegZipName = " + Util.FfmpegZipName());

            Console.WriteLine();
            Console.WriteLine("== PE 头解析单测 ==");
            string yt = Util.YtDlpPath;
            if (System.IO.File.Exists(yt))
            {
                ushort m = Util.GetPEMachine(yt);
                Console.WriteLine("yt-dlp.exe Machine = 0x" + m.ToString("X4") + "（应为 0x8664）");
                Console.WriteLine("  → " + (m == 0x8664 ? "PASS" : "FAIL"));
            }
            string ff = Util.FfmpegPath;
            if (System.IO.File.Exists(ff))
            {
                ushort m2 = Util.GetPEMachine(ff);
                Console.WriteLine("ffmpeg.exe Machine = 0x" + m2.ToString("X4") + "（应为 0x8664）");
                Console.WriteLine("  → " + (m2 == 0x8664 ? "PASS" : "FAIL"));
            }

            Console.WriteLine();
            Console.WriteLine("== 架构匹配判定 ==");
            Console.WriteLine("YtDlpArchOk = " + Util.YtDlpArchOk() + "（应为 True）");
            Console.WriteLine("FfmpegArchOk = " + Util.FfmpegArchOk() + "（应为 True）");
            Console.WriteLine("FfmpegArchMismatch = " + UpdateManager.FfmpegArchMismatch() + "（应为 False）");

            Console.WriteLine();
            Console.WriteLine("== 模拟 ARM64 环境的资产名（逻辑推演）==");
            Console.WriteLine("ARM64 时 yt-dlp 资产: yt-dlp_arm64.exe（代码: IsArm64()?…）");
            Console.WriteLine("ARM64 时 ffmpeg zip: ffmpeg-master-latest-winarm64-gpl.zip");
        }
    }
}