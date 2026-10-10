# VideoGrabber

基于 [yt-dlp](https://github.com/yt-dlp/yt-dlp) 内核的 Windows 视频下载器，绿色便携，无需安装。特别优化了 B 站视频与合集的下载体验。

![.NET Framework 4.0+](https://img.shields.io/badge/.NET%20Framework-4.0%2B-blue) ![Windows](https://img.shields.io/badge/platform-Windows%20x64%2FARM64-success) ![License: MIT](https://img.shields.io/badge/license-MIT-green)

## 功能特性

- **内核自动维护**：每次启动自动对比 GitHub 最新版本号，不存在或非最新时自动下载更新；官方源失败自动回退镜像源
- **B 站合集下载**：自动识别链接类型——合集/分P/播放列表 → 在下载目录下创建**以合集名命名的文件夹**，按 `序号 - 标题 [BV号]` 分集保存
- **单视频智能回退**：勾选合集模式但粘贴的是单视频链接时，自动改用单文件模板直接保存，不误建文件夹
- **多任务并行**：任务队列 + 1~8 可调并行数，实时进度 / 速度 / 输出路径显示
- **登录态支持**：直接读取 Chrome / Edge / Firefox 的 B 站登录 Cookie（B 站取流必需），或使用 cookies.txt
- **ffmpeg 一键安装**：需要合并音视频的平台（YouTube 等）点一下即自动下载安装对应架构版本
- **x64 / ARM64 双架构**：自动检测 CPU 架构并下载匹配的 yt-dlp / ffmpeg 原生版本，从其他架构电脑迁移时自动替换
- **UTF-8 输出**：强制 yt-dlp UTF-8 输出，彻底解决 Windows 下中文乱码
- **绿色便携**：单主程序 + 同目录 yt-dlp.exe，配置自动持久化

## 下载使用

从 [Releases](../../releases) 下载 `VideoGrabber-portable.zip`，解压后直接运行 `VideoGrabber.exe`：

1. 首次启动自动下载最新版 yt-dlp（x64 / ARM64 自动适配）
2. 粘贴视频 / 合集链接 → 点"添加任务"自动开始
3. B 站下载建议在"登录 Cookie"选择已登录 B 站的浏览器（下载时保持浏览器关闭）

### B 站下载要点

| 场景 | 操作 |
|------|------|
| 单个视频 | 直接粘贴 `https://www.bilibili.com/video/BVxxxx` |
| 合集 / 剧集 / 番剧 | 在视频页点击**合集名称**进入合集页，复制合集页链接（`space.bilibili.com/…/channel/collectiondetail?sid=…`），保持勾选"下载整个合集" |
| 需要 login 的高清/大会员内容 | "登录 Cookie"选择对应浏览器 |

> B 站合集内的**视频页链接**（带 `spm_id_from=…videopod.sections` 参数）只是单个视频，软件会自动识别并提示如何获取合集页链接。

## 项目结构

```
├── src/
│   ├── Program.cs        入口 + 全局异常捕获
│   ├── AppConfig.cs      配置持久化 / CPU 架构与 PE 头检测
│   ├── UpdateManager.cs  yt-dlp 自检更新 / ffmpeg 安装（按架构选资产）
│   ├── DownloadTask.cs   下载任务（yt-dlp 进程管理 / 输出解析 / 合集探测）
│   ├── TaskManager.cs    任务队列与并行调度
│   └── MainForm.cs       WinForms 界面（Anchor 自适应布局）
├── tests/
│   ├── TestHarness.cs    yt-dlp 更新逻辑控制台测试台
│   └── ArchTest.cs       架构检测单元测试
└── assets/
    └── app.ico           应用图标（iOS 风格灰底白闪电）
```

## 从源码编译

无需 Visual Studio，系统自带的 .NET Framework 编译器即可：

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe ^
  /win32icon:assets\app.ico ^
  /out:VideoGrabber.exe ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  src\*.cs
```

> `tests\` 下的两个测试台为独立控制台程序，编译主程序时无需包含。

## License

[MIT](LICENSE)