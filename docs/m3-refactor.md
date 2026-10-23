# Windows 客户端 Material Design 3 重构记录

## 技术栈与依赖

- WPF / .NET 8 / `net8.0-windows10.0.19041.0`
- `MaterialDesignThemes 5.4.0`（MaterialDesignInXamlToolkit，MD3 主题）
- 现有 MVVM、服务层和配置文件保持不变

## UI 清单（Windows 端）

| 类型 | 文件 | 处理 |
| --- | --- | --- |
| 主窗口 | `MainWindow.xaml` | 导航、标题栏、标签页、服务器条、状态区、成员抽屉、全局主题入口 |
| 页面 | `FocusView.xaml` | M3 卡片、排版、按钮、进度条、复选框 |
| 页面 | `TodoView.xaml` | M3 卡片、输入框、按钮、列表项 |
| 页面 | `AppView.xaml` | M3 筛选栏、搜索框、应用卡片、空状态 |
| 页面 | `SettingsView.xaml` | M3 设置分组、复选框、下拉框、输入框、外观主题 |
| 页面 | `LayoutView.xaml` | M3 分组、编辑器、列表 |
| 页面 | `AppEditorView.xaml` | M3 表单、分类按钮、复选框 |
| 页面 | `ChatPanel.xaml` | M3 消息面、输入条、内联发送按钮 |
| 子页 | `JoinRoomView.xaml` | M3 表单、复选框、操作按钮 |
| 窗口 | `BorderDialog.xaml` | M3 表面、图标关闭按钮、MD3 Slider |
| 窗口 | `CategoryDialog.xaml` | M3 表面、图标关闭按钮、列表 |
| 窗口 | `FontDialog.xaml` | M3 表面、字体选择、图标关闭按钮 |
| 弹出/控件 | `ColorPicker.xaml` | MD3 输入框、图标按钮、弹出表面 |
| 弹出/控件 | `FontPicker.xaml` | MD3 输入框、图标按钮、弹出列表 |
| 叠加层 | `OverlayWindow.xaml` | 保留用户自定义叠加层，改用统一字体与令牌 |

## 设计系统

- `Themes/Tokens.xaml`：深色语义令牌
- `Themes/TokensLight.xaml`：浅色语义令牌
- `Themes/Design.xaml`：M3 排版、卡片、按钮、输入框、对话框表面
- `Themes/Controls.xaml`：滚动条、`ModernTextBox`、`ModernButton`、`ModernPrimaryButton`、`ModernCheckBox`、`ModernRadioButton` 的库别名
- `Services/ThemeService.cs`：启动时选择浅/深色主题并加载对应令牌

原则：

- 按钮优先使用 `MaterialDesignFlatButton` / `MaterialDesignRaisedButton`
- 标签页使用 `MaterialDesignTabRadioButton`
- 复选框/单选框使用 `MaterialDesignCheckBox` / `MaterialDesignRadioButton`
- 输入框使用 `MaterialDesignFilledTextBox`
- 图标统一使用 `materialDesign:PackIcon`
- 不再为每个页面复制 `ControlTemplate`

## 主题

设置页新增“外观 → 使用浅色主题”。主题在下次启动时生效，原因是 WPF `StaticResource` 页面资源不能在运行中安全整体替换。

## 验证

- `dotnet build -c Release`：成功，0 错误
- 深色 125%：主界面、专注、待办、设置已实际启动截图
- 浅色 125%：主界面已实际启动截图
- 对话框：已改为共享 M3 资源并成功编译；未逐一到每个打开路径截图
- 100% / 150%：未在本会话切换系统缩放验证

## 已知限制

- 浅色/深色切换需要重启客户端
- `OverlayWindow` 保留用户自定义颜色/字体，不强制应用主题
- 现有 `Compact`/`Expanded` 状态切换仍在页面代码后置中，后续可迁移到 VisualState
- 仍需在不同窗口尺寸和 100%/150% DPI 下做一轮人工验收

## 第二轮定向审查

- 语义角色：Primary / OnPrimary / PrimaryContainer / OnPrimaryContainer / SurfaceContainer / Outline / Error。
- 导航与状态 chip 共用 PrimaryContainer；标签页指示线使用 Primary。
- 状态选择：`RadioButton` + 共享 `ModeChip`，使用 `GroupName="Mode"`，支持 Tab、方向键、hover、键盘焦点边框和选中 Check 图标。
- 服务器操作：连接为主操作（填充 Primary），连接后断开为文本操作，离开房间为次级文本操作。
- 主内容空态增加最小高度，减少大面积无意义空白。
- 输入框显式 `IsReadOnly=False`、`IsTabStop=True`、`InputMethod.IsInputMethodEnabled=True`；服务器地址连接态按原需求只读。\n- 最终浅色/深色均在 125% 下实际启动检查；深色主界面和浅色已连接状态见 `tmp/round2-final2.png`、`tmp/round2-light2.png`。
## 输入与交互故障修复

- `InputBox` 不再依赖 MaterialDesignFilledTextBox 的浮动提示/固定高度布局，改为共享轻量模板：有背景、焦点底线、hover、错误状态和稳定的 `PART_ContentHost`。
- 服务器地址框、房间号、昵称、应用规则、设置页、起止时间和子任务统一使用该模板。
- 待办首行使用外置字段标签，避免输入文字被浮动提示裁切。
- 状态 chip 切换增加缩放、透明度和 Check 淡入动画。
- 连接按钮使用独立模板，hover 仅改变透明度，不会丢失填充背景。
- 标签页关闭库 ripple，避免波纹溢出标签边界。