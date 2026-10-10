using System;
using System.Collections.Generic;
using System.Threading;

namespace VideoGrabber
{
    /// <summary>任务队列与并行调度（后台线程启动任务，UI 定时刷新）</summary>
    public class TaskManager
    {
        public List<DownloadTask> Tasks = new List<DownloadTask>();
        public int MaxParallel = 2;
        private int _nextId = 1;
        private readonly object _lock = new object();
        public Action<DownloadTask> OnTaskFinished;

        public DownloadTask AddTask(string url, string formatArgs, string extraArgs, bool playlist, int cookieSource,
            string cookieFile, string proxy, string template, string fallbackTemplate, string outputDir, bool useFfmpeg)
        {
            DownloadTask t = new DownloadTask();
            t.Id = _nextId++;
            t.Url = url;
            t.Title = url;
            t.FormatArgs = formatArgs;
            t.ExtraArgs = extraArgs;
            t.PlaylistMode = playlist;
            t.NeedDetect = playlist;          // 勾选合集模式时启动探测：单视频自动回退单文件模板
            t.FallbackTemplate = fallbackTemplate;
            t.CookieSource = cookieSource;
            t.CookieFile = cookieFile;
            t.ProxyUrl = proxy;
            t.Template = template;
            t.OutputDir = outputDir;
            t.UseFfmpegLoc = useFfmpeg;
            t.OnFinished = new Action<DownloadTask>(HandleFinished);
            lock (_lock) Tasks.Add(t);
            return t;
        }

        /// <summary>排队所有未完成/失败/初始任务并调度</summary>
        public void StartAll()
        {
            lock (_lock)
            {
                foreach (DownloadTask t in Tasks)
                {
                    if (t.State == TaskState.Idle || t.State == TaskState.Failed)
                    {
                        t.State = TaskState.Queued;
                    }
                }
            }
            Schedule();
        }

        public void StartTask(DownloadTask t)
        {
            lock (_lock)
            {
                if (t.State == TaskState.Idle || t.State == TaskState.Failed || t.State == TaskState.Queued)
                    t.State = TaskState.Queued;
                else return;
            }
            Schedule();
        }

        public void StopTask(DownloadTask t)
        {
            t.Kill();
            lock (_lock)
            {
                if (t.State == TaskState.Running || t.State == TaskState.Queued)
                {
                    t.State = TaskState.Idle;
                    t.ProgressText = "已停止";
                }
            }
            Schedule();
        }

        public void StopAll()
        {
            lock (_lock)
            {
                foreach (DownloadTask t in Tasks)
                {
                    if (t.State == TaskState.Running || t.State == TaskState.Queued)
                    {
                        t.Kill();
                        t.State = TaskState.Idle;
                        t.ProgressText = "已停止";
                    }
                }
            }
        }

        public void Clear()
        {
            StopAll();
            lock (_lock) Tasks.Clear();
        }

        private void Schedule()
        {
            // 在独立线程调度，避免占用 UI 线程
            ThreadPool.QueueUserWorkItem(delegate(object o) { DoSchedule(); });
        }

        private void DoSchedule()
        {
            while (true)
            {
                DownloadTask pick = null;
                lock (_lock)
                {
                    int running = 0;
                    foreach (DownloadTask t in Tasks)
                        if (t.State == TaskState.Running) running++;
                    if (running >= MaxParallel) return;
                    foreach (DownloadTask t in Tasks)
                    {
                        if (t.State == TaskState.Queued) { pick = t; break; }
                    }
                    if (pick == null) return;
                    pick.State = TaskState.Running;
                }
                DownloadTask current = pick; // 拷贝引用，避免闭包捕获循环变量
                ThreadPool.QueueUserWorkItem(delegate(object s2) { current.Run(); });
            }
        }

        private void HandleFinished(DownloadTask t)
        {
            // 通知 UI 任务结束（日志显示完成/失败原因）
            Action<DownloadTask> h = OnTaskFinished;
            if (h != null) h(t);
            if (OnTasksChanged != null) OnTasksChanged();
            Schedule();
        }

        public event Action OnTasksChanged;
    }
}