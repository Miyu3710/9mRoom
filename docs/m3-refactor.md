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
