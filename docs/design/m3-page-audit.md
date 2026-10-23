# 9mRoom Windows 端 · 页面组件审计与选型记录

> 审计对象：`clients/windows/src/NineMRoom.Client.App`（WPF, .NET 8）
> 规则依据：[`m3-component-rules.md`](m3-component-rules.md) · Google Material Design 3
> 审计时间：2026-10-23 · 基线提交：`356a630`

## 0. 审计范围与方法

1. 逐文件阅读 16 个 XAML 页面/窗口与 9 个 ViewModel，确认每个控件的真实行为（命令、绑定、
   快捷键、持久化路径）；
2. 记录「现状问题」，按下表规则选型，并写明落选方案；
3. 不改变任何业务行为；仅新增必要的 UI 状态属性（如计时时长、编辑态、常驻提示）。

统计：XAML 文件 16 个、窗口 5 个、UserControl 8 个、对话框窗口 3 个、Popup 5 处、
`RadioButton` 26 个、`Button` 61 个、`TextBox` 21 个、`CheckBox` 18 个、Card 风格 Border 24 个。

## 0.1 实施结果摘要（2026-10-23 完成）

本文第 2–14 节是**改造前的选型记录**；实际落地结果与差异以
[`m3-implementation-notes.md`](m3-implementation-notes.md) 为准。落地状态：

| 页面 / 区域 | 结果 |
| --- | --- |
| 主窗口 Shell | ✅ 自绘 top app bar（持久连接状态 + 重连进度 + 溢出菜单）、expanded/collapsed navigation rail、primary tabs（3dp 指示器 + 位移动画）、常驻告警条、Snackbar、确认对话框 |
| 自习室 · 状态 | ✅ M3 filled 服务器地址字段、Filled/Outlined 按钮、Segmented 状态选择、元数据 List |
| 自习室 · 房间 | ✅ List 成员、复制房间码 Icon button、Side sheet 成员详情 |
| 自习室 · 日志 / 聊天 | ✅ List 日志、左右对齐消息气泡、Filled 输入 + 发送 Icon button |
| 专注 | ✅ 进度条、Segmented 时长、Switch、Slider、List 历史 |
| 待办 | ✅ 展示态/编辑态、overflow Menu、Checkbox、日期时间选择器、空状态 |
| 状态（同步列表） | ✅ Assist chip 添加块、List 行、预览面、拖拽 |
| 应用 | ✅ Search field、Segmented 排序、List 行、空状态 |
| 应用规则 | ✅ Side sheet、下拉分类、TextField、Checkbox |
| 设置 | ✅ 分组 + List、全部 Switch、Segmented 预设、只读路径 + 复制、Filled/Text 动作 |
| 加入房间 / 确认 / 分类 / 背景 / 字体 | ✅ Basic dialog 面 + Text button 动作 + M3 输入与选择控件 |
| 颜色 / 字体拾取 | ✅ M3 字段 + Icon button + 弹出面 |
| 日期 / 时间 | ✅ 自绘 M3 日期网格 + 24 小时制时间输入 |
| 叠加层 | ✅ 保留用户主题，仅连接状态色与字体回退 token（有意不套用表面/形状规范） |
| 托盘菜单 | ⚠️ WinForms 原生菜单，无法模板化（已记录） |

**已知项目级覆盖**：所有按钮使用 8dp 圆角矩形（不使用 M3 full/胶囊），详见实现说明 5.1。

## 1. 审计结论摘要

| 问题 | 现状 | 影响 | 处理 |
| --- | --- | --- | --- |
| 一级导航用 RadioButton 自绘 | `NavItem` 样式 RadioButton ×6 | 无方向键、无导航语义、选中态只有底色 | 改 Navigation rail（ListBox + M3 目的地） |
| 互斥状态用样式相同的 chip 按钮 | `ModeChip` RadioButton ×5（仅有背景变化） | 不满足「互斥选择需明确语义」 | 改 single-select Segmented button（含勾选图标、方向键） |
| 导航与内容切换混用 RadioButton | 房间 4 个 Tab 用 `MaterialDesignTabRadioButton` + 手工指示器动画 | 指示器由 60 行 code-behind 维护，焦点/键盘语义弱 | 改 M3 primary tabs（自定义 TabControl，指示器内建） |
| Card 泛滥 | 每个设置分组/每个待办/每个统计区一个 Card，共 24 个 | 层级扁平、视觉噪音 | 改为「分区标题 + List + Divider」，仅保留 1 个预览面 Card |
| 所有开关都是 CheckBox | 设置页 11 个即时保存的 CheckBox | 违反 Switch/Checkbox 语义区分 | 即时设置改 Switch；提交式多选保留 Checkbox |
| 状态文本被当作临时提示 | 连接状态在状态页 + 侧边栏重复；`NoticeText` 用黄色文本常显 | 连接状态不够持久、提示不统一 | 状态入应用栏；临时反馈改 Snackbar；长期告警改常驻提示条 |
| 输入框用 Hint 当标签 | `InputBox` + `HintAssist.Hint`，高 64，无错误态 | 标签随输入消失、错误无说明 | 自绘 M3 filled text field（浮动标签、支持文本、错误态） |
| 图标按钮不统一 | MDIX IconButton、自绘 24–46px 按钮混用 | 命中区/状态不一致、部分无 Tooltip | 统一 M3 Icon button 40×40 + Tooltip + Automation 名称 |
| 分类选择没有选中态 | 应用规则用一组 `CompactButton`，无法看出当前分类 | 用户无法确认当前值 | 改 M3 下拉（menu）单选 |
| 待办行常驻输入框 | 每行标题/子任务/时间都是 TextBox | 阅读密度差、视觉噪音 | 列表行显示文本，编辑态展开字段 |
| 日期/时间手输 | 「10-22 14:00」自由文本 | 格式错误无法发现 | 增加日期选择器 + 时间输入对话框，保留文本兼容 |
| 应用卡片是自绘 Border 网格 | 190px 卡片，点击打开编辑器 | 键盘不可达、无列表语义 | 改 M3 List（ListBox，方向键、单 Tab 停靠） |

---

## 2. 主窗口 Shell

### 2.1 应用栏 / 标题栏

| 元素 | 现状与问题 | 选定组件 | 理由 | 落选方案 |
| --- | --- | --- | --- | --- |
| 标题栏（48 高，自绘） | 只有 logo + 最小化/最大化/关闭；连接状态在侧边栏 | **Top app bar**（56 高）：leading logo + 标题，trailing 持久连接状态 + 窗口控制 | 官方 app bar 承载页面级上下文与状态；桌面窗口仍保留系统窗口按钮 | 继续用纯标题栏（状态无处安放）；把状态做成 Snackbar（禁止） |
| 最小化/最大化/关闭 | 46×32 自绘按钮，无 Tooltip | **Icon button**（Windows 尺寸保留 46×32，视觉 40×40 命中区），Tooltip + Automation 名称 | 窗口控制是图标动作；保留 Windows 习惯位置 | 改成普通文字按钮（不符合桌面惯例） |
| 「更多」 | 无 | **Icon button + Menu**：隐藏到托盘 / 退出 | 次要且低频的全局动作，适合 menu | 平铺两个按钮（占空间、强调过度） |
| 连接状态 | 侧边栏 + 状态页各一份，7px 圆点 + 文本 | **持久状态**：状态点 + `ConnectionText`；`reconnecting` 时加 4dp indeterminate 线 | 官方要求持续状态常驻可见，不用 Snackbar | Snackbar（短暂、会丢失）；Tooltip（不可作为唯一来源） |
| 版本号 | 侧边栏底部 | bodySmall supporting text（导航栏底部） | 低优先级信息 | 放应用栏（噪音） |

### 2.2 一级导航

| 元素 | 现状与问题 | 选定组件 | 理由 | 落选方案 |
| --- | --- | --- | --- | --- |
| 自习室/专注/待办/状态/应用/设置 | 6 个 `NavItem` RadioButton，高 44，圆角 12，选中仅 `primaryContainer` 底色 | **Expanded navigation rail**：240px，item 56，选中 56×32 pill（secondaryContainer）+ 标签变色 + 图标填充 | 桌面端一级目的地稳定、清晰；官方 expanded rail 即为此场景 | Navigation drawer（隐藏一级导航，桌面无必要）；Tabs（禁止代替主导航）；Bottom nav（桌面不合适） |
| 折叠 | 无 | `<1120px` 折叠为 80px collapsed rail（图标+12px 标签） | 官方 rail 折叠态；窄窗口保持内容宽度 | 固定 240（窄窗口挤压内容）；抽屉（丢上下文） |
| 方向键/焦点 | 无 | ListBox roving focus（↑↓ Home End）+ 3dp 焦点环 | 键盘可达性 | 保留 RadioButton（无方向键语义） |

### 2.3 房间页容器与标签

| 元素 | 现状与问题 | 选定组件 | 理由 | 落选方案 |
| --- | --- | --- | --- | --- |
| 页面标题「自习室」 | `PageTitle` 26px SemiBold | **headlineMedium（28/36）** | M3 页面标题角色 | 保持自定义字号（层级不统一） |
| 「状态/房间/日志/聊天」 | `MaterialDesignTabRadioButton` + 自绘 indicator + 60 行动画代码 | **Primary tabs**（自定义 `M3TabControl`：48 高、3dp 圆角指示器、可选滑动动画） | 同一内容区的平级视图，官方 Tabs 场景 | Segmented button（语义是「选择值」不是「切换视图」）；继续 RadioButton（无 tab 语义） |
| 内容大圆角容器 | 18 圆角 + `BrushGlassSurface` 包住整个 tab 内容 | 移除容器，改用 surface 直接承载 + Divider 分区 | 内容不是独立实体，不需要卡片 | 保留大卡片（视觉噪音） |

### 2.4 服务器连接区

| 元素 | 现状与问题 | 选定组件 | 理由 | 落选方案 |
| --- | --- | --- | --- | --- |
| 服务器地址 | 无标签，`HintAssist.Hint="服务器地址"`，高 64，已连接时只读 | **Filled text field**：标签「服务器地址」常显，支持文本说明「连接后自动同步…」，已连接时只读态 | 官方 text field；标签不能是 placeholder | 纯文本展示（仍需编辑）；Outlined 字段（与整体 filled 体系不一致） |
| 连接服务器 | 填充色自绘按钮，与输入框拼成一体 | **Filled button**「连接服务器」 | 页面主操作 | Filled tonal（次级，不应抢主操作） |
| 断开服务器 | 同一个按钮换文案（同强调级） | **Outlined button**「断开服务器」+ 确认对话框（房间中时） | 官方 Outlined 用于次级、有边界的动作；断开有状态丢失风险 | Filled（过度强调）；Text（风险操作太弱） |
| 离开房间 | 紧贴地址栏的次级文字按钮，无确认 | **Outlined button**「离开房间」+ 确认对话框 | 离开房间中止同步，属中风险动作 | Text button（风险动作弱）；Filled（与连接竞争） |
| 连接中 | 无反馈 | **Filled button busy 态**（圆形进度 + 禁用重复提交） | 官方按钮支持加载/禁用状态 | 只改文案（无进行中反馈） |

### 2.5 成员详情侧边面板

| 元素 | 现状与问题 | 选定组件 | 理由 | 落选方案 |
| --- | --- | --- | --- | --- |
| 右侧 400px 抽屉 | 自绘 Border + 99% 黑遮罩 + 180ms 位移 | **Side sheet**：surfaceContainerLow、leading 16 圆角、scrim 32%、200ms | 桌面端临时补充信息用 side sheet（官方桌面建议） | Bottom sheet（手机形态）；Dialog（信息非阻断） |
| 关闭按钮 | 32px 自绘按钮，无 Tooltip | **Icon button**「关闭」 | 图标动作 | 文字按钮（占位） |
| 「戳一下」 | `PrimaryButton`（填充） | **Filled tonal button** | 轻量社交动作，不与页面主操作竞争 | Filled（过度）；Text（缺少可点击暗示） |
| 成员字段 | 6 组 Label+Body 竖排 | **List（label/value 行 56）** | M3 元数据列表 | Card 分组（噪音） |
| 关闭遮罩 | 点击遮罩关闭（保留） | scrim + 点击关闭 + Esc | 官方模态交互 | — |

### 2.6 加入房间（对话）

| 元素 | 现状与问题 | 选定组件 | 理由 | 落选方案 |
| --- | --- | --- | --- | --- |
| 对话面板 | 460 宽、`#17171A` 硬编码、`RadiusMd` | **Basic dialog**：surfaceContainerHigh、28 圆角、宽 560、标题 headlineSmall | 官方 dialog 规格 | 独立窗口（上下文丢失）；side sheet（阻断性任务更适合 dialog） |
| 房间号/昵称 | 无标签 TextBox | **Filled text field** ×2 + 支持文本 | 输入规则 | — |
| 记住昵称 / 设为家房间 | CheckBox | **Checkbox**（保留） | 会话内多选、点「加入」才提交 → 官方 Checkbox 场景，**不能**改 Switch | Switch（会在提交前生效，语义错误） |
| 取消 / 加入房间 | 自绘次级 + 主按钮 | **Text button ×2**（取消中性、加入 primary 文字） | 官方对话框动作规范 | Filled（对话框内不属于页面主操作） |

### 2.7 应用规则编辑（侧边面板）

| 元素 | 现状与问题 | 选定组件 | 理由 | 落选方案 |
| --- | --- | --- | --- | --- |
| 面板 | 500px 右侧滑入，无标题层级 | **Side sheet（400）**，标题 titleLarge | 上下文编辑 | Dialog（内容过长）；全屏页（过重） |
| 显示名称/文件位置/状态模板 | 无标签 TextBox + Hint | **Filled text field** ×3（文件位置只读？否——可编辑，保留） | 官方输入 | — |
| 应用分类 | 一组无选中态的 `CompactButton` | **Dropdown menu（ComboBox）** 单选，含「自动」 | 选项多、空间受限，官方推荐菜单式单选 | Radio 列表（12+ 项过长）；分段按钮（过多段） |
| 模板占位符帮助 | 「?」按钮 + Popup | **Icon button（Help）+ 弹出说明面** | 图标辅助入口；内容为参考文档，超出 tooltip 容量 | Tooltip（内容过长，禁止承载关键说明） |
| 忽略应用 / 隐藏窗口标题 | CheckBox ×2 | **Checkbox**（保留）：保存时统一提交 | 延迟提交的多选 → Checkbox | Switch（即时生效与「保存/取消」冲突） |
| 取消 / 保存 | 仅 dirty 时出现 | **Text button + Filled button**，常显；保存禁用态表达未修改 | 明确动作可用性 | 隐藏动作（用户不知道如何保存） |

---

## 3. 房间 · 状态标签页

| 元素 | 现状与问题 | 选定组件 | 理由 | 落选方案 |
| --- | --- | --- | --- | --- |
| 未入房空状态 | 圆角图标块 + 两行文字 + 「打开房间」次级按钮 | **Empty state**：图标 + headline + body + Filled button（已连接时） | 官方空状态：说明 + 引导动作 | Card 包裹（无必要） |
| 「我的状态」标题 | 18px SemiBold | **titleLarge（22/28）** | 区块标题角色 | 自定义字号 |
| 当前状态文本 | bodyMedium | **bodyLarge** + 状态色点 + 「当前：专注」 | 状态不能只靠控件外观 | 仅高亮按钮 |
| 自动/专注/休息/离开/隐身 | 5 个 `ModeChip`（RadioButton，统一圆角 16，仅底色变化） | **Segmented button（single-select）** 5 段 + 勾选图标 + 组标签 | 官方 segmented single-select；互斥状态需明确选中语义 | 普通按钮组（无选中语义，禁止）；Radio 竖排（占空间但语义最明确，作为备选） |
| 当前待办/应用/空闲/音乐/同步 | 两列 Label + Body | **List（label/value 行）**，单列 5 行 + Divider | 连续元数据记录 | Card ×5（禁止） |
| 同步时间 | 混在详情中 | 持久状态行「同步于 N 秒前」，重连时配 indeterminate 进度 | 官方进度反馈 | Snackbar |

---

## 4. 房间 · 房间标签页

| 元素 | 现状与问题 | 选定组件 | 理由 | 落选方案 |
| --- | --- | --- | --- | --- |
| 房间名/房间码 | 点击房间名复制（无可见提示，Tooltip 才说明） | **titleLarge + 复制 Icon button（Tooltip「复制房间码」）** + 房间码支持文本 | 动作要有可见入口与语义 | 只有 Tooltip 的隐式点击（禁止） |
| 人数 | 右上 Label | bodyMedium supporting（「5 人」） | 低强调元数据 | 徽标/Chip（非交互） |
| 成员行 | 自绘 Border 卡片，40 头像，右下角圆点 + 时间 | **List item（72 高）**：avatar + 名单（titleMedium）+ 设备/状态/标题（bodySmall）+ 在线点 + 时间；Divider | 官方 list 用于重复条目；成员不是卡片 | Card 列表（禁止）；DataGrid（过度） |
| 设备标签 | 带底色的 5,1 padding 小圆角块 | 纯 supporting text（「Windows · 笔记本」） | 属性标签不是 chip（chip 表示操作/筛选） | Assist chip（语义错误） |
| 离线成员 | 整行 opacity 0.5 | 文本变 onSurfaceVariant + 「离线」文字 + 点变 outline | 不能只用透明度表达状态 | 仅置灰（颜色唯一） |

---

## 5. 房间 · 日志标签页

| 元素 | 现状与问题 | 选定组件 | 理由 |
| --- | --- | --- | --- |
| 日志行 | 62px 时间列 + 文本，无空状态 | **List item（双行 56–64）**：时间 labelSmall（等宽）+ 文本 bodyMedium；空状态 | 连续记录用 List；空状态需说明 |
| 滚动 | ScrollViewer | 保留（M3 无列表虚拟化规范，WPF 用 ScrollViewer） | — |

---

## 6. 房间 · 聊天标签页

| 元素 | 现状与问题 | 选定组件 | 理由 | 落选方案 |
| --- | --- | --- | --- | --- |
| 消息 | 自绘 Border 气泡，自己/他人只换底色 | 消息行：他人 surfaceContainerHigh、自己 primaryContainer；姓名 labelMedium + 时间 labelSmall + 正文 bodyMedium；**不靠颜色**（自己消息右对齐 + 「我」标记） | M3 无聊天组件，用容器+排版组合，颜色+对齐双编码 | Card（每条一个卡片，禁止） |
| 空状态 | 无 | Empty state「还没有消息」 | 官方空状态 | 空白 |
| 输入 | TextBox + 竖分隔线 + 「发送」文字按钮 | **Filled text field（标签「消息」）+ 发送 Icon button（Tooltip）** | 输入用 text field；发送是图标动作 | 文字按钮（可用但占用宽度） |

---

## 7. 专注页

| 元素 | 现状与问题 | 选定组件 | 理由 | 落选方案 |
| --- | --- | --- | --- | --- |
| 今日专注 | Card + 26px 数字 + 原生 ProgressBar + 3 行文字 | 分区 + **displaySmall 数字** + **linear determinate progress（含标签）** + supporting | 官方进度指示器需可读标签；数值是页面焦点 | Card（无独立语义） |
| 番茄计时 | Card + 36px 数字 + 3 个相同按钮 + 3 个时长按钮（无选中态） | **displaySmall 等宽数字**；开始 = Filled、暂停 = Filled tonal、重置 = Text；时长 = **Segmented single-select（25/45/60）** | 动作优先级区分；时长是互斥设置 | 三个同权按钮（层级不清）；Dropdown（3 项无需下拉） |
| 记录专注统计 | CheckBox | **Switch**（即时保存、立即生效） | 官方 Switch 场景 | Checkbox（延迟提交语义不符） |
| 每日目标 | TextBox 数字（LostFocus 保存） | **Slider 0–300 步长 15** + 数值标签 +「0 为关闭」 | 连续数值用 Slider | 文本框（易输错，且无范围提示） |
| 近 7 天 | Card + 7 行 Grid | **List（label/value 行 44）** + Divider | 连续数据 | Card（禁止） |
| 说明文本 | Label | bodySmall supporting | — | — |

---

## 8. 待办页

| 元素 | 现状与问题 | 选定组件 | 理由 | 落选方案 |
| --- | --- | --- | --- | --- |
| 新建待办 | 无标签 TextBox + 「添加待办」 | **Filled text field（标签「新待办」）+ Filled button「添加」**；回车保留 | 主创建动作 | FAB（非浮动场景） |
| 空状态 | 一行 Label | **Empty state**（标题 + 说明） | 官方空状态 | 单行提示 |
| 待办行 | 每行一个 Card，标题/子任务/时间全部常驻 TextBox | **List item（可变高）**：Checkbox + 标题文本 + 子任务进度 supporting + trailing overflow Icon button | 阅读优先；编辑通过菜单进入编辑态（真实数据仍即时保存） | 常驻输入框（视觉噪音、与「只读信息不用输入框」冲突） |
| 完成状态 | Checkbox 仅打勾 | Checkbox + 文本删除线 + 颜色降级 | 状态不能只靠控件 | 仅勾选 |
| 置顶/删除 | 两个平铺按钮（「置顶」文字 + 「×」） | **Menu**（置顶/取消置顶、编辑、删除） | 官方 menu 场景：次要、上下文动作 | 平铺按钮（占空间、删除无确认层级） |
| 子任务 | 常驻 TextBox + 「×」 | 编辑态内 TextField + 删除 Icon button；显示态纯文本 | — | — |
| 开始/结束时间 | 自由文本 + Tooltip 说明格式 | 编辑态：**Filled field（标签「开始时间」）+ 日期选择器/时间输入对话框** + 格式校验错误态 | 官方日期/时间选择器；错误需文字说明 | 手输（易错）；三个下拉（过重） |
| 删除待办 | 无确认 | **Menu + 确认对话框**（「删除待办」error 色文字） | 破坏性操作需确认 | 直接删除 |

---

## 9. 状态页（同步列表）

| 元素 | 现状与问题 | 选定组件 | 理由 | 落选方案 |
| --- | --- | --- | --- | --- |
| 预览 | Card + 两行文本 | **Outlined preview surface**（独立预览实体） | 唯一保留的 Card 场景 | 普通文本（无法区分预览/编辑） |
| 块行 | Card 列表，行内 Checkbox + 标签 + TextBox + × | **List（64 高）**：拖动图标按钮 + Checkbox（启用）+ 标题 + TextField（模板）+ 删除 Icon button | 连续可编辑记录 | Card 列表（禁止） |
| 拖动手柄 | 自绘 Border + 「≡」字符 | **Icon button（DragIndicator）+ Tooltip「拖动排序」** | 图标语义 + 48 命中区 | 字符文本（无 a11y） |
| 添加块 | 5 个同权按钮 | **Assist chip ×5**（＋应用/音乐/计时/待办/自定义） | 官方 Assist chip 表示可执行动作；紧凑一组动作 | Filled 按钮（过度强调）；Filter chip（语义错误） |
| 恢复默认布局 | 普通按钮 | **Text button** | 低频、可回退动作 | Filled（过度） |
| 占位符说明 | 「?」+ Popup | **Icon button（Help）+ M3 弹出面** | 内容超出 tooltip 容量 | Tooltip（禁止承载关键说明） |
| 校验提示 | 黄色文本 | **supporting/error text（就近显示）** | 错误需说明且就近 | Snackbar（会消失） |

---

## 10. 应用页

| 元素 | 现状与问题 | 选定组件 | 理由 | 落选方案 |
| --- | --- | --- | --- | --- |
| 搜索 | 自绘 238×64 容器 + 无标签 TextBox + 覆盖占位 TextBlock + 「×」 | **Search field**：持久标签「搜索应用」、leading 搜索图标、trailing 清除 Icon button、支持文本说明匹配范围 | 官方搜索输入模式 | 普通 text field（缺少搜索语义）；不提供清除（低效） |
| 排序 | 3 个同权按钮（无当前态，仅文字说明） | **Segmented single-select**（最近使用/名称/分类） | 互斥、即时生效 | Menu（多一次点击）；按钮组（无选中语义） |
| 分类管理 | 普通按钮 | **Outlined button** | 次级但有边界 | Filled（与页面主任务竞争） |
| 应用列表 | 190px 自绘 Border 网格，WrapPanel | **List（ListBox，64 高）**：36 图标 + 名称 + 分类 + trailing chevron；选中态 = secondaryContainer | 官方 list；键盘可达、扫描效率高 | Card 网格（禁止/噪音）；TreeView（无层级） |
| 空状态 | 两行 Label | **Empty state**（无匹配 / 尚未探测到两种情况） | 官方空状态 | 单行提示 |

---

## 11. 设置页

| 元素 | 现状与问题 | 选定组件 | 理由 | 落选方案 |
| --- | --- | --- | --- | --- |
| 分组 | 6 个 Card | **分区标题（titleLarge）+ List + Divider** | 设置是分组列表，不是卡片集合 | Card（禁止）；Tabs（切分成本高） |
| 使用浅色主题 | CheckBox | **Switch** + supporting「重启后生效」 | 即时保存的二元设置 | Checkbox（延迟语义） |
| 自动连接/开机自启/关闭到托盘/关闭提示 | CheckBox ×4 | **Switch ×4**（禁用态用于托盘不可用） | 全部即时保存 | Checkbox |
| 通知 ×2 | CheckBox | **Switch ×2** | 即时保存 | Checkbox |
| 共享窗口标题/音乐 | CheckBox | **Switch ×2** | 即时保存 | Checkbox |
| 音乐来源过滤 | TextBox + 提示 | **Filled text field（标签「播放器过滤」）** + supporting | 文本输入 | — |
| 叠加层开关 | CheckBox ×2 | **Switch ×2** | 即时保存 | Checkbox |
| 预设样式 | 5 个 RadioButton（无选中态样式） | **Segmented single-select ×5** | 互斥、即时生效 | Radio 竖排（占空间） |
| 自定义主题摘要 | 两行 label+value + 「…」按钮 | **List 行 + Outlined button** | 打开对话框属于次级动作 | 内联表单（过长） |
| 预览 | 自绘 Border | 预览面（沿用，反映用户自定义配色） | 需要真实反映自定义主题 | 移除（丢失预览） |
| 应用自定义/复制/导出/导入 | 4 个同权按钮 | **Filled（应用）+ Text ×3** | 主次区分 | 全部普通按钮 |
| 重置位置 | 普通按钮 | **Text button** | 低频可回退 | — |
| 版本/路径 | Body 文本 | **List 行 + 只读 field（可选中复制）+ 复制 Icon button** | 只读信息用只读展示 | 可编辑 TextBox（禁止） |
| 检查更新/下载更新 | 两个普通按钮 | **Text（检查）+ Filled（下载，仅可用时）** | 主次区分 | — |
| 更新状态 | Label | bodySmall supporting；错误时 error 色 + 说明 | 失败需可读说明 | 只变色 |

---

## 12. 对话框与拾取器

| 元素 | 现状与问题 | 选定组件 | 理由 |
| --- | --- | --- | --- |
| FontDialog / BorderDialog / CategoryDialog | 独立窗口，`BrushSurface` + 16 圆角 + 22 padding，无对话框标题层级 | **Basic dialog 表面**：surfaceContainerHigh、28 圆角、标题 headlineSmall、bodyMedium 说明、动作 Text button；保留窗口拖动 | 官方 dialog 规格；桌面窗口仍需可移动 |
| BorderDialog 颜色 | 4 个 ColorPicker | 保留（自定义控件）+ M3 字段/图标按钮样式 | 业务需要 |
| BorderDialog 不透明度 | Slider（原生） | **M3 Slider**（4dp 轨道 + 4×44 手柄） | 连续数值 |
| BorderDialog 单行/双行、右下/底部居中 | 4 个 RadioButton 平铺 | **Radio button ×2 组**（布局 / 位置） | 每组互斥、需要文字说明 |
| FontDialog 字体列表 | 原生 ListBox | **M3 List（ListBox）** + 选中态 secondaryContainer | 列表选择 |
| FontDialog 字重/字号 | RadioButton ×3 + TextBox | **Segmented（常规/中等/半粗）** + **TextField（字号）** | 互斥紧凑；数值输入 |
| CategoryDialog 分类行 | 24+130+*+Auto 网格，TextBox ×2 + × | **List item（双行 72）**：拖动手柄 + 名称 field + 模板 field + 删除 Icon button | 连续可编辑记录 |
| CategoryDialog 新增 | 两个无标签 TextBox + 普通按钮 | **Text field ×2（标签）+ Filled button「新增分类」** | 创建动作 |
| ColorPicker / FontPicker | 32px 图标按钮 + 硬编码 popup 背景 | **Icon button + M3 弹出面** | 图标动作 + 菜单面 |

---

## 13. 叠加层（HUD）

叠加层是**用户可自定义主题**的窗口（背景/文字/字体/圆角均可配置），不属于标准 M3 容器：

- 保留用户主题（否则会删掉既有功能），只将连接状态点改为语义状态色
  （connected=`tertiary`、reconnecting=`break`、offline=`outline`），并使用统一 token 字体；
- 连接状态在 HUD 内为持久状态（点 + 头部文本 Tooltip），符合「持续状态不用 Snackbar」；
- 未套用 M3 表面/形状规范的原因记录在实现说明中。

## 14. 托盘菜单

`TrayIcon` 使用 WinForms `ContextMenuStrip`（`Services/TrayIcon.cs`），Windows 原生菜单，
无法套用 WPF 模板。保留原生实现，记录为「官方 M3 Menu 无法覆盖的系统级菜单」。

---

## 15. 业务行为核对清单（重构后逐项验证）

- [ ] 连接/断开服务器、自动连接、家房间校验
- [ ] 加入房间、房间码复制、离开房间、断开确认
- [ ] 模式切换实际发送状态（`SetManualMode` → `SendNowAsync`）
- [ ] 番茄计时 开始/暂停/重置/时长切换 + 全局快捷键 Ctrl+Alt+Shift+T
- [ ] 叠加层开关、点击穿透、位置重置、Ctrl+Alt+H
- [ ] 待办新增/完成/置顶/删除/子任务/时间（持久化）
- [ ] 同步块 启用/模板/排序/添加/删除/恢复默认
- [ ] 应用规则 编辑/保存/取消/图标/分类/忽略/隐藏标题
- [ ] 分类管理 新增/删除/排序
- [ ] 设置 全部开关、主题、过滤、预设、导入导出
- [ ] 聊天发送（回车）、日志、成员详情、戳一下
