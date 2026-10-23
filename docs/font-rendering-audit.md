# 字体渲染与 DPI 适配审计

## 结论摘要

DPI 适配本身是正确的，没有发现 Windows 对应用窗口进行位图缩放。

实际影响清晰度的原因主要来自三处：

1. 主窗口使用接近全透明的 `Background="#01000000"`，同时通过 DWM `DwmExtendFrameIntoClientArea` 扩展玻璃边框，文本可能落入非不透明合成表面，导致 WPF 退回灰度抗锯齿。
2. 主窗口全局使用 `TextFormattingMode="Ideal"`。在 125% 缩放下，11–14px 的小字号界面文字会出现亚像素定位，边缘偏软。
3. 成员详情和 App 编辑抽屉的打开动画使用 `Opacity` + `TranslateTransform`，动画结束后没有清除动画，WPF 可能保留中间合成层，使抽屉文字持续发糊。

## DPI 审计

- 目标框架：`net8.0-windows10.0.19041.0`
- Manifest：`app.manifest`
  - `dpiAware=true/pm`
  - `dpiAwareness=PerMonitorV2`
- 进程实测：`GetProcessDpiAwareness = 2`（PerMonitor DPI aware）
- 当前测试显示缩放：125%（系统 DPI 120）
- 980×720 DIP 窗口实测物理尺寸：1225×900，符合 1.25 倍缩放，没有位图放大
- 窗口使用 `WindowChrome` + `WM_GETMINMAXINFO`，最大化按显示器工作区计算
- 未修改 Manifest 和 DPI Awareness，因为配置本身正确

## WPF 文本设置审计

| 位置 | TextFormattingMode | TextRenderingMode | 说明 |
| --- | --- | --- | --- |
| MainWindow 根 | Ideal | ClearType | 大标题、正文、动态内容保持 Ideal |
| Label / NavItem / RoomTab | Display | 继承 ClearType | 11–14px 静态 UI 文字 |
| 小按钮 / 输入框 | Display | 继承 ClearType | 避免小字号亚像素发软 |
| 对话框 | Ideal | ClearType | 保持原有设置 |
| OverlayWindow | 默认/Ideal | 未强制 ClearType | 透明叠加层不使用强制 ClearType |

未发现 `Aliased` 渲染模式，也没有 `BitmapCache`。

## 透明度与合成层

- 主窗口背景由 `#01000000` 改为不透明 `BrushBg`
- `ShellBorder` 增加 `RenderOptions.ClearTypeHint="Enabled"`
- `MemberDrawerPanel`、`AppEditorPanel` 增加 `ClearTypeHint`
- 成员详情抽屉和 App 编辑器打开动画完成后：
  - 清除 `Opacity` 动画
  - 清除 `TranslateTransform.X` 动画
  - 把最终值固定为 `Opacity=1`、`X=0`
- 没有移除现有动画，只移除动画结束后的残留合成层

## 字体与回退

原字体族字符串使用中文族名和 `Medium` 子族名，WPF 实际解析会回退到 Noto Sans SC，导致字体和字重不完全可控。

现已下载并内嵌 **MiSans**，使用四个字重：

- `MiSans-Regular.ttf`
- `MiSans-Medium.ttf`
- `MiSans-Semibold.ttf`
- `MiSans-Bold.ttf`

WPF 字体 token：

```text
pack://application:,,,/Assets/Fonts/#MiSans, Microsoft YaHei UI
```

字体文件作为 `Resource` 编译进应用，不依赖用户系统安装。MiSans 是面向屏幕显示设计的字体，笔画在小字号下比当前回退字体更紧凑、边缘更清楚。

来源：

- 官方字体页：<https://hyperos.mi.com/font/zh/>
- 字体文件镜像：<https://github.com/boyan01/mi_sans_font>
- 许可证：MiSans 官方允许免费商用；仓库内附 `LICENSE-MiSans-Repo.txt`

## 布局舍入

- `UseLayoutRounding="True"`
- `SnapsToDevicePixels="True"`
- 保留，不把它们当作唯一修复手段。

## 测试结果

- 125%：已用 MiSans 截图核对主界面、连接/断开态、标签区；字体正常加载，无缺字，布局未退化。
- 100% / 150%：当前会话无法切换系统显示缩放（需要注销/重启会话）。没有伪造 100%/150% 的截图结论。
- 合成 `WM_DPICHANGED` 的测试没有让 WPF 接受模拟 DPI，因此不作为验证依据。
- 构建：`dotnet build -c Release` 通过，0 错误。

## 未修改项

- 没有把所有透明窗口强制为 ClearType
- 没有把全局字号放大
- 没有全局加粗
- 没有移除动画
- 只为小字号静态 UI 增加了 Display 渲染模式，大标题和动态内容仍使用 Ideal
