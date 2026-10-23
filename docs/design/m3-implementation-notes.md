# 9mRoom Windows 端 · M3 实现说明与限制

> 配套：[组件选型规范](m3-component-rules.md) · [页面审计](m3-page-audit.md)
> 技术栈：WPF / .NET 8 / MaterialDesignThemes 5.4（仅使用其 `PackIcon` 字形，控件模板全部自绘）

## 1. 官方文档取证与限制

执行环境对 `m3.material.io` 与 `developer.android.com` 的直接 HTTPS 访问被网络策略阻断
（域名解析到非公网地址，TLS 握手失败）。因此**没有逐页阅读官方页面全文**，也没有声称阅读过。
本项目的规范依据来自：

- 官方页面入口与组件目录的公开检索结果（URL 见规范文档第 0 节）；
- Material Design 3 已公开发布的组件语义：变体划分、状态层、形状与尺寸、使用条件。

凡是无法从官方页面核实的细节（例如 M3 Expressive 最新 Button group 的精确规格），在本文
第 5 节标注为「不确定」并按保守方案实现。若后续可访问官方文档，应优先复核这些条目。

## 2. 主题与公共组件落地方式

| 层 | 文件 | 内容 |
| --- | --- | --- |
| Token | `design/tokens.json` → `Themes/Tokens.xaml` / `TokensLight.xaml` | M3 语义色角色（深/浅）、typescale、shape、size、state、motion，由 `design/generate-tokens.ps1` 生成 |
| 字阶 | `Themes/M3/Typography.xaml` | 15 个 M3 typescale 角色 + 应用语义别名（PageTitle / SectionTitle / ItemSupporting…） |
| 组件 | `Themes/M3/Buttons.xaml` | Filled / Filled tonal / Outlined / Text / Icon / Caption / Assist chip；统一 hover / pressed / focus / disabled / busy 状态 |
| 组件 | `Themes/M3/Selection.xaml` | Radio、Checkbox、Switch、Segmented single-select（含方向键）、Icon toggle |
| 组件 | `Themes/M3/Inputs.xaml` | Filled text field（浮动标签、helper、错误态、只读态）、Search field、M3 下拉、Slider |
| 组件 | `Themes/M3/Navigation.xaml` | Expanded / collapsed Navigation rail、Primary tabs（共享 3dp 指示器 + 位移动画） |
| 组件 | `Themes/M3/Containment.xaml` | Card（filled / outlined）、List、Divider、Menu、Tooltip、Side sheet、Dialog 面、滚动条 |
| 组件 | `Themes/M3/Feedback.xaml` | Linear progress（确定/不确定）、常驻错误提示条 |
| 交互件 | `Controls/` | `M3Button`（IsBusy）、`M3SegmentedButton`、`NavigationRail`、`M3TabControl`、`M3DialogHost`（确认对话框）、`M3Snackbar`、`EmptyState`、`M3DateTimeDialog`（M3 日期网格 + 时间输入） |

主题装载顺序：`Tokens(.xaml|TokensLight)` → `Themes/M3.xaml` →（MDIX BundledTheme 仅提供图标字体）。
组件字典内部对 token 使用 `DynamicResource`，避免浅色主题被深色字典覆盖。

## 3. 技术栈适配说明

- **不把 MDIX 默认样式当成 M3 规范**：除 `PackIcon` 字形外，所有控件模板均为自绘，颜色/状态/形状来自本项目 token。
- **状态层替代 Ripple**：WPF 无官方 ripple；统一以 8% hover / 10% pressed / 3dp 焦点环表达状态，
  并保证选中态同时具备形状、勾选图标或文本变化，不只靠颜色。
- **焦点环**：3dp primary，替代系统虚线框；`FocusVisualStyle` 置空，但 Tab 顺序保持不变。
- **文字颜色继承**：根元素设置 `TextElement.Foreground`，控件模板在 ContentPresenter 上重设，
  避免模板内的 TextBlock 被全局隐式样式抢占前景色。
- **布局宽度**：页面使用单列、可换行的 4dp 网格布局；宽窗口下内容自然伸展，不强制固定最大宽度
  （早期尝试过 `MaxWidth + Stretch`，在 WPF 中会退化为按内容宽度排列，已移除）。
- **日期选择器**：WPF `Calendar` 的模板深度绑定系统主题（蓝色标题栏、英文月份），与 M3 冲突；
  改为自绘 6×7 日期网格（周一为首日、选中/今天状态、月份切换），时间采用 M3 time input 变体。

## 4. 已实现的官方组件清单

| 官方组件 | 本项目实现 | 备注 |
| --- | --- | --- |
| Buttons（filled / tonal / outlined / text） | ✅ | 共享模板；8dp 圆角矩形（见 5.1） |
| Icon button（standard / filled / toggle） | ✅ | 40×40，Tooltip + Automation 名称 |
| Segmented button（single-select） | ✅ | 5 段状态选择、时长、排序、预设、字重；←/→ Home/End |
| Chips（assist） | ✅ | 同步块「添加」动作 |
| Navigation rail（expanded / collapsed） | ✅ | 240 ↔ 80，`<1120px` 折叠；ListBox 键盘导航 |
| Primary tabs | ✅ | 48 高、3dp 指示器、位移动画、方向键切换 |
| Text fields（filled） | ✅ | 浮动标签、helper、错误态、只读态；搜索字段带前导图标 |
| Menu（dropdown） | ✅ | 应用分类；菜单面 4dp 圆角、48 高条目、选中勾选 |
| Menus（context） | ✅ | 待办行「更多」、应用栏溢出；支持图标 + 快捷键文本 |
| Radio / Checkbox / Switch | ✅ | 严格区分即时设置与延迟提交 |
| Slider | ✅ | 4dp 轨道 + 4×44 手柄；每日目标、叠加层不透明度 |
| Dialog（basic） | ✅ | 加入房间、确认对话框、Font/Border/Category 编辑 |
| Side sheet | ✅ | 成员详情、应用规则（桌面右侧，400dp，32% scrim） |
| Lists | ✅ | 成员、日志、应用、待办、分类、字体、消息 |
| Tooltip | ✅ | 图标按钮用途，不承载关键信息 |
| Snackbar | ✅ | 真实事件反馈；同一时间一条；可选动作 |
| Progress indicator（linear） | ✅ | 目标进度（确定）+ 重连（不确定） |
| Empty state | ✅（组合实现） | 图标 + 标题 + 说明 + 可选主操作 |
| Date picker / time input | ✅ | 自绘 M3 日期网格 + 24 小时制时间输入 |
| Top app bar | ✅ | 自绘标题栏：品牌、持久连接状态、窗口控制、溢出菜单 |

## 5. 与官方规范的已知差异（必须如实记录）

### 5.1 按钮形状：圆角矩形而非 full

按项目负责人要求，**所有按钮（Filled/Tonal/Outlined/Text/Icon/日期格/段按钮外缘）统一使用
8dp 圆角矩形**，覆盖 M3 默认的 full（胶囊）形状。这是有意的项目级覆盖，不宣称为 M3 原生形状。
导航指示器（56×32 pill）、头像、聊天气泡、开关轨道等非按钮元素保持其原有形状。

### 5.2 其他差异

| 差异 | 原因 | 影响 |
| --- | --- | --- |
| 无 ripple 扩散动画 | WPF 无内置 ripple，性能与实现成本高 | 用状态层 + 动效表达；不声称具备 ripple |
| 无 FAB | 创建动作都在固定位置，桌面无单手可达性诉求 | 创建按钮为所在区块的 Filled button / Assist chip |
| 无 Modal navigation drawer | 窗口最小宽度 900，rail 已可在窄窗口折叠为 80 | 未引入抽屉，避免与 rail 语义重复 |
| 对话框动作使用 Text button | 官方 Dialog 规范 | 「加入房间」「确定」等为主色文字按钮 |
| Side sheet 宽 400 且不可拖拽调整 | 官方建议 256–400 | 表单较长时可滚动 |
| Menu 不支持子菜单 | 当前所有菜单均为扁平命令 | 后续需要时再补级联模板 |
| 没有分割按钮 / 按钮组（动作型） | 无对应业务场景 | 未为覆盖清单而添加功能 |
| 托盘右键菜单为 WinForms 原生菜单 | 系统级菜单无法套用 WPF 模板 | 保留原生实现，深浅色由系统决定 |
| 叠加层 HUD 保留用户自定义主题 | 用户可配置背景/字体/圆角（既有功能） | 仅状态色与字体回退到 token |
| 无富文本 Tooltip / Badge / Carousel / Banner 组件 | 业务无需求 | 未实现 |

### 5.3 不确定项

- M3 Expressive 的 Connected button group 与经典 Segmented button 的最新推荐边界：本实现采用
  「Segmented single-select」语义（选中勾选 + 互斥），可在后续按官方更新调整。
- 官方对桌面端 Side sheet 最大宽度的最新建议：本实现取 400dp。

## 6. 构建与验证

### 6.1 构建

```
# 环境（本仓库自带的隔离 SDK 与包缓存）
$env:DOTNET_CLI_HOME="$PWD\.tools\dotnet-home"
$env:NUGET_PACKAGES="$PWD\.tools\nuget"
$env:HOME / APPDATA / LOCALAPPDATA / TEMP → .tools/...

# 生成 token（Tokens.xaml 是生成物）
powershell -ExecutionPolicy Bypass -File design\generate-tokens.ps1

# 构建客户端
.tools\dotnet\dotnet.exe build clients\windows\src\NineMRoom.Client.App\NineMRoom.Client.App.csproj -c Debug
```

结果：**0 error / 0 warning**（修复了既有 CS8602 警告）。

### 6.2 UI 冒烟渲染

`tmp/UiSmokeHost`（未入库，`tmp/` 已被忽略）把真实客户端源码编译进一个离屏宿主，
在 `-20000,-20000` 位置显示窗口后逐页 `RenderTargetBitmap`：

- 覆盖：主窗口（展开/折叠导航、tab 指示器）、专注、待办、状态、应用、设置、聊天、
  加入房间、应用规则面板、空状态、分类/背景/字体对话框、颜色拾取、日期时间对话框、确认对话框；
- 深浅两套主题各渲染一遍（`shots/` 与 `shots-light/`）；
- 结果：全部页面加载与渲染成功，无缺失资源、无 XAML 异常、无布局崩溃；
- 确认对话框做了行为测试：触发「离开房间」→ 点击确认 → `Task<bool>` 返回 `True`。

### 6.3 未自动验证的部分（需人工/联机）

- 真实服务器连接、加入房间、断线重连的端到端流程（需要本地服务端）；
- 托盘图标与系统通知（受运行环境权限限制，本次沙箱内托盘不可用）；
- 全局快捷键、叠加层实际窗口（HUD 保留用户主题，未在本轮重做）；
- 键盘 Tab 顺序全量巡检、屏幕阅读器朗读、高 DPI 150%/200% 缩放；
- 应用规则保存、导出/导入主题等依赖文件对话框的路径。

## 7. 后续建议

1. 若可访问官方文档，复核 5.3 的不确定项，并把 Button group / Segmented 的最新规范同步到规范文档；
2. 为键盘用户补「同步块上移/下移」菜单项（拖拽目前为指针操作）；
3. 增加 UI 自动化（UIA）测试，覆盖状态切换、连接确认、加入房间；
4. 若后续引入图表/日历等复杂组件，继续沿用「先查官方组件、再定自绘模板」的流程。

## 8. Outlined text field 选型与联机验证（2026-10-23）

### 8.1 组件库 Outlined text field 试接结论

按要求直接引用 `MaterialDesignThemes` 的 `MaterialDesignOutlinedTextBox` 试接，确认两点问题：

- 其浮动标签绘制在控件上边界之外，在 56dp 高度、本项目字体与 4dp 网格下，标签顶部会被裁切；
- 其 helper/footer 位于控件底部之外，会压到下一行控件。

最终保留项目内按同一规范实现的 outlined 模板（1dp 描边、4dp 圆角、缺口标签、前导图标、清除按钮、只读保留完整描边），
只继续使用组件库的 PackIcon 与基础主题；该决定是“视觉正确优先”的偏差，已在验收说明中标注。

### 8.2 真实服务器联机结果

- 服务器：`http://9mroom.ikuyo.top`
- 房间：`testRoom`（服务端显示为“测试房间”）
- 昵称：`test`
- 结果：连接成功；加入房间成功；成员列表出现 `test`（online）；房间日志出现“已进入房间 测试房间”“test 开始专注”；
  同步状态读取到本机应用与音乐信息；测试结束后已离开房间。
