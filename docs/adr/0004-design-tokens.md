# 0004 视觉唯一来源：tokens.json 生成 WPF 资源

- 日期：2026-10-20
- 状态：已接受

## 背景

Windows 与 Android 要共用一套视觉语言，但两端使用不同的 UI 技术（WPF / Compose）。
如果颜色、字号、间距各自手写，两端必然漂移，而且改一次要改两处。

## 决定

- `design/tokens.json` 是唯一视觉来源，只放数值，不放组件样式；
- `design/generate-tokens.ps1` 生成 `Tokens.xaml`（WPF ResourceDictionary）；
- 生成物不入库（已在 `.gitignore`），`clients/windows/build.ps1` 在构建前自动生成；
- 后续 Compose 端由同一份 tokens 生成 `Theme.kt`，生成脚本可以再加一个输出。

## 后果

- 改颜色/字阶只需改 tokens 并重新构建，两端不会漂移；
- 生成物不提交，克隆后必须先构建（脚本已处理），单独打开 XAML 会看不到资源；
- 组件样式（圆角卡片、按钮模板）仍手写在 XAML 中，只引用 tokens；
- 什么时候重新考虑：组件数量增长到需要跨端复用结构时，再评估是否引入设计系统库。
