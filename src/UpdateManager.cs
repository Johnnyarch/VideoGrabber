using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace VideoGrabber
{
    /// <summary>yt-dlp / ffmpeg 的检测、下载与更新管理</summary>
    public static class UpdateManager
    {
        // .NET 4.0 默认不走 TLS1.2，GitHub 要求 TLS1.2+，这里强制启用
        static UpdateManager()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)(3072 | 768 | 192 | 12288);
                ServicePointManager.Expect100Continue = false;
            }
            catch { }
        }

        private const string ApiLatest = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";
        // 自动回退镜像前缀（在官方直连失败时依次尝试）
        private static readonly string[] Mirrors = new string[] {
            "https://ghfast.top/",
            "https://gh-proxy.com/",
            "https://ghproxy.net/"
        };

        /// <summary>读取本地 yt-dlp 版本号；不存在返回 null</summary>
        public static string GetLocalYtDlpVersion()
        {
            if (!File.Exists(Util.YtDlpPath)) return null;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Util.YtDlpPath;
                psi.Arguments = "--version";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
                using (Process p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    string err = p.StandardError.ReadToEnd();
                    p.WaitForExit(15000);
                    string v = (outp + err).Trim();
                    Match m = Regex.Match(v, @"\d{4}\.\d{2}\.\d{2}");
                    if (m.Success) return m.Value;
                    return v.Length > 0 && v.Length < 40 ? v : null;
                }
            }
            catch { return null; }
        }

        /// <summary>从 GitHub API 获取最新版本号（含镜像回退）</summary>
        public static string GetLatestYtDlpVersion(string mirror, string proxy)
        {
            string[] seeds = BuildMirror(mirror, ApiLatest, proxy == null || proxy.Length == 0);
            foreach (string url in seeds)
            {
                try
                {
                    string json = HttpGetText(url, proxy, 15000);
                    Match m = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                    if (m.Success && m.Groups[1].Value.Length > 0) return m.Groups[1].Value;
                    Match m2 = Regex.Match(json, "\"name\"\\s*:\\s*\"([^\"]+)\"");
                    if (m2.Success && m2.Groups[1].Value.Length > 0) return m2.Groups[1].Value;
                }
                catch { }
            }
            return null;
        }

        /// <summary>构建镜像 URL 列表。若手动 mirror 非空则优先。</summary>
        private static string[] BuildMirror(string mirror, string url, bool officialFirst)
        {
            List<string> list = new List<string>();
            if (mirror != null && mirror.Length > 0)
            {
                string m = mirror.Trim();
                if (!m.EndsWith("/")) m += "/";
                list.Add(m + url);
                if (officialFirst) list.Add(url);
            }
            else
            {
                if (officialFirst) list.Add(url);
                foreach (string mm in Mirrors) list.Add(mm + url);
            }
            return list.ToArray();
        }

        /// <summary>HTTP GET 返回文本</summary>
        public static string HttpGetText(string url, string proxy, int timeoutMs)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "VideoGrabber/1.0 (yt-dlp updater)";
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.AllowAutoRedirect = true;
            if (proxy != null && proxy.Length > 0) req.Proxy = new WebProxy(proxy);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        /// <summary>HTTP 下载到文件（返回成功与否；progress 回调 已下载/总大小，-1 表示未知）</summary>
        public static bool HttpDownload(string url, string dest, string proxy, Action<long, long> progress)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "VideoGrabber/1.0 (yt-dlp updater)";
            req.Timeout = 30000;
            req.ReadWriteTimeout = 30000;
            req.AllowAutoRedirect = true;
            if (proxy != null && proxy.Length > 0) req.Proxy = new WebProxy(proxy);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (Stream ins = resp.GetResponseStream())
            using (FileStream outs = new FileStream(dest, FileMode.Create, FileAccess.Write))
            {
                long total = resp.ContentLength;
                byte[] buf = new byte[81920];
                long got = 0;
                int n;
                while ((n = ins.Read(buf, 0, buf.Length)) > 0)
                {
                    outs.Write(buf, 0, n);
                    got += n;
                    if (progress != null) progress(got, total);
                }
                outs.Flush();
            }
            return true;
        }

        /// <summary>
        /// 确保 yt-dlp 存在且为最新，且架构与本机匹配：不存在→下载；版本旧→覆盖更新；架构不匹配（如从 x64 电脑拷到 ARM64）→重新下载匹配版本。
        /// report(string msg) 报告过程信息；progressPct(-1~100) 报告进度，完成时 -1。
        /// 返回 true 表示更新流程成功完成（可能有更新，也可能已最新）；false 表示失败。
        /// </summary>
        public static bool EnsureYtDlpLatest(AppConfig cfg, Action<string> report, Action<double> progressPct)
        {
            bool arm = Util.IsArm64();
            report("正在检查 yt-dlp 版本（当前电脑架构：" + (arm ? "ARM64" : "x64") + "）…");

            // 架构自检：从其他架构电脑迁移过来时，本地 exe 可能与本机不匹配
            bool archMismatch = false;
            if (File.Exists(Util.YtDlpPath))
            {
                ushort m = Util.GetPEMachine(Util.YtDlpPath);
                if (m != 0 && m != Util.TargetMachine()) archMismatch = true;
            }
            if (archMismatch) report("检测到 yt-dlp 架构与本机不匹配，将重新下载匹配版本…");

            string local = GetLocalYtDlpVersion();
            string latest = null;
            try { latest = GetLatestYtDlpVersion(cfg); }
            catch { latest = null; }

            if (local == null)
            {
                report("未找到 yt-dlp，开始下载最新版（" + (arm ? "ARM64" : "x64") + " 版）…");
                bool ok = DownloadYtDlp(cfg, latest, report, progressPct);
                if (ok) report("yt-dlp 已就绪（版本 " + (latest ?? "latest") + "）");
                else report("yt-dlp 下载失败，请检查网络或镜像设置");
                return ok;
            }

            if (latest == null)
            {
                if (archMismatch)
                {
                    report("无法联网查询版本，但架构不匹配仍需重新下载…");
                    bool okM = DownloadYtDlp(cfg, null, report, progressPct);
                    if (okM) report("已重新下载与本机匹配的 yt-dlp");
                    else report("重新下载失败（沿用旧版，可能需模拟运行）");
                    return okM;
                }
                report("无法联网查询最新版本，沿用本地 yt-dlp " + local);
                return true;
            }

            int cmp = CompareVersions(local, latest);
            if (cmp >= 0 && !archMismatch)
            {
                report("yt-dlp 已是最新版本：" + local);
                return true;
            }

            if (cmp < 0) report("发现新版本：本地 " + local + " → 最新 " + latest + "，开始更新…");
            bool ok2 = DownloadYtDlp(cfg, latest, report, progressPct);
            if (ok2) report(cmp < 0 ? "yt-dlp 已更新到 " + latest : "已重新下载与本机匹配的 yt-dlp");
            else report("yt-dlp 更新失败（保留旧版，可稍后重试）");
            return ok2;
        }

        private static string GetLatestYtDlpVersion(AppConfig cfg)
        {
            return GetLatestYtDlpVersion(cfg.Mirror, cfg.ProxyUrl);
        }

        private static bool DownloadYtDlp(AppConfig cfg, string latest, Action<string> report, Action<double> progressPct)
        {
            string tmp = Path.Combine(Util.RootDir, "yt-dlp.exe.tmp");
            bool ok = false;
            // 按当前电脑架构选择官方资产（x64 / ARM64）
            string asset = Util.YtDlpAssetName();
            // 优先按 tag 精确下载，失败再回退 latest 链接
            if (latest != null && latest.Length > 0)
            {
                string tagUrl = "https://github.com/yt-dlp/yt-dlp/releases/download/" + latest + "/" + asset;
                ok = TryDownload(tagUrl, tmp, cfg, report, progressPct);
            }
            if (!ok) ok = TryDownload("https://github.com/yt-dlp/yt-dlp/releases/latest/download/" + asset, tmp, cfg, report, progressPct);
            if (!ok)
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                return false;
            }
            // 覆盖旧文件（可能被占用时重试）
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    if (File.Exists(Util.YtDlpPath)) File.Delete(Util.YtDlpPath);
                    File.Move(tmp, Util.YtDlpPath);
                    return true;
                }
                catch
                {
                    Thread.Sleep(1500);
                }
            }
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            return false;
        }

        private static bool TryDownload(string url, string dest, AppConfig cfg, Action<string> report, Action<double> progressPct)
        {
            string[] seeds = BuildMirror(cfg.Mirror, url, cfg.Mirror == null || cfg.Mirror.Length == 0);
            foreach (string seed in seeds)
            {
                try
                {
                    long lastPct = -1;
                    bool ok = HttpDownload(seed, dest, cfg.ProxyUrl, delegate(long got, long total)
                    {
                        double pct = total > 0 ? (double)got * 100.0 / total : -1;
                        if (pct >= 0 && (int)pct != (int)lastPct)
                        {
                            lastPct = (long)pct;
                            if (progressPct != null) progressPct(pct);
                        }
                    });
                    if (ok && new FileInfo(dest).Length > 1000000)
                    {
                        if (progressPct != null) progressPct(100);
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }

        /// <summary>比较两个版本号（yyyy.MM.dd 格式或通用格式）；a&gt;b 返回正数</summary>
        public static int CompareVersions(string a, string b)
        {
            if (a == b) return 0;
            Version va, vb;
            bool pa = Version.TryParse(a, out va);
            bool pb = Version.TryParse(b, out vb);
            if (pa && pb) return va.CompareTo(vb);
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>ffmpeg 是否已就绪（存在且架构匹配当前系统）</summary>
        public static bool HasFfmpeg()
        {
            return Util.FfmpegArchOk();
        }

        /// <summary>ffmpeg 已安装但架构与本机不匹配（迁移场景）</summary>
        public static bool FfmpegArchMismatch()
        {
            if (!File.Exists(Util.FfmpegPath)) return false;
            ushort m = Util.GetPEMachine(Util.FfmpegPath);
            return m != 0 && m != Util.TargetMachine();
        }

        /// <summary>下载并安装 ffmpeg（BtbN 官方构建，按当前架构选 win64 / winarm64，解压后把 ffmpeg.exe 放到根目录）</summary>
        public static bool InstallFfmpeg(AppConfig cfg, Action<string> report, Action<double> progressPct)
        {
            if (HasFfmpeg()) { report("ffmpeg 已存在，无需安装"); return true; }
            string dir = Path.Combine(Util.RootDir, ".ffmpeg_tmp");
            string zip = Path.Combine(dir, "ffmpeg.zip");
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
                string zipUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/latest/download/" + Util.FfmpegZipName();
                report("正在下载 ffmpeg（约 " + (Util.IsArm64() ? "141" : "200") + "MB，仅需一次）…");
                bool ok = false;
                string[] seeds = BuildMirror(cfg.Mirror, zipUrl, true);
                foreach (string s in seeds)
                {
                    try
                    {
                        ok = HttpDownload(s, zip, cfg.ProxyUrl, delegate(long got, long total)
                        {
                            if (progressPct != null) progressPct(50);
                        });
                        if (ok && new FileInfo(zip).Length > 50000000) break;
                        ok = false;
                    }
                    catch { ok = false; }
                }
                if (!ok) { report("ffmpeg 下载失败"); return false; }
                report("正在解压安装…");
                if (progressPct != null) progressPct(80);
                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe");
                psi.Arguments = "-NoProfile -Command \"Expand-Archive -LiteralPath '" + zip + "' -DestinationPath '" + dir + "' -Force\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi))
                {
                    if (!p.WaitForExit(300000)) { p.Kill(); return false; }
                }
                string found = FindFfmpegExe(dir);
                if (found == null) { report("解压后未找到 ffmpeg.exe"); return false; }
                File.Copy(found, Util.FfmpegPath, true);
                if (progressPct != null) progressPct(100);
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
                report("ffmpeg 安装完成：" + Util.FfmpegPath);
                return true;
            }
            catch (Exception ex)
            {
                report("ffmpeg 安装失败：" + ex.Message);
                return false;
            }
        }

        private static string FindFfmpegExe(string root)
        {
            foreach (string f in Directory.GetFiles(root, "ffmpeg.exe", SearchOption.AllDirectories))
                return f;
            return null;
        }
    }
}