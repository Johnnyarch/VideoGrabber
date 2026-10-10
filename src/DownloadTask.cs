using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace VideoGrabber
{
    public enum TaskState { Idle, Queued, Running, Completed, Failed }

    /// <summary>单个下载任务（每个任务独立进程运行 yt-dlp）</summary>
    public class DownloadTask
    {
        public int Id;
        public string Url;
        public string Title = "";
        public TaskState State = TaskState.Idle;

        // 进程实时字段（worker 线程写，UI 定时读）
        public string ProgressText = "";
        public double ProgressPct = 0;
        public string SpeedText = "";
        public string OutFile = "";
        public string ErrorText = "";
        public DateTime DoneTime;

        // 构建参数
        public string FormatArgs = "";
        public string ExtraArgs = "";
        public bool PlaylistMode = false;
        public int CookieSource = 0;
        public string CookieFile = "";
        public string ProxyUrl = "";
public string Template = "";
        public string OutputDir = "";
        public bool UseFfmpegLoc = false;
        /// <summary>合集模板下探测为单视频时回退使用的单文件模板</summary>
        public string FallbackTemplate = "";
        /// <summary>是否需要探测链接是否为合集（勾选合集模式时为 true）</summary>
        public bool NeedDetect = false;
        /// <summary>探测/回退说明（UI 日志显示）</summary>
        public string DetectNote = "";

        private Process _proc;
        private readonly object _lock = new object();
        public List<string> LogLines = new List<string>();
        public Action<DownloadTask> OnFinished;

        public void Kill()
        {
            lock (_lock)
            {
                if (_proc != null)
                {
                    try { if (!_proc.HasExited) _proc.Kill(); } catch { }
                }
            }
        }

        /// <summary>在后台线程执行：启动 yt-dlp 并解析输出</summary>
        public void Run()
        {
            try
            {
                State = TaskState.Running;
                ProgressText = "准备中…";
                // 合集模式下先探测链接是否为真正的合集/播放列表：
                // 若是单视频 → 自动回退到单文件模板（不建合集文件夹）
if (NeedDetect)
                {
                    ProgressText = "检测链接类型…";
                    bool isPlaylist = ProbePlaylist(Url, ProxyUrl);
                    if (!isPlaylist)
                    {
                        if (FallbackTemplate.Length > 0) Template = FallbackTemplate;
                        DetectNote = "该链接为单视频（非合集），已自动改用单文件模板：文件直接保存到下载目录";
                        // B 站视频页链接可能带 spm_id_from=...videopod.sections（从合集页点入的追踪参数），
                        // 这类链接只代表单个视频；给用户提示获取合集页链接的方法
                        string u = Url.ToLowerInvariant();
                        if (u.IndexOf("bilibili") >= 0 && (u.IndexOf("videopod") >= 0 || u.IndexOf("sections") >= 0 || u.IndexOf("spm_id_from") >= 0))
                        {
                            DetectNote += "；若想下载整个合集：在视频页点击视频下方合集名称进入合集页，复制合集页链接（space.bilibili.com/…/channel/collectiondetail?sid=…）再添加";
                        }
                    }
                    else
                    {
                        DetectNote = "检测到合集/播放列表，按合集保存（下载目录\\\\合集名\\\\）";
                    }
                }
                string exe = "";
                string args = "";
                BuildArgs(out exe, out args);
                AddLog("命令: " + exe + " " + args);

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exe;
                psi.Arguments = args;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                using (Process p = Process.Start(psi))
                {
                    lock (_lock) _proc = p;
                    p.OutputDataReceived += new DataReceivedEventHandler(OnOutputLine);
                    p.ErrorDataReceived += new DataReceivedEventHandler(OnOutputLine);
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    p.WaitForExit();
                    lock (_lock) _proc = null;

                    if (State == TaskState.Running)
                    {
                        if (p.ExitCode == 0) { State = TaskState.Completed; ProgressText = "完成"; ProgressPct = 100; }
                        else if (p.ExitCode == 100)
                        {
                            // yt-dlp 退出码 100：播放列表/合集中部分视频失败，其余已下载成功
                            State = TaskState.Completed;
                            ProgressText = "完成（部分失败）";
                        }
                        else if (p.ExitCode == 101)
                        {
                            State = TaskState.Failed;
                            ProgressText = "已中止";
                        }
                        else if (p.ExitCode == 2)
                        {
                            State = TaskState.Failed;
                            ProgressText = "参数错误";
                            if (ErrorText.Length == 0) ErrorText = "yt-dlp 参数无效（请检查文件名模板 / 格式代码等设置）";
                        }
                        else
                        {
                            State = TaskState.Failed;
                            ProgressText = "失败（退出码 " + p.ExitCode + "）";
                            if (ErrorText.Length == 0) ErrorText = "yt-dlp 未返回具体错误信息（可尝试命令行运行排查）";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                State = TaskState.Failed;
                ProgressText = "启动失败";
                ErrorText = ex.Message;
            }
            DoneTime = DateTime.Now;
            if (OnFinished != null) OnFinished(this);
        }

        private void OnOutputLine(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data)) return;
            string line = e.Data;
            AddLog(line);
            ParseLine(line);
        }

        private void AddLog(string line)
        {
            LogLines.Add(line);
            if (LogLines.Count > 800) LogLines.RemoveAt(0);
        }

        private void ParseLine(string line)
        {
            string l = line.Trim();
            if (l.Length == 0) return;
            // 标题识别：[Bilibili] 合集名: Downloading ...
            Match tm = Regex.Match(l, @"^\[[^\]]+\]\s+(.+?):\s+Downloading\s");
            if (tm.Success)
            {
                string t = tm.Groups[1].Value.Trim();
                if (t.Length > 0 && t.Length < 300) Title = t;
                return;
            }
            // 下载目标文件
            Match dm = Regex.Match(l, @"^\[download\]\s+Destination:\s*(.+)$");
            if (dm.Success)
            {
                OutFile = dm.Groups[1].Value.Trim().Trim('"', '\u201c', '\u201d');
                ProgressText = "下载中";
                return;
            }
            // 进度
            Match pm = Regex.Match(l, @"^\[download\]\s+(\d+(?:\.\d+)?)%\s+of\s+([^\s]+?)\s+at\s+([^\s]+)");
            if (pm.Success)
            {
                double pct;
                if (double.TryParse(pm.Groups[1].Value, out pct))
                {
                    ProgressPct = pct;
                    ProgressText = pm.Groups[1].Value + "% of " + pm.Groups[2].Value;
                    SpeedText = pm.Groups[3].Value;
                }
                return;
            }
            Match pm2 = Regex.Match(l, @"^\[download\]\s+100%\s+of\s+([^\s]+?)\s+in\s+([^\s]+)");
            if (pm2.Success)
            {
                ProgressPct = 100;
                ProgressText = "100% of " + pm2.Groups[1].Value;
                SpeedText = "";
                return;
            }
            Match im = Regex.Match(l, @"^\[download\]\s+Downloading\s+item\s+(\d+)\s+of\s+(\d+)");
            if (im.Success)
            {
                ProgressText = "项目 " + im.Groups[1].Value + "/" + im.Groups[2].Value;
                return;
            }
            // 合并 / 音频提取
            Match mm = Regex.Match(l, @"^\[Merger\]\s+Merging\s+formats\s+into\s+\""?(.+?)\""?\s*$");
            if (mm.Success) { OutFile = mm.Groups[1].Value; ProgressText = "正在合并…"; return; }
            Match am = Regex.Match(l, @"^\[ExtractAudio\]\s+Destination:\s*(.+)$");
            if (am.Success) { OutFile = am.Groups[1].Value; ProgressText = "转换音频…"; return; }
            // 错误 / 警告
            if (l.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
            {
                ErrorText = l.Length > 400 ? l.Substring(0, 400) : l;
            }
        }

        private void BuildArgs(out string exe, out string args)
        {
            exe = Util.YtDlpPath;
            List<string> a = new List<string>();
            a.Add("--no-warnings");
            a.Add("--newline");
            a.Add("--encoding utf-8");   // 强制 yt-dlp 以 UTF-8 输出，避免 Windows 下 GBK 中文乱码
            if (PlaylistMode) a.Add("--yes-playlist");
            else a.Add("--no-playlist");
            if (Template.Length > 0)
            {
                // 模板含空格（如 "%(title)s [%(id)s].%(ext)s"），必须加引号，否则会被拆成多个参数
                a.Add("-o \"" + Template + "\"");
            }
            if (FormatArgs.Length > 0) a.Add("-f " + FormatArgs);
            if (ExtraArgs.Length > 0) a.Add(ExtraArgs);
            if (OutputDir.Length > 0) a.Add("--paths \"" + OutputDir + "\"");
            if (ProxyUrl.Length > 0) a.Add("--proxy \"" + ProxyUrl + "\"");
            switch (CookieSource)
            {
                case 1: a.Add("--cookies-from-browser chrome"); break;
                case 2: a.Add("--cookies-from-browser edge"); break;
                case 3: a.Add("--cookies-from-browser firefox"); break;
                case 4: a.Add("--cookies-from-browser edge:Default"); break;
                case 5:
                    if (CookieFile.Length > 0) a.Add("--cookies \"" + CookieFile + "\"");
                    break;
            }
            if (UseFfmpegLoc && File.Exists(Util.FfmpegPath)) a.Add("--ffmpeg-location \"" + Util.FfmpegPath + "\"");
            a.Add("--retries 5");
            a.Add("--continue");
            if (Url.Length > 0) a.Add("\"" + Url + "\"");
            args = string.Join(" ", a.ToArray());
        }

/// <summary>
        /// 探测 URL 是否为合集/播放列表：flat 模式列出条目，条目数 ≥ 2 即判定为合集。
        /// flat 模式不解析视频格式，不受"需登录才能取流"影响；0/1 条 → 单视频。
        /// </summary>
        private bool ProbePlaylist(string url, string proxy)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Util.YtDlpPath;
                psi.Arguments = "--no-warnings --flat-playlist --encoding=utf-8 --print \"%(id)s\" \"" + url + "\"";
                if (proxy.Length > 0) psi.Arguments = "--proxy \"" + proxy + "\" " + psi.Arguments;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
                using (Process p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(25000);
                    if (!p.HasExited) { try { p.Kill(); } catch { } }
                    int count = 0;
                    foreach (string ln in outp.Split('\n'))
                    {
                        if (ln.Trim().Length > 0) count++;
                    }
                    return count >= 2;
                }
            }
            catch { return true; } // 探测异常时保守按合集处理
        }
    }
}