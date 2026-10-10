using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace VideoGrabber
{
    /// <summary>应用配置：以 INI 形式保存在 exe 同目录 config.ini</summary>
    public class AppConfig
    {
        public string OutputDir = "";
        public int FormatIndex = 0;              // 0最佳 1:1080p 2:720p 3:仅音频 4:自定义
        public string CustomFormat = "bestvideo*+bestaudio/best";
        public int MaxParallel = 2;
        public int CookieSource = 0;             // 0无 1chrome 2edge 3firefox 4浏览器自动 5cookies.txt文件
        public string CookieFile = "";
        public string ProxyUrl = "";
        public bool PlaylistMode = true;         // 下载整个合集/播放列表
        public string SingleTemplate = "%(title)s [%(id)s].%(ext)s";
        public string PlaylistTemplate = "%(playlist_title)s/%(playlist_index)s - %(title)s [%(id)s].%(ext)s";
        public bool UsePlaylistTemplate = true;
        public string Mirror = "";               // 手动镜像前缀，空则自动尝试官方+内置镜像

        public static string ConfigFile { get { return Path.Combine(Util.RootDir, "config.ini"); } }

        public static AppConfig Load()
        {
            AppConfig cfg = new AppConfig();
            try
            {
                if (!File.Exists(ConfigFile)) return cfg;
                foreach (string raw in File.ReadAllLines(ConfigFile, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#") || line.IndexOf('=') <= 0) continue;
                    string k = line.Substring(0, line.IndexOf('=')).Trim();
                    string v = line.Substring(line.IndexOf('=') + 1).Trim();
                    switch (k)
                    {
                        case "OutputDir": cfg.OutputDir = v; break;
                        case "FormatIndex": { int t; if (int.TryParse(v, out t)) cfg.FormatIndex = t; break; }
                        case "CustomFormat": cfg.CustomFormat = v; break;
                        case "MaxParallel": { int t; if (int.TryParse(v, out t) && t >= 1 && t <= 8) cfg.MaxParallel = t; break; }
                        case "CookieSource": { int t; if (int.TryParse(v, out t)) cfg.CookieSource = t; break; }
                        case "CookieFile": cfg.CookieFile = v; break;
                        case "ProxyUrl": cfg.ProxyUrl = v; break;
                        case "PlaylistMode": cfg.PlaylistMode = (v == "1"); break;
                        case "SingleTemplate": if (v.Length > 0) cfg.SingleTemplate = v; break;
                        case "PlaylistTemplate": if (v.Length > 0) cfg.PlaylistTemplate = v; break;
                        case "UsePlaylistTemplate": cfg.UsePlaylistTemplate = (v == "1"); break;
                        case "Mirror": cfg.Mirror = v; break;
                    }
                }
            }
            catch { }
            // 模板自愈：修复历史版本写入的非法模板（如合集模板丢失 playlist_title 目录导致合集不建文件夹）
            if (cfg.PlaylistTemplate.IndexOf("playlist_title") < 0 || cfg.PlaylistTemplate.IndexOf("/") < 0)
                cfg.PlaylistTemplate = "%(playlist_title)s/%(playlist_index)s - %(title)s [%(id)s].%(ext)s";
            if (cfg.SingleTemplate.IndexOf("/") >= 0)
                cfg.SingleTemplate = "%(title)s [%(id)s].%(ext)s";
            return cfg;
        }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("; 视频下载器配置文件（UTF-8）");
                sb.AppendLine("OutputDir=" + OutputDir);
                sb.AppendLine("FormatIndex=" + FormatIndex);
                sb.AppendLine("CustomFormat=" + CustomFormat);
                sb.AppendLine("MaxParallel=" + MaxParallel);
                sb.AppendLine("CookieSource=" + CookieSource);
                sb.AppendLine("CookieFile=" + CookieFile);
                sb.AppendLine("ProxyUrl=" + ProxyUrl);
                sb.AppendLine("PlaylistMode=" + (PlaylistMode ? "1" : "0"));
                sb.AppendLine("SingleTemplate=" + SingleTemplate);
                sb.AppendLine("PlaylistTemplate=" + PlaylistTemplate);
                sb.AppendLine("UsePlaylistTemplate=" + (UsePlaylistTemplate ? "1" : "0"));
                sb.AppendLine("Mirror=" + Mirror);
                File.WriteAllText(ConfigFile, sb.ToString(), new UTF8Encoding(true));
            }
            catch { }
        }
    }

    public static class Util
    {
        /// <summary>exe 所在目录（软件根目录，yt-dlp/ffmpeg/config 都在这里）</summary>
        public static string RootDir
        {
            get
            {
                string p = AppDomain.CurrentDomain.BaseDirectory;
                return p;
            }
        }

        public static string YtDlpPath { get { return Path.Combine(RootDir, "yt-dlp.exe"); } }
        public static string FfmpegPath { get { return Path.Combine(RootDir, "ffmpeg.exe"); } }

        public static string DefaultDownloadDir()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "视频下载");
            try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch { }
            return dir;
        }

        // ============ CPU 架构检测（支持 x64 / ARM64 迁移）============

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_INFO
        {
            public ushort wProcessorArchitecture;
            public ushort wReserved;
            public uint dwPageSize;
            public IntPtr lpMinimumApplicationAddress;
            public IntPtr lpMaximumApplicationAddress;
            public IntPtr dwActiveProcessorMask;
            public uint dwNumberOfProcessors;
            public uint dwProcessorType;
            public uint dwAllocationGranularity;
            public ushort wProcessorLevel;
            public ushort wProcessorRevision;
        }

        [DllImport("kernel32.dll")]
        private static extern void GetNativeSystemInfo(ref SYSTEM_INFO lpSystemInfo);

        private const ushort ARCH_AMD64 = 9;   // PROCESSOR_ARCHITECTURE_AMD64
        private const ushort ARCH_ARM64 = 12;  // PROCESSOR_ARCHITECTURE_ARM64

        /// <summary>当前系统是否为 ARM64（即使本进程运行在模拟层下也返回真实硬件架构）</summary>
        public static bool IsArm64()
        {
            try
            {
                SYSTEM_INFO si = new SYSTEM_INFO();
                GetNativeSystemInfo(ref si);
                return si.wProcessorArchitecture == ARCH_ARM64;
            }
            catch { return false; }
        }

        /// <summary>当前系统对应的目标 PE 架构（0x8664=x64 / 0xAA64=ARM64）</summary>
        public static ushort TargetMachine()
        {
            return IsArm64() ? (ushort)0xAA64 : (ushort)0x8664;
        }

        /// <summary>读取 exe 的 PE 头 Machine 字段（0x8664=x64 / 0xAA64=ARM64 / 0x14C=x86 / 0=未知）</summary>
        public static ushort GetPEMachine(string exePath)
        {
            try
            {
                using (FileStream fs = File.OpenRead(exePath))
                {
                    byte[] buf = new byte[4096];
                    int n = fs.Read(buf, 0, buf.Length);
                    if (n < 0x40) return 0;
                    int peOff = buf[0x3C] | (buf[0x3D] << 8) | (buf[0x3E] << 16) | (buf[0x3F] << 24);
                    if (peOff < 0 || peOff + 6 > n) return 0;
                    if (buf[peOff] != 'P' || buf[peOff + 1] != 'E') return 0;
                    return (ushort)(buf[peOff + 4] | (buf[peOff + 5] << 8));
                }
            }
            catch { return 0; }
        }

        /// <summary>按当前架构返回 yt-dlp 官方资产文件名</summary>
        public static string YtDlpAssetName()
        {
            return IsArm64() ? "yt-dlp_arm64.exe" : "yt-dlp.exe";
        }

        /// <summary>按当前架构返回 BtbN ffmpeg 构建 zip 名</summary>
        public static string FfmpegZipName()
        {
            return IsArm64() ? "ffmpeg-master-latest-winarm64-gpl.zip" : "ffmpeg-master-latest-win64-gpl.zip";
        }

        /// <summary>yt-dlp.exe 是否存在且架构匹配当前系统</summary>
        public static bool YtDlpArchOk()
        {
            if (!File.Exists(YtDlpPath)) return false;
            ushort m = GetPEMachine(YtDlpPath);
            return m == 0 || m == TargetMachine(); // 0=无法解析，宽容处理
        }

        /// <summary>ffmpeg.exe 是否存在且架构匹配当前系统</summary>
        public static bool FfmpegArchOk()
        {
            if (!File.Exists(FfmpegPath)) return false;
            ushort m = GetPEMachine(FfmpegPath);
            return m == 0 || m == TargetMachine();
        }
    }
}