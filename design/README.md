# design

「统一 UI 风格」的落地物。跨端统一靠 tokens 与验收，不依赖同一个 UI 框架。

## 目录

| 文件 / 目录 | 内容 |
| --- | --- |
| `tokens.json` | M3 语义色角色（深/浅）、typescale、shape、size、state、motion —— 唯一来源 |
| `generate-tokens.ps1` | 由 `tokens.json` 生成 `Themes/Tokens.xaml`（深色）与 `Themes/TokensLight.xaml`（浅色） |
| `icons/` | SVG 源文件，两端各自转成可用格式（待补） |
| `reference/` | 参考实现截图，仅保留在设计阶段，不入公开仓库 |

组件选型与使用规范见 [`docs/design/m3-component-rules.md`](../docs/design/m3-component-rules.md)；
页面审计见 [`docs/design/m3-page-audit.md`](../docs/design/m3-page-audit.md)；
实现差异与限制见 [`docs/design/m3-implementation-notes.md`](../docs/design/m3-implementation-notes.md)。

## 生成方式

```powershell
powershell -ExecutionPolicy Bypass -File design\generate-tokens.ps1
```

生成物：`clients/windows/src/NineMRoom.Client.App/Themes/Tokens.xaml`（深色）、
`TokensLight.xaml`（浅色）。`Tokens.xaml` 按仓库约定不入库（`.gitignore`），
构建前需先运行生成脚本；Android 端的 `Theme.kt` 计划复用同一份 `tokens.json`。

## 原则

- 颜色使用 M3 语义角色（primary / surface / outline / error …），页面不硬编码色值；
- 字阶使用 M3 typescale，同类组件共用同一角色；
- 间距 4 的倍数；形状按组件固定（文本框 4 上圆角、Card 12、对话框 28、菜单 4）；
- **按钮形状为项目级覆盖：8dp 圆角矩形**（不使用 M3 默认 full/胶囊），理由见实现说明 5.1；
- 中文字体使用系统字体，避免字体授权与包体膨胀；
- 图标统一线性风格、24px 网格，图标按钮必须有可访问名称；
- emoji 在 Windows 与 Android 上字形不同，状态块只用 emoji 做前缀点缀，不承载唯一语义。

## 状态

Windows 端已完成 M3 token + 组件库 + 全页面重构；Android 端待开始。
