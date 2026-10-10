using System;

namespace VideoGrabber
{
    /// <summary>控制台测试台：验证 yt-dlp 自动更新逻辑（与 GUI 共用 UpdateManager）</summary>
    static class TestHarness
    {
        static void Main(string[] args)
        {
            Console.WriteLine("== 测试 yt-dlp 自检/自动更新 ==");
            AppConfig cfg = new AppConfig();
            bool ok = UpdateManager.EnsureYtDlpLatest(cfg,
                delegate(string msg) { Console.WriteLine("[LOG] " + msg); },
                delegate(double pct) { Console.WriteLine("[PCT] " + (int)pct + "%"); });
            Console.WriteLine("== 结果: " + ok + " ==");
            Console.WriteLine("== 本地版本: " + (UpdateManager.GetLocalYtDlpVersion() ?? "无") + " ==");
        }
    }
}