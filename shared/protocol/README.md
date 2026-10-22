# shared/protocol

跨端唯一的契约来源：Windows、Android、server 都从这里取消息定义。

## 为什么单独放

状态字段会同时出现在三处。没有单一来源时，字段名、类型和语义必然漂移，
而这类漂移往往在联调最后一天才暴露。

## 形式（候选，未定）

- 传输：HTTP/JSON 请求 + WebSocket/JSON 文本帧
- 定义：OpenAPI 3.1（HTTP）+ JSON Schema（WebSocket 消息）
- 版本：路径带 `/v1`；消息带 `v` 字段；**新增字段必须可选**，删除需跨版本

## 消息外壳草案

```json
{
  "v": 1,
  "type": "presence.update",
  "id": "c-8f2a1b",
  "ts": 0,
  "payload": {}
}
```

- `id` 由客户端生成，服务端用于幂等去重
- `ts` 由服务端回填，客户端时间不可信
- `payload` 结构按 `type` 定义，未知 `type` 必须被忽略而不是报错

## 状态块（status）

`presence.update` 里，`activity` / `music` 是结构化语义字段，供房间聚合与筛选；
`status.blocks` 是展示层，由接收端渲染。两者刻意并存：字段用来算，块用来显示。

```jsonc
"status": {
  "layout": "inline",
  "separator": " | ",
  "blocks": [
    { "id": "b1", "type": "app", "template": "🍚正在被 {app} 拷打",
      "data": { "appId": "vscode", "category": "coding" } }
  ]
}
```

- 块类型、占位符与渲染规则见 [`shared/status-blocks/`](../status-blocks/README.md)
- 服务端只校验（类型白名单、长度、块数 ≤ 8），不渲染、不存模板
- 未知块类型丢弃单块，不影响整条消息
- 自定义文本块只上报展开后的文本；`${...}` 命令/脚本表达式与路径只在本机执行，不进入协议
- 窗口标题默认不出现；总开关开启后为黑名单模式：除黑名单应用外共享，单个应用还可隐藏
- 聊天只转发纯文本，不写库；新加入者看不到历史

## HTTP 辅助接口

- `GET /healthz`：服务器存活与房间 / 成员计数
- `GET /api/rooms/{code}`：`{ code, exists, kind, online }`，`kind=server` 的房间可设为“家”
- 客户端先连接服务器，加入房间时才建立 WebSocket；加入前通过房间信息判断是否允许设为家

## 消息清单（草案）

| 方向 | 类型 | 说明 |
| --- | --- | --- |
| C → S | `hello` | 连接后声明版本、设备、房间 |
| C → S | `presence.update` | 上报最新状态（全量，不做增量补丁） |
| C → S | `profile.update` | 修改昵称 |
| C → S | `chat.send` | 发送聊天纯文本（≤ 500 字符，服务端限频） |
| C → S | `room.subscribe` | 订阅 / 取消订阅房间 |
| C → S | `ping` | 应用层心跳 |
| S → C | `welcome` | 返回 `serverTime`、用户与房间概况 |
| S → C | `room.snapshot` | 房间全量成员状态（重连后第一帧） |
| S → C | `presence.changed` | 单个成员状态变化 |
| S → C | `member.joined` | 成员加入房间 |
| S → C | `member.left` | 成员离开 / 超时 |
| S → C | `chat.message` | 房间聊天消息（只广播，不持久化） |
| S → C | `error` | 带 `code` 与可读 `message` |

## 约定

- 状态只保留最新值，不设计事件重放
- 服务端是时间与状态有效性的唯一权威
- 状态变化做 1 秒合并窗口，避免频繁切换应用时刷屏
- 聊天只保留在客户端本次连接内；服务端不落库、不回放历史
- 聊天限 500 字符，同一成员 800ms 内最多一条；空消息直接拒绝
- 所有时间字段为 UTC 毫秒时间戳

## 状态

草案。等第一个端到端 spike 跑通后再写正式 schema。
