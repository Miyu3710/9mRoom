# 9mRoom · Windows 客户端

## 当前状态

**Windows 1.2.0：采集 + 常驻化 + Material Design 3 界面。** 前台窗口、SMTC 音乐、空闲检测、appId 映射、WebSocket 上报都已打通；
客户端支持托盘、关闭到托盘、单实例、开机自启、配置持久化与断线重连。

## 1.2.0 更新摘要

- 全客户端迁移 Material Design 3：`MaterialDesignThemes 5.4.0`，深/浅两套语义色与 M3 组件
- 一级导航改为可折叠 NavigationRail，入口为：自习 / 专注 / 待办 / 状态 / 应用 / 设置
- 顶部持久连接状态与重连进度；房间标签改为浏览器式 M3 tabs
- 专注新增“专注 / 休息”循环，可分别设置时长
- 待办改为 M3 列表 + 编辑态，加入日期/时间选择器
- 应用页恢复磁贴列表，规则侧边面板与分类管理统一为 M3
- 托盘菜单改用 Windows 原生深色菜单
- MSI 改为 per-machine / x64，默认 Program Files，保留自定义路径；1.2.0 只发布 MSI

完整更新日志见 [`../../Fix.md`](../../Fix.md) 的“Windows 1.2.0 更新日志”。

## 目录

```
clients/windows/
├─ src/
│  ├─ NineMRoom.Client.Core/     采集与协议（可复用类库）
│  │  ├─ Native.cs                Win32：前台窗口 / 空闲 / 进程名
│  │  ├─ ForegroundWatcher.cs     前台应用轮询与变化事件
│  │  ├─ MediaWatcher.cs          SMTC 媒体会话
│  │  ├─ AppCatalog.cs            进程名 → appId / 类别（读 shared/app-catalog）
│  │  ├─ StatusComposer.cs        组装状态块
│  │  ├─ StatusRenderer.cs        状态块渲染（与 fixtures 对齐）
│  │  └─ PresenceClient.cs        WebSocket 上报与房间成员
│  ├─ NineMRoom.Client.Cli/       无界面探针（验证采集与协议）
│  └─ NineMRoom.Client.App/       WPF 界面
│     ├─ Controls/                M3 控件：导航栏、按钮、字段、标签、对话框、Snackbar
│     ├─ Services/                托盘 / 单实例 / 设置 / 主题 / 自启
│     ├─ ViewModels/              主界面状态与成员同步
│     ├─ Themes/M3/               MD3 按钮、输入、选择、导航、排版组件
│     ├─ Themes/Tokens.xaml       深色语义令牌，由 design/tokens.json 生成
│     ├─ Themes/TokensLight.xaml  浅色语义令牌
│     └─ app.manifest             PerMonitorV2 DPI
├─ build.ps1
├─ run.ps1                        运行探针
└─ run-app.ps1                    运行 WPF 客户端
```

## 构建与运行

```powershell
# 服务端
..\..\server\run.ps1

# 客户端（自动生成 tokens 并构建 CLI + WPF）
.\build.ps1
.\run-app.ps1
```

启动后会自动连接（可在连接栏取消「自动连接」）。开机自启时使用 `--minimized` 静默启动到托盘：

```powershell
.\run-app.ps1 --minimized
```

关闭窗口默认隐藏到托盘；托盘菜单可以显示主窗或退出。

无界面探针仍保留，用于排查采集问题：

```powershell
.\run.ps1 --seconds 20 --room demo --name WindowsProbe
```

| 探针参数 | 说明 |
| --- | --- |
| `--server` | 服务端地址，默认 `http://127.0.0.1:5080` |
| `--room` / `--name` | 房间与昵称 |
| `--seconds` | 运行秒数，0 表示直到 Ctrl+C |
| `--no-send` | 只采集，不连接服务端 |
| `--show-title` | 控制台显示窗口标题（默认不显示，也永不上传） |
| `--log` | 追加写入日志文件 |
| `--render-check <fixtures.json>` | 跑状态块渲染一致性用例 |

## 已实现

- 前台窗口：`GetForegroundWindow` + 进程名，每秒轮询；UWP 应用会解析 `ApplicationFrameHost` 的子窗口
- SMTC：标题、歌手、专辑、播放状态、进度与总长，2 秒刷新
- 空闲：`GetLastInputInfo`，满 5 分钟把 mode 切到 `away`
- 应用映射：`shared/app-catalog/catalog.json`，未收录归 `other`
- 上报：每 3 秒 `presence.update`，块为应用 / 音乐 / 计时 / 待办 / 自定义文本
- 房间成员：解析 snapshot 与 presence.changed，界面实时更新
- 状态块渲染：C# 实现与 `shared/status-blocks/fixtures.json` 全部用例一致
- 视觉：Material Design 3；`design/tokens.json` 生成深/浅两套 WPF 语义令牌
- 常驻：托盘图标与原生菜单、关闭到托盘、单实例激活、静默启动参数
- 可靠性：断线指数退避重连、界面显示上次同步时间
- 配置：服务器 / 房间 / 昵称 / 自启 / 自动连接落盘；写入失败时自动回落到 exe 同目录
- DPI：PerMonitorV2 manifest
- 设置页：M3 分组列表，含启动与常驻、外观（浅色主题）、隐私（音乐共享）、通知、叠加层（开关 / 点击穿透 / 预设 / 自定义主题）、版本、配置与日志目录
- 叠加层主题：预设 / 自定义；颜色画盘 + 十六进制、系统字体选择、字号、字重、单行自动换行，实时预览，支持导入导出 JSON
- 连接流程：先填写服务器地址连接服务器（`/healthz`），不进入房间；房间页显示空状态，点“加入房间”打开 M3 对话框，输入房间号与昵称
- 加入选项：可记住默认昵称；仅服务端创建的房间可设为“家”，下次连接自动加入
- 自习室四标签：状态 / 房间 / 日志 / 聊天；聊天为本次连接、500 字、服务端限频
- 手动状态：自动 / 专注 / 休息 / 离开 / 隐身；隐身时不展示状态
- 番茄计时：专注 / 休息循环，可分别设置时长；支持开始、暂停、重置，运行中作为 timer 块同步
- 待办页：标题、可选起止时间、子任务；全部子任务完成才算完成，支持置顶；状态只同步标题与进度
- 应用页：磁贴列出探测到的应用，支持搜索与最近使用 / 名称 / 分类排序；点击磁贴以 M3 侧边面板查看规则
- 应用规则面板：显示可执行文件路径，可改显示名、自定义图标，覆盖模板 / 分类，支持忽略与隐藏标题；图标文件缓存，失败回退文字
- 状态页：预览置顶；块显隐、拖拽（跟手预览 + 插入线）、增删、模板覆盖；自定义块最多 5 个
- 全局快捷键：`Ctrl+Alt+Shift+T` 开始/暂停计时，`Ctrl+Alt+H` 显示/隐藏叠加层
- 隐私：音乐共享开关；窗口标题默认关闭，开启后为黑名单模式
- 专注统计：本地按天累计，近 7 天与合计，可关闭
- 每日目标：目标分钟数可设，状态卡显示进度条与连续达标天数
- 叠加层连接灯：已连接绿色、重连黄色、离线灰色
- 设置在字体 / 背景窗口中修改时，叠加层实时预览；取消恢复原主题
- 字体列表优先显示中文名，滚轮一次一项；字号直接输入；颜色选择提供完整画盘
- 设置“关于”支持检查 GitHub Releases 更新，发现新版本可直接前往下载
- 应用规则：文件位置可编辑；任意文件可提取关联图标；单应用模板 > 分类模板 > 全局模板
- 应用分类：可新增 / 删除 / 拖动排序分类，分类可配置 app 块模板；应用卡片显示分类名称
- 应用搜索：按显示名、原应用名、进程名、分类搜索；搜索框在排序行右侧
- 自定义状态块：支持 `${...}` 在本机执行终端命令或 PowerShell 脚本，15 秒缓存、2.5 秒超时、输出清洗截断
- 导航：自习 / 专注 / 待办 / 状态 / 应用 / 设置；NavigationRail 可折叠，只有胶囊区域响应悬停
- 外观：无边框主窗 + 自绘标题栏 + M3 Top app bar，原生窗口最小化/最大化动画，浏览器式房间标签
- 打包：`packaging/package.ps1` 生成 per-machine / x64 MSI；1.2.0 只发布 MSI，不发布便携版；发布包要求 `-Sign` 签名，当前无证书，暂为未签名构建
- 性能：空闲约 0.05% CPU（16 线程）/ 约 225 MB（Debug）；发送间隔 3s / 空闲 15s / 隐身 30s / 暂停 0
- 多显示器：叠加层按所在显示器的工作区定位，并在该屏幕内重置位置
- 托盘：提示显示房间人数与状态；右键菜单使用 Windows 原生 `TrackPopupMenuEx` + 深色模式
- 成员详情侧边面板：模式、最近同步、今日专注、完整状态、窗口标题与设备，Esc 关闭
- 日志：按天写 `client-YYYYMMDD.log`，超过 4 MB 切分，保留 7 天
- 屏幕叠加层：透明置顶、不抢焦点、不进 Alt+Tab、可拖动并记忆位置；显示在线成员状态；四套预设
- 自定义主题：背景/文字/次要/边框色、字号、透明度、圆角、内边距、宽度、单/双行、位置；JSON 导入导出

## 未实现 / 待验收

- 100% / 150% DPI 与物理双屏的完整验收
- 浅色主题下全部页面与三个对话框的逐页截图
- 1.2.0 per-machine MSI 在 D 盘安装后的完整卸载实机验证
- 静默自动升级（当前为检查 + 下载 + 询问安装）
- 离线队列
- 代码签名（当前无证书，发布包未签名）
- 托盘原生菜单的深色模式依赖系统支持，无法自定义圆角/勾选样式

## 环境说明

**托盘图标要求正常权限启动。** Windows 会拒绝低完整性进程注册托盘（`Shell_NotifyIcon` 错误 5），
而当前自动化会话启动的子进程是 Low 完整性。因此调试时可能出现「进程在跑但没有托盘图标」：
此时客户端会提示并改为「关闭即退出」，不会留下找不到入口的后台进程。

从桌面双击 `9mRoom.cmd`（或直接运行 exe）会以正常权限启动，托盘应正常出现。
Windows 11 默认会把新图标放进任务栏的 `^` 隐藏区，可右键固定。

当前沙箱也会拒绝客户端进程写 `HKCU\...\Run`，所以「开机自启」在本机会回退为关闭并给出提示；
在普通桌面环境下应可正常写入。若遇到同类问题，备用方案是计划任务或启动文件夹。

本地 SDK 在仓库根的 `.tools/dotnet`，脚本会把 `DOTNET_CLI_HOME`、`NUGET_PACKAGES`、
`APPDATA`、`LOCALAPPDATA`、`TMP` 全部重定向到 `.tools/`。
当前机器上用户目录的临时目录权限异常，不重定向会导致 MSBuild 失败。

首次 restore 需要 `Microsoft.Windows.SDK.NET.Ref`（SMTC 投影），需要代理；
之后走本地缓存即可离线构建。
