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
| C → S | `avatar.update` | 更新头像；512×512 JPEG，解码后 ≤128 KiB |
| C → S | `chat.send` | 发送聊天纯文本（≤ 500 字符，服务端限频） |
| C → S | `poke.send` | 戳同房间的在线成员，`targetId` 为成员 ID |
| C → S | `leave` | 显式离开房间，服务端立即移除并广播 `member.left` |
| C → S | `room.subscribe` | 订阅 / 取消订阅房间 |
| C → S | `ping` | 应用层心跳 |
| S → C | `welcome` | 返回 `serverTime`、用户与房间概况 |
| S → C | `room.snapshot` | 房间全量成员状态（重连后第一帧） |
| S → C | `presence.changed` | 单个成员状态变化 |
| S → C | `member.joined` | 成员加入房间 |
| S → C | `member.left` | 成员离开 / 超时 |
| S → C | `member.avatar` | 单个成员头像数据；snapshot 中只带 `hasAvatar` / `avatarHash` |
| S → C | `chat.message` | 房间聊天消息（只广播，不持久化） |
| S → C | `poke.received` | 只发给被戳者；包含 `from` 成员摘要与 `at` |
| S → C | `poke.sent` | 只发给发起者；确认 `targetId` / `targetName` / `at` |
| S → C | `error` | 带 `code` 与可读 `message` |

## 约定

- 状态只保留最新值，不设计事件重放
- 服务端是时间与状态有效性的唯一权威
- 状态变化做 1 秒合并窗口，避免频繁切换应用时刷屏
- 聊天只保留在客户端本次连接内；服务端不落库、不回放历史
- 聊天限 500 字符，同一成员 800ms 内最多一条；空消息直接拒绝
- 戳一下只发给目标成员，不广播给房间；同一成员 2 秒内最多一次
- 戳一下要求目标在同一房间且在线；不存在、离线、戳自己均返回 `error`
- 所有时间字段为 UTC 毫秒时间戳

## 连接恢复

成员视图在原有 `online` 之外增加可选的 `connectionState` 字段，取值为
`"online"` 或 `"reconnecting"`；旧客户端可以忽略。

- WebSocket 异常断开后，成员不立即移除，保留 120 秒恢复窗口：
  `online=false`、`connectionState="reconnecting"`，广播一次 `presence.changed`。
- 同一 `(room, clientId)` 在窗口内重新连接时复用原 `userId`，只广播
  `presence.changed`，不广播 `member.left` / `member.joined`。
- 超过窗口未恢复：服务端移除成员并广播一次 `member.left`。
- 客户端也可以发送 `{ "type": "leave" }` 显式离开：服务端立即移除并广播
  `member.left`，之后同 `clientId` 重连必须按新成员处理。
- `room.snapshot` 包含处于重连窗口中的成员，因此新加入者也能看到“重连中”。

## 头像

客户端本地缓存 512×512 JPEG（目标 ≤96 KiB，服务端硬上限 128 KiB），
连接或重连时自动发送；服务端只在内存保存，不落库。

```jsonc
// C → S；data 为空表示清除头像
{
  "v": 1, "type": "avatar.update", "id": "a1",
  "payload": {
    "mime": "image/jpeg",
    "width": 512,
    "height": 512,
    "data": "<base64>"
  }
}

// S → C；只发给头像变化的房间成员
{
  "v": 1, "type": "member.avatar",
  "payload": {
    "memberId": "u_1a2b3c4d",
    "avatar": {
      "mime": "image/jpeg",
      "hash": "<sha256>",
      "width": 512,
      "height": 512,
      "data": "<base64>"
    }
  }
}
```

- `room.snapshot` / `member.joined` 中的成员只带 `hasAvatar` 和 `avatarHash`，不内嵌图片
- 新成员收到 `room.snapshot` 后，服务端再逐条发送 `member.avatar`
- 头像更新限频 2 秒；MIME 只接受 `image/jpeg` / `image/png` / `image/webp`
- 服务端会校验图片 magic、尺寸（≤512）与字节数（≤128 KiB）
- 旧客户端忽略新字段，不影响状态同步

## 戳一下（poke）

```jsonc
// C → S
{ "v": 1, "type": "poke.send", "id": "p1", "payload": { "targetId": "u_1a2b3c4d" } }

// S → 被戳者
{
  "v": 1, "type": "poke.received", "ts": 1730000000000,
  "payload": {
    "from": { "id": "u_9f8e7d6c", "name": "朔月", "device": "windows", "mode": "focusing" },
    "targetId": "u_1a2b3c4d",
    "at": 1730000000000
  }
}

// S → 发起者
{
  "v": 1, "type": "poke.sent", "ts": 1730000000000,
  "payload": { "targetId": "u_1a2b3c4d", "targetName": "小林", "at": 1730000000000 }
}
```

错误码：`bad_poke`（请求格式错误）、`poke_self`（不能戳自己）、
`poke_not_found`（目标不在本房间或已离线）、`slow_down`（2 秒内重复戳）。

客户端收到 `poke.received` 后自行决定提示方式；服务端不持久化、不回放、不向房间广播。

## 状态

草案。等第一个端到端 spike 跑通后再写正式 schema。
