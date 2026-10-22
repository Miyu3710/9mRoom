# shared/status-blocks

房间里那行状态文本，由若干「块」拼成。用户界面里叫**同步列表**：
可增删块、拖拽排序、给不同应用配不同模板；Windows 端还能在自定义文本里调用命令。

```
🍚正在被 VS Code 拷打 | 🎶晴天 - 周杰伦 | 📚 看完第三章 | 🔌已开机 3小时50分20秒
```

## 为什么不做成一段字符串

如果客户端直接上报一句渲染好的话，会失去三样东西：

- 「已开机 3 小时」「音乐 2:23/4:48」这类内容需要每秒刷新，但没必要每秒发一条网络消息；
- 应用名、歌名要在对方设备上按本地目录解析，成品字符串会把发送端的写法固化下来；
- 服务端无法做长度与类型校验，异常内容只能整条丢弃。

所以：**上报结构化块 + 模板，由接收端渲染**；只有命令与自由文本的输出才以成品字符串上报。

## 同步列表模型

```jsonc
{
  "layout": "inline",          // inline 单行 | stacked 多行
  "separator": " | ",          // 最长 4 字符，仅 inline 使用
  "blocks": [
    { "id": "b1", "type": "app",    "template": "🍚正在被 {app} 拷打", "data": { "appId": "vscode", "category": "coding", "idleSeconds": 0 } },
    { "id": "b2", "type": "media",  "template": "🎶{title} - {artist} {progress}", "data": { "title": "晴天", "artist": "周杰伦", "isPlaying": true, "positionMs": 143000, "durationMs": 288000, "sampledAt": 1730003580000 } },
    { "id": "b3", "type": "todo",   "template": "📚 {todo}", "data": { "text": "看完第三章" } },
    { "id": "b4", "type": "timer",  "template": "⏱ {remaining}", "data": { "mode": "countdown", "startedAt": 1730000000000, "durationMinutes": 90 } }
  ]
}
```

## 块类型（v1）

| type | 界面名称 | data 字段 | 占位符 | 刷新方式 |
| --- | --- | --- | --- | --- |
| `app` | 应用活动 | `appId`、`category`、`idleSeconds`、可选 `title` | `{app}` `{category}` `{title}` `{idle}` | 前台变化时重发 |
| `media` | 歌曲识别 | `title`、`artist`、`isPlaying`、`positionMs`、`durationMs`、`sampledAt` | `{title}` `{artist}` `{progress}` | 换歌 / 暂停时重发 |
| `todo` | 待办状态 | `text` | `{todo}` | 用户改动时重发 |
| `timer` | 计时器 | `mode`、`startedAt`、`durationMinutes` | `{elapsed}` `{remaining}` | 接收端本地走秒 |
| `uptime` | 开机时长（已废弃，仅兼容接收） | `startedAt` | `{uptime}` | 接收端本地走秒 |
| `text` | 自定义文本 | `text` | `{text}` | 命令刷新或用户编辑时重发 |

`{app}` 由接收端用本地 `app-catalog` 解析成展示名，解析不到就用 `appId` 原文。

## 渲染规则（两端必须逐字一致）

渲染是纯函数：`render(blocks, layout, separator, now, catalog)`。

1. 数据为空或为 `null` 的块不渲染，也不产生分隔符。
2. 未知占位符替换为空字符串；渲染后连续空格合并为一个。
3. 去掉首尾空白。单块上限 80 字符，整行上限 200 字符，超出截断并追加 `…`。
4. `separator` 只出现在两个成功渲染的块之间。
5. `layout = stacked` 时每块一行，忽略 `separator`。
6. 去掉控制字符（U+0000–U+001F）；`stacked` 下保留换行。
7. `{uptime}`：`h > 0` → `{h}小时{m}分{s}秒`；`h = 0` 且 `m > 0` → `{m}分{s}秒`；否则 `{s}秒`。
8. `{elapsed}` / `{remaining}`：`MM:SS`，超过 1 小时用 `H:MM:SS`。
9. `{progress}`：`isPlaying` 且 `durationMs > 0` 时输出 `[位置/总长]`，位置按
   `positionMs + (now - sampledAt)` 外推并截断到总长；否则为空。
10. `{idle}`：`idleSeconds` 满 60 秒输出 `空闲{n}分`（向下取整），否则为空。

## 每个应用单独配模板

```jsonc
{
  "appRules": [
    { "appId": "vscode", "template": "💻在 {app} 里搬砖" },
    { "appId": "chrome", "template": "🌐用 {app} 查资料", "includeTitle": false }
  ],
  "defaultTemplate": "🍚正在被 {app} 拷打",
  "shareWindowTitle": false
}
```

自上而下匹配，未命中用 `defaultTemplate`。**规则只在本机，不上传服务端。**
`shareWindowTitle` 默认关闭（2026-10-20 已确认）；即使开启，也只对显式声明 `includeTitle: true` 的应用附带标题。

## 自定义文本与命令

- Windows 端：字段按 PowerShell 表达式求值，支持 `$()` 子表达式，例如
  `${&'E:\Scripts\uptime.ps1'}`；**表达式与脚本路径只存本机，不上传**，只有输出文本上报。
- 求值间隔 ≥ 30 秒（可配 10–300 秒），单次超时 3 秒，失败沿用上一次成功的结果。
- 输出截断到 80 字符并去除控制字符。
- 表达式以当前用户权限执行，设置页必须提示：**不要在里面写密钥，输出会展示给房间成员。**
- Android 端 v1 只支持手动文本，不做命令求值。

## 编辑器形态

- 每块一行：拖拽手柄、图标、名称、一句说明、设置、删除
- 设置里改模板、数据源选项（如是否带窗口标题、命令刷新间隔）
- 底部输入框添加快捷项：计时器、待办、自定义文本；Windows 下支持 `$()` 语法
- 编辑时实时预览「别人看到的样子」

## 校验与兼容

- 服务端只做校验，不解析模板：类型白名单、`blocks` ≤ 8、`template` ≤ 80、
  `title` ≤ 120、`artist` ≤ 80、`todo` ≤ 100、`text` ≤ 80
- 未知 `type` 的单块丢弃，不影响其它块
- 旧客户端收到不认识的字段忽略，新增字段一律可选
- 接收端必须安全渲染（Compose `Text` / WPF `TextBlock` 默认安全），异常内容不得影响其他成员

## 一致性测试

[`fixtures.json`](fixtures.json) 是两端共用的黄金用例：给定块、`now` 与目录，
渲染结果必须逐字相同。两端各写一个单元测试读取同一份文件，防止规则悄悄漂移。

## 状态

草案 v0.2，按参考实现修订，尚未实现。
