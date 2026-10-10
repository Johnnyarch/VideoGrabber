using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace VideoGrabber
{
    public class MainForm : Form
    {
        private AppConfig _cfg;
        private TaskManager _tm = new TaskManager();
        private System.Windows.Forms.Timer _timer;

        private TextBox txtUrl;
        private TextBox txtDir;
        private Button btnBrowse;
        private ComboBox cmbFormat;
        private ComboBox cmbCookie;
        private Button btnCookieFile;
        private NumericUpDown nupParallel;
        private CheckBox chkPlaylist;
        private TextBox txtProxy;
        private TextBox txtTemplate;
        private ComboBox cmbMirror;
        private Button btnAdd;
        private DataGridView grid;
        private RichTextBox LogBox;
        private Button btnStartAll, btnStopSel, btnRemoveSel, btnClearLog, btnUpdateYt, btnInstallFfmpeg, btnOpenDir, btnClearAll;
        private ToolStripStatusLabel stStatus;
        private ToolStripProgressBar stProgress;
        private Label lblFfmpeg;

        public MainForm()
        {
            _cfg = AppConfig.Load();
            // 窗体/任务栏图标使用 exe 内嵌图标（灰色底白色闪电）
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            InitUI();
            _tm.MaxParallel = _cfg.MaxParallel;
            _tm.OnTaskFinished = new Action<DownloadTask>(OnTaskFinished);
            this.FormClosing += new FormClosingEventHandler(MainForm_FormClosing);
            this.Shown += new EventHandler(MainForm_Shown);
        }

        // ================= 界面构建 =================
        private void InitUI()
        {
            this.Text = "视频下载器（yt-dlp）";
            this.ClientSize = new Size(1180, 708);
            this.MinimumSize = new Size(980, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Microsoft YaHei UI", 9f);

            // --- 顶部：新建任务 ---
            GroupBox gb1 = new GroupBox();
            gb1.Text = "1. 新建下载任务（支持 B 站合集 / 剧集 / 播放列表 / 单个视频）";
            gb1.SetBounds(12, 8, 1156, 56);
            gb1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtUrl = new TextBox(); txtUrl.SetBounds(14, 22, 996, 24);
            txtUrl.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnAdd = new Button(); btnAdd.Text = "添加任务"; btnAdd.SetBounds(1016, 21, 112, 28);
            btnAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAdd.Click += new EventHandler(btnAdd_Click);
            gb1.Controls.Add(txtUrl); gb1.Controls.Add(btnAdd);

            // 设置区
            GroupBox gb2 = new GroupBox();
            gb2.Text = "2. 下载设置";
            gb2.SetBounds(12, 68, 1156, 140);
            gb2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            // 行1：下载目录
            Label l1 = new Label(); l1.Text = "下载目录"; l1.SetBounds(16, 24, 64, 22);
            txtDir = new TextBox(); txtDir.SetBounds(84, 22, 858, 24);
            txtDir.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtDir.Text = _cfg.OutputDir.Length > 0 ? _cfg.OutputDir : Util.DefaultDownloadDir();
            btnBrowse = new Button(); btnBrowse.Text = "浏览…"; btnBrowse.SetBounds(948, 22, 84, 26);
            btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnBrowse.Click += new EventHandler(btnBrowse_Click);
            btnOpenDir = new Button(); btnOpenDir.Text = "打开目录"; btnOpenDir.SetBounds(1036, 22, 100, 26);
            btnOpenDir.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnOpenDir.Click += new EventHandler(btnOpenDir_Click);
            gb2.Controls.Add(l1); gb2.Controls.Add(txtDir); gb2.Controls.Add(btnBrowse); gb2.Controls.Add(btnOpenDir);

            // 行2：画质 / Cookie / 并行 / 代理
            Label l2 = new Label(); l2.Text = "画质"; l2.SetBounds(16, 60, 44, 22);
            cmbFormat = new ComboBox(); cmbFormat.DropDownStyle = ComboBoxStyle.DropDownList; cmbFormat.SetBounds(62, 58, 224, 24);
            cmbFormat.Items.Add("最佳画质（自动合并音视频）");
            cmbFormat.Items.Add("1080P 及以下");
            cmbFormat.Items.Add("720P 及以下");
            cmbFormat.Items.Add("仅音频（MP3）");
            cmbFormat.Items.Add("自定义格式代码…");
            if (_cfg.FormatIndex >= 0 && _cfg.FormatIndex < cmbFormat.Items.Count) cmbFormat.SelectedIndex = _cfg.FormatIndex; else cmbFormat.SelectedIndex = 0;

            Label l3 = new Label(); l3.Text = "登录 Cookie"; l3.SetBounds(320, 60, 76, 22);
            cmbCookie = new ComboBox(); cmbCookie.DropDownStyle = ComboBoxStyle.DropDownList; cmbCookie.SetBounds(398, 58, 170, 24);
            cmbCookie.Items.Add("无（匿名下载）");
            cmbCookie.Items.Add("Chrome");
            cmbCookie.Items.Add("Edge");
            cmbCookie.Items.Add("Firefox");
            cmbCookie.Items.Add("Edge (默认配置)");
            cmbCookie.Items.Add("cookies.txt 文件…");
            if (_cfg.CookieSource >= 0 && _cfg.CookieSource < cmbCookie.Items.Count) cmbCookie.SelectedIndex = _cfg.CookieSource; else cmbCookie.SelectedIndex = 0;
            cmbCookie.SelectedIndexChanged += new EventHandler(cmbCookie_SelectedIndexChanged);
            btnCookieFile = new Button(); btnCookieFile.Text = "选文件"; btnCookieFile.SetBounds(572, 58, 70, 26);
            btnCookieFile.Visible = (_cfg.CookieSource == 5);
            btnCookieFile.Click += new EventHandler(btnCookieFile_Click);

            Label l4 = new Label(); l4.Text = "并行"; l4.SetBounds(654, 60, 40, 22);
            nupParallel = new NumericUpDown(); nupParallel.Minimum = 1; nupParallel.Maximum = 8; nupParallel.Value = _cfg.MaxParallel; nupParallel.SetBounds(696, 58, 52, 24);
            nupParallel.ValueChanged += new EventHandler(nupParallel_ValueChanged);
            Label l5 = new Label(); l5.Text = "代理(可选)"; l5.SetBounds(762, 60, 70, 22);
            txtProxy = new TextBox(); txtProxy.Text = _cfg.ProxyUrl; txtProxy.SetBounds(836, 58, 300, 24);
            txtProxy.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            gb2.Controls.AddRange(new Control[] { l2, cmbFormat, l3, cmbCookie, btnCookieFile, l4, nupParallel, l5, txtProxy });

            // 行3：合集模式 + 模板 + 更新源
            chkPlaylist = new CheckBox(); chkPlaylist.Text = "下载整个合集 / 剧集 / 播放列表"; chkPlaylist.SetBounds(16, 92, 280, 22);
            chkPlaylist.Checked = _cfg.PlaylistMode;
            chkPlaylist.CheckedChanged += new EventHandler(chkPlaylist_CheckedChanged);
            Label l6 = new Label(); l6.Text = "文件名模板"; l6.SetBounds(304, 94, 80, 22);
            txtTemplate = new TextBox(); txtTemplate.SetBounds(388, 92, 538, 24);
            txtTemplate.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtTemplate.Text = _cfg.PlaylistMode ? _cfg.PlaylistTemplate : _cfg.SingleTemplate;
            Label l7 = new Label(); l7.Text = "更新源"; l7.SetBounds(946, 94, 56, 22);
            l7.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cmbMirror = new ComboBox(); cmbMirror.DropDownStyle = ComboBoxStyle.DropDownList; cmbMirror.SetBounds(1006, 92, 130, 24);
            cmbMirror.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cmbMirror.Items.Add("官方(GitHub)");
            cmbMirror.Items.Add("ghfast.top");
            cmbMirror.Items.Add("gh-proxy.com");
            cmbMirror.Items.Add("ghproxy.net");
            if (_cfg.Mirror == "") cmbMirror.SelectedIndex = 0;
            else if (_cfg.Mirror.IndexOf("ghfast") >= 0) cmbMirror.SelectedIndex = 1;
            else if (_cfg.Mirror.IndexOf("gh-proxy") >= 0) cmbMirror.SelectedIndex = 2;
            else if (_cfg.Mirror.IndexOf("ghproxy") >= 0) cmbMirror.SelectedIndex = 3;
            else cmbMirror.SelectedIndex = 0;
            cmbMirror.SelectedIndexChanged += new EventHandler(cmbMirror_SelectedIndexChanged);
            Label l8 = new Label(); l8.Text = "yt-dlp 启动时自动更新"; l8.ForeColor = Color.Gray; l8.SetBounds(1006, 116, 150, 18);
            l8.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            gb2.Controls.AddRange(new Control[] { chkPlaylist, l6, txtTemplate, l7, cmbMirror, l8 });

            // 任务列表
            GroupBox gb3 = new GroupBox();
            gb3.Text = "3. 任务列表";
            gb3.SetBounds(12, 212, 1156, 340);
            gb3.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.ReadOnly = true;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.BackgroundColor = Color.White;
            grid.ColumnHeadersHeight = 26;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            DataGridViewTextBoxColumn c1 = new DataGridViewTextBoxColumn(); c1.HeaderText = "任务"; c1.Width = 380;
            DataGridViewTextBoxColumn c2 = new DataGridViewTextBoxColumn(); c2.HeaderText = "状态"; c2.Width = 90;
            DataGridViewTextBoxColumn c3 = new DataGridViewTextBoxColumn(); c3.HeaderText = "进度"; c3.Width = 200;
            DataGridViewTextBoxColumn c4 = new DataGridViewTextBoxColumn(); c4.HeaderText = "速度"; c4.Width = 100;
            DataGridViewTextBoxColumn c5 = new DataGridViewTextBoxColumn(); c5.HeaderText = "输出文件"; c5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; c5.MinimumWidth = 200;
            grid.Columns.AddRange(new DataGridViewColumn[] { c1, c2, c3, c4, c5 });
            gb3.Controls.Add(grid);

            // 日志区
            GroupBox gb4 = new GroupBox();
            gb4.Text = "4. 运行日志";
            gb4.SetBounds(12, 590, 1156, 90);
            gb4.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            LogBox = new RichTextBox();
            LogBox.Dock = DockStyle.Fill;
            LogBox.ReadOnly = true;
            LogBox.BackColor = Color.FromArgb(30, 30, 30);
            LogBox.ForeColor = Color.FromArgb(230, 230, 230);
            LogBox.BorderStyle = BorderStyle.None;
            LogBox.Font = new Font("Consolas", 9f);
            gb4.Controls.Add(LogBox);

            // 底部按钮条
            AnchorStyles botLeft = AnchorStyles.Bottom | AnchorStyles.Left;
            btnStartAll = new Button(); btnStartAll.Text = "开始全部"; btnStartAll.SetBounds(12, 556, 96, 30); btnStartAll.Anchor = botLeft; btnStartAll.Click += new EventHandler(btnStartAll_Click);
            btnStopSel = new Button(); btnStopSel.Text = "停止选中"; btnStopSel.SetBounds(112, 556, 96, 30); btnStopSel.Anchor = botLeft; btnStopSel.Click += new EventHandler(btnStopSel_Click);
            btnRemoveSel = new Button(); btnRemoveSel.Text = "移除选中"; btnRemoveSel.SetBounds(212, 556, 96, 30); btnRemoveSel.Anchor = botLeft; btnRemoveSel.Click += new EventHandler(btnRemoveSel_Click);
            btnClearAll = new Button(); btnClearAll.Text = "清空列表"; btnClearAll.SetBounds(312, 556, 96, 30); btnClearAll.Anchor = botLeft; btnClearAll.Click += new EventHandler(btnClearAll_Click);
            btnUpdateYt = new Button(); btnUpdateYt.Text = "更新 yt-dlp"; btnUpdateYt.SetBounds(412, 556, 106, 30); btnUpdateYt.Anchor = botLeft; btnUpdateYt.Click += new EventHandler(btnUpdateYtd_Click);
            btnInstallFfmpeg = new Button(); btnInstallFfmpeg.Text = "安装 ffmpeg"; btnInstallFfmpeg.SetBounds(522, 556, 106, 30); btnInstallFfmpeg.Anchor = botLeft; btnInstallFfmpeg.Click += new EventHandler(btnInstallFfmpeg_Click);
            btnClearLog = new Button(); btnClearLog.Text = "清空日志"; btnClearLog.SetBounds(632, 556, 96, 30); btnClearLog.Anchor = botLeft; btnClearLog.Click += new EventHandler(btnClearLog_Click);
            lblFfmpeg = new Label(); lblFfmpeg.AutoSize = true; lblFfmpeg.SetBounds(742, 561, 430, 20);
            lblFfmpeg.Anchor = botLeft;
            lblFfmpeg.Text = "";
            this.Controls.AddRange(new Control[] { gb1, gb2, gb3, gb4, btnStartAll, btnStopSel, btnRemoveSel, btnClearAll, btnUpdateYt, btnInstallFfmpeg, btnClearLog, lblFfmpeg });

            // 状态栏
            StatusStrip ss = new StatusStrip();
            ss.SizingGrip = false;
            stStatus = new ToolStripStatusLabel(); stStatus.Text = "就绪"; stStatus.Spring = true;
            stProgress = new ToolStripProgressBar(); stProgress.Width = 140; stProgress.Visible = false;
            ss.Items.Add(stStatus); ss.Items.Add(stProgress);
            this.Controls.Add(ss);

            _timer = new System.Windows.Forms.Timer(); _timer.Interval = 100; _timer.Tick += new EventHandler(Timer_Tick); _timer.Start();
            FfmpegLabel();
        }

        // ================= 事件 =================
        private void btnAdd_Click(object sender, EventArgs e)
        {
            string url = txtUrl.Text.Trim();
            if (url.Length == 0) { MessageBox.Show("请输入视频 / 合集 / 播放列表链接"); return; }
            if (!url.StartsWith("http://") && !url.StartsWith("https://")) url = "https://" + url;
            string fmt = BuildFormatFor(cmbFormat.SelectedIndex);
            string extra = cmbFormat.SelectedIndex == 3 ? "-x --audio-format mp3 --audio-quality 0" : "";
            bool playlist = chkPlaylist.Checked;
            int cs = cmbCookie.SelectedIndex;
            string cf = (cs == 5) ? _cfg.CookieFile : "";
            string tmpl = txtTemplate.Text.Trim();
            if (tmpl.Length == 0 || tmpl == _cfg.SingleTemplate || tmpl == _cfg.PlaylistTemplate)
            {
                // 勾选“下载整个合集”→ 合集模板：在下载目录下自动创建“合集名”文件夹分集保存；
                // 未勾选 → 单视频模板：直接存在下载目录
                if (playlist) tmpl = _cfg.PlaylistTemplate;
                else tmpl = _cfg.SingleTemplate;
                txtTemplate.Text = tmpl;
            }
            DownloadTask t = _tm.AddTask(url, fmt, extra, playlist, cs, cf, _cfg.ProxyUrl, tmpl, _cfg.SingleTemplate, txtDir.Text.Trim(), true);
            grid.Rows.Add(new object[] { t.Title, "排队中", "", "", "" });
            grid.Rows[grid.Rows.Count - 1].Tag = t;
            if (playlist) Log("添加任务: " + url + "（合集模式：视频将保存到 下载目录\\合集名\\ 文件夹下）");
            else Log("添加任务: " + url);
            _tm.StartAll();
            // 清空输入框并聚焦，便于连续添加多个链接
            txtUrl.Clear();
            txtUrl.Focus();
        }

        private string BuildFormatFor(int idx)
        {
            switch (idx)
            {
                case 0: return "bestvideo*+bestaudio/best";
                case 1: return "bv*[height<=1080]+ba/b[height<=1080]/b";
                case 2: return "bv*[height<=720]+ba/b[height<=720]/b";
                case 3: return "bestaudio/best";
                case 4: return _cfg.CustomFormat;
                default: return "bestvideo*+bestaudio/best";
            }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog d = new FolderBrowserDialog();
            d.Description = "选择下载保存目录";
            if (d.ShowDialog(this) == DialogResult.OK) txtDir.Text = d.SelectedPath;
        }

        private void btnOpenDir_Click(object sender, EventArgs e)
        {
            string dir = txtDir.Text.Trim();
            if (!Directory.Exists(dir)) { try { Directory.CreateDirectory(dir); } catch { } }
            try { System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\""); } catch { }
        }

        private void cmbCookie_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnCookieFile.Visible = (cmbCookie.SelectedIndex == 5);
            if (cmbCookie.SelectedIndex != 5) _cfg.CookieSource = cmbCookie.SelectedIndex;
        }

        private void btnCookieFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "cookies 文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
            if (d.ShowDialog(this) == DialogResult.OK)
            {
                _cfg.CookieFile = d.FileName;
                Log("cookies 文件: " + d.FileName);
            }
        }

        private void nupParallel_ValueChanged(object sender, EventArgs e)
        {
            _tm.MaxParallel = (int)nupParallel.Value;
            _cfg.MaxParallel = (int)nupParallel.Value;
        }

        private void chkPlaylist_CheckedChanged(object sender, EventArgs e)
        {
            string cur = txtTemplate.Text.Trim();
            if (chkPlaylist.Checked && (cur == _cfg.SingleTemplate || cur.Length == 0)) txtTemplate.Text = _cfg.PlaylistTemplate;
            if (!chkPlaylist.Checked && (cur == _cfg.PlaylistTemplate || cur.Length == 0)) txtTemplate.Text = _cfg.SingleTemplate;
            _cfg.PlaylistMode = chkPlaylist.Checked;
        }

        private void cmbMirror_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (cmbMirror.SelectedIndex)
            {
                case 0: _cfg.Mirror = ""; break;
                case 1: _cfg.Mirror = "https://ghfast.top/"; break;
                case 2: _cfg.Mirror = "https://gh-proxy.com/"; break;
                case 3: _cfg.Mirror = "https://ghproxy.net/"; break;
            }
        }

        private void btnStartAll_Click(object sender, EventArgs e) { _tm.StartAll(); Log("已开始全部任务"); }
        private void btnStopSel_Click(object sender, EventArgs e)
        {
            if (grid.CurrentRow == null) return;
            DownloadTask t = grid.CurrentRow.Tag as DownloadTask;
            if (t != null) { _tm.StopTask(t); Log("已停止任务: " + t.Title); }
        }
        private void btnRemoveSel_Click(object sender, EventArgs e)
        {
            if (grid.CurrentRow == null) return;
            DownloadTask t = grid.CurrentRow.Tag as DownloadTask;
            if (t != null) _tm.StopTask(t);
            grid.Rows.Remove(grid.CurrentRow);
        }
        private void btnClearAll_Click(object sender, EventArgs e)
        {
            if (grid.Rows.Count == 0) return;
            if (MessageBox.Show("确定移除全部任务？（进行中的任务会被停止）", "清空列表", MessageBoxButtons.OKCancel) == DialogResult.OK)
            {
                _tm.Clear();
                grid.Rows.Clear();
            }
        }
        private void btnClearLog_Click(object sender, EventArgs e) { LogBox.Clear(); }
        private void btnUpdateYtd_Click(object sender, EventArgs e) { ThreadPool.QueueUserWorkItem(delegate(object o) { RunUpdateCheck(); }); }
        private void btnInstallFfmpeg_Click(object sender, EventArgs e) { ThreadPool.QueueUserWorkItem(delegate(object o) { RunFfmpegInstall(); }); }

        private void Timer_Tick(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                DownloadTask t = row.Tag as DownloadTask;
                if (t == null) continue;
                row.Cells[0].Value = (t.Title.Length > 0 && t.Title != t.Url) ? t.Title : t.Url;
                row.Cells[1].Value = StatusText(t.State);
                row.Cells[1].Style.ForeColor = StatusColor(t.State);
                row.Cells[2].Value = t.ProgressText;
                row.Cells[3].Value = t.SpeedText;
                row.Cells[4].Value = t.OutFile;
            }
        }

        private static string StatusText(TaskState s)
        {
            switch (s)
            {
                case TaskState.Idle: return "空闲";
                case TaskState.Queued: return "排队中";
                case TaskState.Running: return "下载中";
                case TaskState.Completed: return "完成";
                case TaskState.Failed: return "失败";
            }
            return "";
        }

        private static Color StatusColor(TaskState s)
        {
            switch (s)
            {
                case TaskState.Running: return Color.DodgerBlue;
                case TaskState.Completed: return Color.ForestGreen;
                case TaskState.Failed: return Color.Crimson;
                default: return Color.Gray;
            }
        }

        // ================= 启动检查与更新 =================
        private void MainForm_Shown(object sender, EventArgs e)
        {
            // 迁移场景：配置里的下载目录在新电脑上可能不存在（盘符/用户名不同），自动回退默认目录
            try
            {
                string d = txtDir.Text.Trim();
                if (d.Length == 0 || !Directory.Exists(d))
                {
                    string fallback = Util.DefaultDownloadDir();
                    txtDir.Text = fallback;
                    Log("配置中的下载目录在本机不存在，已改用默认下载目录：" + fallback);
                }
            }
            catch { }
            ThreadPool.QueueUserWorkItem(delegate(object o) { RunUpdateCheck(); });
        }

        private void RunUpdateCheck()
        {
            SetUiStatus(true);
            try
            {
                bool ok = UpdateManager.EnsureYtDlpLatest(_cfg,
                    delegate(string msg) { Log(msg); },
                    delegate(double pct) { SetProgress(pct); });
            }
            catch (Exception ex) { Log("更新检查异常: " + ex.Message); }
            finally
            {
                SetProgress(-1);
                SetUiStatus(false);
                FfmpegLabel();
            }
        }

        private void RunFfmpegInstall()
        {
            SetUiStatus(true);
            try
            {
                UpdateManager.InstallFfmpeg(_cfg,
                    delegate(string msg) { Log(msg); },
                    delegate(double pct) { SetProgress(pct); });
            }
            catch (Exception ex) { Log("ffmpeg 安装异常: " + ex.Message); }
            finally
            {
                SetProgress(-1);
                SetUiStatus(false);
                FfmpegLabel();
            }
        }

        private void FfmpegLabel()
        {
            if (InvokeRequired) { Invoke(new Action(FfmpegLabel)); return; }
            if (!System.IO.File.Exists(Util.FfmpegPath))
            {
                lblFfmpeg.Text = "ffmpeg：未安装（B 站等直出格式可用；需合并音视频的平台建议安装）";
                lblFfmpeg.ForeColor = Color.DarkOrange;
                return;
            }
            if (UpdateManager.FfmpegArchMismatch())
            {
                lblFfmpeg.Text = "ffmpeg：架构与本机不匹配（从其他电脑拷贝的），请点\"安装 ffmpeg\"重新下载对应版本";
                lblFfmpeg.ForeColor = Color.Crimson;
                return;
            }
            lblFfmpeg.Text = "ffmpeg：已就绪 ✓" + (Util.IsArm64() ? "（ARM64）" : "");
            lblFfmpeg.ForeColor = Color.ForestGreen;
        }

        private void SetUiStatus(bool busy)
        {
            if (InvokeRequired) { Invoke(new Action<bool>(SetUiStatus), busy); return; }
            stProgress.Visible = busy;
            if (!busy) stStatus.Text = "就绪";
        }

        private void SetProgress(double pct)
        {
            if (InvokeRequired) { Invoke((Action)(delegate { SetProgress(pct); })); return; }
            if (pct < 0) { stProgress.Visible = false; stStatus.Text = "就绪"; return; }
            stProgress.Visible = true;
            stProgress.Value = (int)Math.Max(0, Math.Min(100, pct));
            stStatus.Text = "更新进度 " + (int)pct + "%";
        }

        private void Log(string msg)
        {
            if (InvokeRequired) { Invoke(new Action<string>(Log), msg); return; }
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine;
            LogBox.AppendText(line);
            if (LogBox.TextLength > 60000) { LogBox.Clear(); LogBox.AppendText("[日志已清空]" + Environment.NewLine); }
            if (LogBox.IsHandleCreated) LogBox.Select(LogBox.TextLength, 0);
        }

        private void OnTaskFinished(DownloadTask t)
        {
            if (t.DetectNote.Length > 0) Log(t.DetectNote);
            if (t.State == TaskState.Completed) Log("完成: " + t.Title);
            else if (t.State == TaskState.Failed) Log("失败: " + t.Title + " → " + (t.ErrorText.Length > 0 ? t.ErrorText : "未知错误"));
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _timer.Stop();
            _tm.StopAll();
            _cfg.OutputDir = txtDir.Text;
            _cfg.MaxParallel = (int)nupParallel.Value;
            _cfg.FormatIndex = cmbFormat.SelectedIndex;
            _cfg.CookieSource = cmbCookie.SelectedIndex;
            _cfg.ProxyUrl = txtProxy.Text.Trim();
            _cfg.PlaylistMode = chkPlaylist.Checked;
            if (chkPlaylist.Checked) _cfg.PlaylistTemplate = txtTemplate.Text.Trim();
            else _cfg.SingleTemplate = txtTemplate.Text.Trim();
            _cfg.Save();
        }
    }
}