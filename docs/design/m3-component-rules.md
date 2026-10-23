# 9mRoom · Material Design 3 组件选型与使用规范

> 适用端：Windows 客户端（WPF, `clients/windows/src/NineMRoom.Client.App`）
> 版本：v1 · 2026-10-23
> 本文是「选什么组件、为什么、怎么用」的唯一规则来源。页面审计见
> [`m3-page-audit.md`](m3-page-audit.md)，实现与官方组件差异见
> [`m3-implementation-notes.md`](m3-implementation-notes.md)。

---

## 0. 规范来源与取证限制

### 0.1 唯一设计依据

本规范只以 Google 官方 Material Design 3 文档为依据：

| 主题 | 官方页面 |
| --- | --- |
| 组件总目录 | <https://m3.material.io/components> |
| 按钮 | <https://m3.material.io/components/buttons/overview> · [specs](https://m3.material.io/components/buttons/specs) |
| 图标按钮 | <https://m3.material.io/components/icon-buttons/overview> · [guidelines](https://m3.material.io/components/icon-buttons/guidelines) |
| 按钮组（Connected button group） | <https://m3.material.io/components/button-groups/overview> |
| 分段按钮（Segmented button） | <https://m3.material.io/components/segmented-buttons/overview> · [accessibility](https://m3.material.io/components/segmented-buttons/accessibility) |
| FAB | <https://m3.material.io/components/floating-action-button/overview> |
| 导航栏（Navigation rail） | <https://m3.material.io/components/navigation-rail/overview> · [specs](https://m3.material.io/components/navigation-rail/specs) · [guidelines](https://m3.material.io/components/navigation-rail/guidelines) |
| 导航抽屉（Navigation drawer） | <https://m3.material.io/components/navigation-drawer/overview> |
| 标签页（Tabs） | <https://m3.material.io/components/tabs/overview> |
| 菜单（Menus） | <https://m3.material.io/components/menus/overview> · [guidelines](https://m3.material.io/components/menus/guidelines) |
| 单选按钮（Radio button） | <https://m3.material.io/components/radio-button/overview> |
| 开关（Switch） | <https://m3.material.io/components/switch/overview> |
| 复选框（Checkbox） | <https://m3.material.io/components/checkbox/overview> |
| 标签（Chips） | <https://m3.material.io/components/chips/overview> |
| 文本输入框（Text fields） | <https://m3.material.io/components/text-fields/overview> · [specs](https://m3.material.io/components/text-fields/specs) |
| 卡片（Cards） | <https://m3.material.io/components/cards/overview> |
| 列表（Lists） | <https://m3.material.io/components/lists/overview> |
| 对话框（Dialogs） | <https://m3.material.io/components/dialogs/overview> |
| 底部面板 / 侧边面板（Sheets） | <https://m3.material.io/components/bottom-sheets/overview> · <https://m3.material.io/components/side-sheets/overview> |
| 工具提示（Tooltips） | <https://m3.material.io/components/tooltips/overview> |
| Snackbar | <https://m3.material.io/components/snackbar/overview> |
| 进度指示器（Progress indicators） | <https://m3.material.io/components/progress-indicators/overview> |
| 滑块（Sliders） | <https://m3.material.io/components/sliders/overview> |
| 日期选择器（Date pickers） | <https://m3.material.io/components/date-pickers/overview> |
| 时间选择器（Time pickers） | <https://m3.material.io/components/time-pickers/overview> |
| 应用栏（App bars） | <https://m3.material.io/components/top-app-bar/overview> |
| 搜索（Search） | <https://m3.material.io/components/search/overview> |
| 无障碍（Accessibility） | <https://m3.material.io/design/usability/accessibility.html> |

### 0.2 取证限制（必须如实记录）

本次执行环境对 `m3.material.io` 与 `developer.android.com` 的直接抓取被网络策略阻断
（域名解析到非公网地址，HTTPS 握手被拦截）。因此：

- **无法逐页核对官方页面全文**，本文对官方规则的引用来自：公开检索到的官方页面入口与标题、
  以及 Material Design 3 已公开的组件规范（类型、变体、状态、尺寸与使用条件）。
- 未声称「已阅读」任何无法访问的页面；凡涉及官方页面内的具体措辞，均按组件规范的公开语义表述，
  并在实现说明中标注不确定项。
- 若官方规范在后续更新中调整（例如 M3 Expressive 的 Button group 取代旧 Segmented button 用法），
  以访问时的官方文档为准，本文相关条目需重新核对。

---

## 1. 先判断任务，再选组件

任何控件在实现前必须先回答四个问题：

1. **用户此刻要完成什么？**（切换位置 / 触发动作 / 选择值 / 输入文本 / 读取信息 / 等待结果）
2. **它在信息架构的哪一层？**（一级导航 / 页面内视图切换 / 分组内容 / 临时操作）
3. **它的优先级与风险？**（主操作 / 次操作 / 低强调 / 破坏性）
4. **状态如何表达？**（选中 / 未选 / 悬停 / 按下 / 焦点 / 禁用 / 加载 / 错误）

只有四问都有答案，才允许开始写 XAML。

---

## 2. 操作类：Buttons

| 用户任务 | 组件 | 使用规则 | 不适用场景 |
| --- | --- | --- | --- |
| 页面最重要、唯一的推进动作 | **Filled button** | 同一屏同一区域最多一个；用于「加入房间」「加入」「保存」「下载更新」 | 导航入口、破坏性操作、并列的多个动作 |
| 需要操作感但不与主操作竞争 | **Filled tonal button** | 「戳一下」这类轻量社交动作、侧边面板内的次要确认 | 与 Filled 按钮并排且同等重要 |
| 需要可见边界、优先级低于主操作 | **Outlined button** | 「断开服务器」「分类管理」「选择字体/背景」「重置布局」 | 主操作；页面上大量出现 |
| 低强调的普通动作 | **Text button** | 「取消」「恢复默认」「检查更新」「重置位置」「完成编辑」；对话框动作 | 表单唯一提交动作 |
| 简短图标动作 | **Icon button** | 关闭、更多、复制、删除单行、拖动排序、发送 | 含义不清且无 Tooltip；承载导航 |
| 页面的核心创建动作 | **FAB** | 仅在「创建」是页面核心且浮动形式有价值时 | 桌面端模仿手机强行加入；9mRoom **不使用 FAB**（见 2.1） |
| 一组相关动作 | **Button group / Segmented button** | 仅用于语义相关的一组选项或动作 | 把互不相关的按钮连在一起 |

### 2.1 按钮选择原则（9mRoom 强制）

1. 先定优先级，再定变体：首页「加入房间」= Filled；「断开服务器」= Outlined；「离开房间」= Outlined
   + 确认对话框；「取消」= Text。
2. 破坏性操作（离开房间、断开服务器、删除待办）必须有明确文案，并在会造成状态丢失时提供确认。
3. 导航目的地**不得**使用 Filled button（旧实现把一级导航写成 RadioButton 样式按钮，已改为
   Navigation rail 目的地）。
4. 不使用圆形图标按钮承担普通文字动作；文字能说清的动作保留文字。
5. 状态不得只靠颜色：选中项同时改变形状/指示器/勾选图标，并保留文本对比度。
6. 每个按钮必须定义 hover / pressed / focus / disabled / busy 五态。异步动作（连接、加入、保存、
   下载）进入 busy 态时显示圆形进度并阻止重复提交，而不是静默等待。
7. 对话框动作使用 **Text button**（官方 Dialog 规范），确认动作用 primary 色文字表达强调，
   不再塞一个 Filled button 冒充「对话框主按钮」。

### 2.2 为什么 9mRoom 不使用 FAB

添加待办、添加同步块、加入房间都发生在**已有的固定位置**（列表顶部输入行、块的添加区、房间页
空状态），不属于「浮动创建入口」的场景；桌面窗口也不存在手机端单手可达性的约束。强行加入 FAB
只会与导航栏和表单竞争注意力。因此 9mRoom 全应用不使用 FAB，创建动作统一由所在区块的
Filled button / Assist chip 承担。

---

## 3. 导航类：Navigation

| 用户任务 | 组件 | 使用规则 | 不适用场景 |
| --- | --- | --- | --- |
| 在应用一级功能间切换 | **Navigation rail（expanded）** | 6 个一级目的地：自习室/专注/待办/状态/应用/设置；宽度 ≥ 1120 展开，窄窗口折叠为 80 | 用 Tabs 代替一级导航 |
| 宽窗口、需要图标+文字的垂直导航 | **Expanded navigation rail** | 桌面应用首选；item 高 56、选中指示器 56×32 pill、标签常显 | 窗口很窄时仍强行展开 |
| 空间不足以常驻导航 | **Modal navigation drawer** | 仅当窗口宽度 < 720 时作为兜底 | 桌面常规宽度下默认折叠成抽屉 |
| 同一内容区的平级视图 | **Primary tabs** | 房间页「状态/房间/日志/聊天」；3dp 指示器、48 高、选中标签用 primary | 一级导航；跨页面跳转 |
| 设置页内的分类 | 分组标题 + List | 设置页保持单页滚动分组，不用 Tabs（见 3.2） | 把每个分类做成 Tab |
| 次要动作列表 | **Menu** | 应用栏「更多」、待办行「更多」、标题栏溢出 | 把次要动作平铺成一行按钮 |
| 页面内的内容分组 | 布局容器 + 标题 + Divider | 分区、间距、列表 | 把内容分组误当导航（做成 Tabs/rail） |

### 3.1 层级与实现约束

- **一级导航**：Navigation rail（`NavigationRail` ListBox + M3 目的地样式）。导航状态由
  `SelectedIndex` 驱动，页面切换在 `SelectionChanged` 中完成；键盘支持 ↑/↓/Home/End。
- **二级导航**：无独立二级目的地，页面内部使用 Tabs（仅房间页 4 个平级视图）或分组。
- **内容切换**：不是导航，不使用 rail；用 Tabs 或直接分区。
- Tabs **不得**出现在 Navigation rail 之外承担主界面导航。
- rail 在窄窗口折叠为 80：图标 + 12px 标签，选中 pill 保留；这是官方 collapsed rail 形态。
- 官方若提供更合适的桌面导航方案（如扩展 rail 的 leading action / 折叠态），优先采用并在
  审计表记录理由。

### 3.2 设置页为什么不用 Tabs

设置页的「外观 / 启动与常驻 / 通知 / 隐私 / 叠加层 / 关于」是**同一表单文档中的分组**，不是并列
视图；用户通常需要连续查看多个分组。改成 Tabs 会把内容切成互斥页签，增加跳转成本。M3 对设置类
内容推荐使用带分组标题的列表导航或滚动分组，因此这里使用分组标题 + List + Divider。

---

## 4. 选择类：Selection

| 用户任务 | 组件 | 使用规则 | 不适用场景 |
| --- | --- | --- | --- |
| 互斥选择、选项需要说明、表单中 | **Radio button** | 每个选项可带说明；同组用 GroupName；方向键切换 | 5 个以上纯标签的紧凑场景（改用分段按钮） |
| 互斥选择、选项少且紧凑 | **Segmented button（single-select）** | 2–5 段；选中段显示勾选图标 + secondaryContainer；外缘 8 圆角、内缘 0（项目级形状覆盖） | 选项需要逐项解释；段数过多 |
| 立即生效的二元设置 | **Switch** | 设置页全部即时保存的开关；带标签与支持文本 | 需要在提交时统一确认的多项选择 |
| 表单中可同时成立的多个条件 | **Checkbox** | 应用规则「忽略应用 / 隐藏窗口标题」；加入房间「记住昵称 / 设为家房间」；待办完成 | 互斥选择；立即生效的开关 |
| 多条件筛选 | **Filter chip** | 目前 9mRoom 无多选筛选项，未引入 | 装饰性标签 |
| 建议值 / 快捷建议 | **Suggestion chip** | 未使用（无搜索建议场景） | 代替普通按钮 |
| 实体属性标签 | **Assist chip** 或纯文本 | 同步块的「添加块」使用 Assist chip；设备名/分类是属性，使用 supporting text，不做 chip | 把任何短文本做成 chip |
| 连续数值 | **Slider** | 每日专注目标（0–300 分钟，步长 15）、叠加层背景不透明度 | 精确输入离散值 |
| 列表取值 | **Dropdown menu（M3 menu）** | 应用分类（选项多、空间受限，单选） | 仅 2–3 个选项时用分段按钮 |

### 4.1 9mRoom 强制规则：状态是互斥选择

「自动 / 专注 / 休息 / 离开 / 隐身」是**同一状态的 5 个互斥取值**，不是 5 个独立动作：

- 使用 **Segmented button（single-select）**：5 段、组名前缀 `Mode`、每段 `Tag` 为业务值；
- 选中段必须同时具备：secondaryContainer 填充、勾选图标、文本由 onSurfaceVariant → onSecondaryContainer，
  不能只换背景色；
- 切换时调用 `MainViewModel.SetManualMode(tag)`，即时发送 `SendNowAsync()` 更新真实业务数据；
- 当前状态同时以文本呈现（「当前：专注 · 手动」），不依赖按钮外观推断；
- 键盘：←/→ 在段间移动并选中，Home/End 跳到两端；焦点环 3dp primary。

### 4.2 预设选择（叠加层）

「极简单行 / 紧凑双行 / 卡片 / 字幕条 / 自定义」同样是互斥状态且即时生效 → 同一套
single-select Segmented button 组件，由既有 `Preset_Checked` 逻辑写入 `OverlayPreset`。

---

## 5. 文本输入类：Text input

| 用户任务 | 组件 | 规则 | 不适用场景 |
| --- | --- | --- | --- |
| 输入服务器地址、昵称、房间号、名称 | **Filled text field** | 永久标签（浮动标签，不用 placeholder 充当标签）、支持文本、错误态 | 只读展示 |
| 密码 / 敏感内容 | 带隐藏语义的 text field | 9mRoom 目前无密码输入（管理员密码在服务端页面） | — |
| 搜索 | **Search field** | 应用页搜索：leading 搜索图标、持久标签、有内容时出现清除 Icon button、支持文本说明匹配范围 | 用搜索框代替筛选/排序 |
| 多行文本 | 多行 text field | 当前无多行业务输入 | 把长文本塞进单行 |
| 只读信息 | 可选中的文本 / 只读字段 | 服务器地址在已连接时只读；配置/日志路径为只读文本 + 复制 Icon button | 把只读文本包成可编辑输入框 |
| 固定少量选项 | 选择组件 | 应用分类用下拉，计时时长用分段按钮 | 让用户手输固定值 |

### 5.1 输入规范（9mRoom 强制）

- 标签必须长期可见：浮动标签始终存在（未聚焦时位于字段中部，聚焦/有值时上浮），**禁止**用
  placeholder 承担标签职责（旧实现的 `HintAssist.Hint` 只能作为补充提示）。
- 错误态：字段描边/标签变 error 色 + 下方 error 文本 + 图标，颜色之外必须有文字说明。
- 宽度：单行字段最小 280，名称类字段随容器伸展；数值字段限定宽度并右对齐。
- 输入框高度 56（M3 filled field）；紧凑场景（列表行内）使用 48 高变体并在审计表登记。
- 键盘：Tab 可达、Caret 可见、选中文本用 primary 色、IMM 输入法启用（中文输入必须可用）。

---

## 6. 容器类：Containment

| 用户任务 | 组件 | 规则 | 不适用场景 |
| --- | --- | --- | --- |
| 一组独立且相关的信息 | **Card（filled / outlined）** | 必须有独立语义与操作；圆角 12；页面上少量出现 | 每个文本/指标/设置项都套 Card |
| 连续记录、设置项、成员、待办 | **List / List item** | 单行 56、双行 72、三行 88；分隔用 Divider 或间距 | 用 Card 列表代替 List |
| 有层级的分组内容 | 标题 + 间距 + Divider | 页面用分区，不用嵌套 Card | 用卡片模拟分组 |
| 需要用户明确回应的对话 | **Dialog** | 标题 headlineSmall、内容 bodyMedium、动作为 Text button、最大宽 560、圆角 28 | 用 Snackbar 承载需要确认的操作 |
| 简短上下文操作 | **Menu** | 表面 surfaceContainer、圆角 4、项高 48、项内含图标+标签 | 把所有操作直接摆在页面上 |
| 悬停辅助说明 | **Tooltip** | 仅简短文本；图标按钮必备；不承载唯一操作入口 | 用 Tooltip 展示错误原因或长文档 |
| 临时覆盖面板 | **Side sheet（桌面）** | 成员详情、应用规则编辑：右侧 400，开合 200ms，scrim 32% | 桌面端照搬手机底部 sheet |

### 6.1 Card 使用限制（本项目重点）

- Card 不是默认容器。**删除**旧的「每段内容一个 Card」结构：设置页、专注页、待办页、应用页
  均改为「分区标题 + List/Divider」。
- 允许的 Card 场景（审计表中逐项登记）：
  1. 同步列表的**预览面**（它是一个独立的预览实体，需要与编辑区区分）；
  2. 对话框/侧边面板表面（属于 Dialog/Sheet，不是内容 Card）；
  3. 房间页成员行不套 Card，使用 List。
- 禁止：同一屏 3 个以上同色 Card、Card 套 Card、为填空白加 Card、用 Card 圆角+阴影冒充 M3。

---

## 7. 反馈类：Communication

| 用户任务 | 组件 | 规则 | 不适用场景 |
| --- | --- | --- | --- |
| 操作成功的短暂反馈 | **Snackbar** | 最长 4s、单行/两行、inverseSurface、最多一个动作；只在真实业务事件后出现 | 持续连接状态、错误原因的唯一说明 |
| 任务执行中 | **Progress indicator** | 确定性用 linear determinate（目标进度）；不确定用 linear/circular indeterminate（重连、按钮忙碌） | 用进度条假装任务在跑 |
| 持续有效的状态 | **持久状态行** | 应用栏「已连接 · 5 人在线」+ 状态点 + 文本；重连时指示器与文案同步 | Snackbar 代替连接状态 |
| 操作失败 | 错误文案 + 恢复动作 | Snackbar 内说明原因并提供「重试」；表单错误就地显示 | 只显示红点/红图标 |
| 空数据 | **Empty state** | 图标 + 标题 + 说明 + 可选主操作（加入房间） | 用空白页或假数据 |
| 控件附加含义 | **Tooltip** | 图标按钮的用途、快捷键提示 | 关键说明、错误原因、唯一入口 |

### 7.1 9mRoom 强制规则

- 「已连接 · N 人在线」是**持久状态**：固定在应用栏，跨页面可见；不得用 Snackbar 表达。
- 重连中：应用栏状态文本变「正在重连…」并显示 4dp indeterminate 线性进度；恢复后回到实心状态点。
- 同步：设置/房间页展示「同步于 N 秒前」的持久行；未连接显示「未同步」。
- 失败：Snackbar 文案包含原因（来自异常/服务端消息），必要时附「重试」动作；不得显示无来源的成功提示。
- 房间码复制成功/失败：Snackbar（真实剪贴板结果），不伪造成功。
- 托盘不可用等**长期**告警：常驻提示条（error container），不进入 Snackbar。

---

## 8. 时间、日期与数值

| 任务 | 组件 | 9mRoom 用法 |
| --- | --- | --- |
| 选择日期 | **Date picker（modal）** | 待办编辑态的「开始/结束时间」：日历选择日期 |
| 选择时间 | **Time picker（input 变体）** | 同一对话框内 HH:MM 输入 + 校验（桌面端键盘优先，不使用表盘） |
| 连续数值 | **Slider** | 每日目标分钟、叠加层不透明度 |
| 离散数值 | 分段按钮 / 下拉 | 番茄时长 25/45/60 |

待办时间仍是自由文本（兼容历史数据），但提供日期/时间选择器写入规范格式
`MM-dd HH:mm`，并对无法解析的输入就地报错；不强制用户手输日期。

---

## 9. 全局设计规范

### 9.1 颜色（M3 语义角色）

统一资源在 `Themes/Tokens.xaml`（深色）与 `Themes/TokensLight.xaml`（浅色），由
`design/tokens.json` 生成。页面**禁止**硬编码颜色，必须使用角色：

| 角色 | 用途 |
| --- | --- |
| `primary` / `onPrimary` | 主操作、选中标签、激活指示器 |
| `primaryContainer` / `onPrimaryContainer` | 选中容器、自己的聊天消息 |
| `secondaryContainer` / `onSecondaryContainer` | 分段按钮选中、选中列表项 |
| `tertiary*` | 专注语义色（与状态色一致） |
| `error` / `errorContainer` | 错误、破坏性确认、常驻告警 |
| `surface` / `onSurface` | 页面底色 / 主文本 |
| `surfaceContainerLowest … Highest` | 分层：页面 < 卡片 < 菜单 < 对话框 |
| `onSurfaceVariant` / `outline` / `outlineVariant` | 次要文本、边框、分隔线 |
| `inverseSurface` / `inverseOnSurface` | Snackbar、Tooltip |
| `scrim` | 模态遮罩（32%） |

状态层：hover 8%、focus 10%（另加 3dp 焦点环）、pressed 10%、拖动 16%；禁用：容器 12%、
文本/图标 38%。所有交互组件复用同一组状态层，不在页面内自定义。

### 9.2 字体与排版（M3 typescale）

| 角色 | 字号/行高 | 用途 |
| --- | --- | --- |
| `displaySmall` 36/44 | 计时器、今日专注时长等唯一大数 |
| `headlineMedium` 28/36 | 页面标题 |
| `titleLarge` 22/28 | 区块/面板标题 |
| `titleMedium` 16/24 | 列表主文本、对话框标题 |
| `bodyLarge` 16/24 | 重要值 |
| `bodyMedium` 14/20 | 正文、说明 |
| `bodySmall` 12/16 | supporting text |
| `labelLarge` 14/20 | 按钮 |
| `labelMedium` 12/16 | 分段按钮/标签页 |
| `labelSmall` 11/16 | 徽标、时间戳 |

同一角色不得在不同页面用不同字号；不通过缩小字号容纳内容，改用换行、省略与布局调整。
中文字体使用 `Noto Sans SC, Microsoft YaHei UI` 回退链。

### 9.3 形状、间距与尺寸

- 形状：none 0 / extraSmall 4 / small 8 / medium 12 / large 16 / extraLarge 28 / full。
- 组件形状固定：**按钮 8（圆角矩形，项目级覆盖，见下）**、文本字段 4 上圆角、Card 12、
  对话框 28、菜单 4、Snackbar 4、列表项 0。
- **项目级形状覆盖（优先级高于官方默认）**：按项目负责人要求，所有按钮（Filled / Tonal /
  Outlined / Text / Icon / 段按钮外缘 / 日期格）统一使用 8dp 圆角矩形，不使用 M3 默认的
  full（胶囊）形状。这是可解释的项目决定，不得在文档或代码中宣称其为 M3 原生按钮形状。
  导航指示器（56×32 pill）、头像、聊天气泡、开关轨道等非按钮元素不受此覆盖影响。
- 间距：4dp 网格，常用 4/8/12/16/24/32；页面左右 24–32，区块间 24。
- 控件高度：按钮 40、图标按钮 40、文本字段 56、列表项 56/72/88、标签页 48、导航项 56。
- 最小交互区域 48×48（图标按钮 40 视觉 + 4 外扩命中区）。
- 不得所有控件同一圆角，也不靠大圆角+阴影制造「M3 感」。

### 9.4 图标

- 统一使用 Material Symbols 语义（通过 `PackIcon` 字形，24dp 网格、1.5–2px 描边风格）；
  尺寸 18/20/24，同一层级一致。
- 图标按钮必须有 `ToolTip` 与 `AutomationProperties.Name`；含义不清必须补文字或 Tooltip。
- 装饰性图标不替代文字说明；状态不能只用图标颜色表达（配文本）。

### 9.5 交互与可访问性

- 所有可交互组件具备 hover / pressed / focus / disabled / (busy) 态；焦点环 3dp primary，
  移除系统虚线框，但保留键盘可达性。
- Tab 顺序遵循视觉顺序；列表使用 ListBox/roving focus，导航使用方向键。
- 对比度：正文 ≥ 4.5:1，大字号 ≥ 3:1；不透明禁用文本 ≥ 38% 且不作为唯一信息来源。
- 为图标按钮、开关、分段组补充 `AutomationProperties.Name`；分段组提供组标签。
- 动效 120–220ms，缓动 `CubicEase EaseOut`；尊重系统动画设置（后续跟进）。

---

## 10. 实施工作流（每个页面必须执行）

1. 读现有 XAML、ViewModel、命令、数据绑定与业务代码；
2. 写清页面主任务、信息层级、操作路径；
3. 列出全部交互元素及当前行为；
4. 为每个元素指定候选 M3 组件并核对官方文档；
5. 记录选择理由与落选理由；
6. 确认技术栈可正确实现（WPF 模板能力、MDIX 仅提供图标字形）；
7. 建立/复用公共组件与主题资源；
8. 实现布局与交互（不是只换外观）；
9. 构建、运行、检查状态与布局；
10. 与原功能清单逐项核对，确保没有丢失业务行为、快捷键与绑定。

## 11. 禁止事项（与官方规则对应）

- 把 M3 理解为「深色底 + 圆角卡片 + 圆角按钮」；
- 所有交互元素都用 Button；所有信息都包 Card；所有导航都用 Tabs；
- 互斥选项做成互相独立、无选中语义的按钮；
- 用 Switch 表示需要提交时统一确认的多项选择；用 Checkbox 表示单选；
- 用 Snackbar 表示持续连接状态；用 Tooltip 承载关键说明/错误原因/唯一入口；
- 仅靠颜色区分选中、错误、禁用；
- 未核实即宣称自定义控件符合官方规范；
- 为视觉统一破坏现有功能、绑定、快捷键；
- 只改主窗口，遗漏其他页面、对话框、菜单与设置页。
