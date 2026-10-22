# 9mRoom · 服务端

## 当前状态

**M0 + 持久化房间可运行。** WebSocket 状态同步、SQLite 管理员 / 房间、`/admin` 管理页已经打通；
聊天与日志仍只存在于客户端本次连接内存中。

## 快速开始

### Docker（推荐部署方式）

```bash
cp .env.example .env
# 修改 .env 中的 NINEMROOM_ADMIN_PASSWORD
docker compose up -d --build
docker compose ps
```

- 服务端：<http://127.0.0.1:5080/>
- 管理页：<http://127.0.0.1:5080/admin>
- 数据卷：`9mroom-data`
- 完整部署、HTTPS、备份与升级见 [docs/deploy-docker.md](../docs/deploy-docker.md)

### 本地开发

```powershell
$env:NINEMROOM_ADMIN_USER = 'admin'
$env:NINEMROOM_ADMIN_PASSWORD = '换成一个强密码'
.\run.ps1
```

首次启动且数据库没有管理员时，会用这两个环境变量创建管理员。
未提供密码时会生成随机密码，并在启动日志中输出一次，请及时保存。

- 调试台：<http://127.0.0.1:5080/>
- 管理页：<http://127.0.0.1:5080/admin>
- 停止：`Ctrl+C`；端口被占用时改 `run.ps1` 里的 `ASPNETCORE_URLS`

## 接口

| 路径 | 说明 |
| --- | --- |
| `GET /healthz` | 健康检查，返回服务端时间、房间数、成员数 |
| `GET /api/rooms/{code}` | `exists / kind / name / online`；`kind=server` 可设为家 |
| `GET /admin` | 管理页；未登录显示登录表单 |
| `POST /admin/login` | 管理员登录，设置 HttpOnly Cookie |
| `POST /admin/logout` | 退出登录 |
| `POST /admin/rooms/create` | 创建服务端房间（房间码 / 显示名） |
| `POST /admin/rooms/delete` | 删除房间，并关闭该房间在线连接 |
| `GET /debug/room/{id}` | 只读房间快照；仅 Development，或设置 `NINEMROOM_DEBUG=1` 时开放 |
| `GET /` | 调试台静态页 |
| `WS /v1/ws` | 状态同步；加入前校验数据库房间，未知房间返回 404 |

WebSocket 消息定义见 [`shared/protocol/`](../shared/protocol/README.md)，
当前实现 `welcome` / `room.snapshot` / `presence.changed` / `member.joined` / `member.left` /
`chat.message` / `pong` / `error`。

## 数据

默认数据库：`{ContentRoot}/data/9mroom.db`，可用 `NINEMROOM_DB` 指定其他路径。

| 表 | 内容 |
| --- | --- |
| `admins` | 管理员账号；PBKDF2-SHA256（120000 迭代 + 随机盐） |
| `rooms` | 房间码、名称、类型、创建时间、最近活跃 |
| `sessions` | 管理页会话 token 与 12 小时过期时间 |

- 预置 `demo` 服务端房间
- 管理页创建的房间 `kind=server`，可设为“家”
- 普通客户端不能建房；未知房间码不会自动创建
- 管理员删除房间后，客户端重连收到 404 并清空家设置

## 协议冒烟测试

先启动服务端，另开一个窗口：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\protocol-smoke.ps1
```

覆盖 welcome、snapshot、自身与广播、状态块往返、非法块拒绝、ping/pong、离开通知等断言。

## 本地构建环境

本机没有全局 .NET SDK，SDK 解压放在仓库根的 `.tools/dotnet`（已 gitignore），版本 8.0.426。

- `build.ps1` / `run.ps1` 会把 `DOTNET_CLI_HOME`、`NUGET_PACKAGES`、`APPDATA`、`LOCALAPPDATA`
  重定向到 `.tools/`
- 服务端依赖 `Microsoft.Data.Sqlite` 8.0.8，首次构建需要能访问 NuGet 或已有本地缓存

## 数据与限制

- 房间与管理员持久化，服务端重启后仍存在
- 成员在线状态仍在内存，重启后需客户端重连
- 客户端用持久化 `ClientId` 重连时，服务端会移除并关闭同 ClientId 的旧连接，避免房间里出现重复的自己
- 聊天 / 日志不落库，只保留在客户端本次连接
- 本地开发只监听 `127.0.0.1`；Docker 容器监听 `0.0.0.0:8080`，由 compose 映射到宿主机
- 管理页使用 HttpOnly Cookie 会话；HTTPS 部署时设置 `NINEMROOM_COOKIE_SECURE=1`
- `Production` 默认关闭 `/debug/room`，需要时显式设置 `NINEMROOM_DEBUG=1`
- presence 有 90 秒 TTL；正常关闭或进程退出会立即移除成员
- 用户自建房大厅与空闲 TTL 尚未实现

## 下一步

1. 用户自建房 + 空闲 TTL 自动销毁
2. `shared/protocol/` 正式 schema（OpenAPI + JSON Schema）
3. 管理页审计日志、多管理员管理与 CSRF 防护
