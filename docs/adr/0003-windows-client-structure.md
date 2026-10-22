# 0003 Windows 客户端结构：Core + CLI 探针 + 后续 WPF

- 日期：2026-10-20
- 状态：已接受

## 背景

M1 阶段只需要验证「前台应用 + 音乐 + 空闲」的采集与上报，但产品最终是常驻托盘、
带房间界面的桌面应用。如果一开始就把采集逻辑写进 WPF 窗口代码，后续既无法在无界面环境验证，
也难以复用。

## 决定

- `NineMRoom.Client.Core`：类库，负责 Win32 / SMTC 采集、appId 映射、状态块组装、WebSocket 上报；
- `NineMRoom.Client.Cli`：控制台探针，负责验证与调试，不承担产品 UI；
- 后续新增 `NineMRoom.Client.App`（WPF）只做界面与托盘，引用 Core；
- 不使用 WinUI 3 或 MAUI：现有 WPF 经验更足，.NET 8 运行时稳定，Windows 10 兼容范围更大。

## 后果

- 采集逻辑可以在无界面下运行和验证（本轮已用 CLI + 服务端 `/debug/room` 完成端到端验证）；
- WPF 界面尚未实现，M3 需要新增项目；
- CLI 会长期保留，作为协议与采集的回归工具；
- Core 只依赖 .NET 8 与 Windows SDK 投影包，没有第三方依赖。
