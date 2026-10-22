# 用 Docker 部署 9mRoom 服务端

适用于 Linux 服务器（或安装了 Docker Desktop 的 Windows / macOS）。  
服务端镜像只包含 ASP.NET Core 运行时与 SQLite，不包含 Windows 客户端。

## 1. 准备

- Docker Engine 24+ 与 Docker Compose v2
- 一个可用的端口（默认 `5080`）
- 如果要公网使用，建议在前面加一层 HTTPS 反向代理（Caddy / Nginx / Traefik）

把仓库放到服务器：

```bash
git clone <仓库地址> 9mRoom
cd 9mRoom
```

> 只需要部署时，也可以只复制 `server/`、`docker-compose.yml`、`.env.example` 到服务器。

## 2. 配置管理员与端口

```bash
cp .env.example .env
```

编辑 `.env`：

```dotenv
NINEMROOM_ADMIN_USER=admin
NINEMROOM_ADMIN_PASSWORD=换成一个足够长的随机密码
NINEMROOM_PORT=5080
NINEMROOM_COOKIE_SECURE=0
```

- `NINEMROOM_ADMIN_USER` / `NINEMROOM_ADMIN_PASSWORD` 只在**数据库还没有管理员时**使用；之后改环境变量不会改密码。
- 首次创建的账号是**超级管理员**；旧数据库升级时，最早创建的管理员会自动补为超级管理员。
- 超级管理员可在 `/admin` 创建 / 删除普通管理员、设置密码；普通管理员只能管理房间。
- 密码至少使用 16 位随机字符，不要沿用示例值。
- 如果通过 HTTPS 反向代理访问，把 `NINEMROOM_COOKIE_SECURE` 设为 `1`。

## 3. 启动

```bash
docker compose up -d --build
docker compose ps
docker compose logs -f server
```

首次启动会：

1. 创建 `/data/9mroom.db`
2. 用 `.env` 中的账号创建第一个管理员
3. 预置 `demo` 服务端房间

检查：

```bash
curl -fsS http://127.0.0.1:5080/healthz
```

返回 `{"ok":true,...}` 即正常。

- 管理页：`http://服务器IP:5080/admin`
- 调试台：`http://服务器IP:5080/debug`（根路径 `/` 不提供控制台）
- 客户端服务器地址填：`http://服务器IP:5080`（或 HTTPS 地址）

## 4. 数据持久化与备份

数据库保存在命名卷 `9mroom-data` 中，`docker compose down` 不会删除；`down -v` 会删除。

```bash
# 查看卷名（通常是 9mroom_9mroom-data）
docker volume ls | grep 9mroom

# 备份：先停服务，导出卷内容
docker compose stop server
docker run --rm \
  -v 9mroom_9mroom-data:/data:ro \
  -v "$PWD":/backup \
  alpine tar czf /backup/9mroom-data-$(date +%F).tgz -C /data .
docker compose start server

# 恢复：停服务，清空卷后解压
docker compose stop server
docker run --rm \
  -v 9mroom_9mroom-data:/data \
  -v "$PWD":/backup \
  alpine sh -c 'rm -rf /data/* && tar xzf /backup/9mroom-data-YYYY-MM-DD.tgz -C /data'
docker compose start server
```

如果改用目录挂载：

```yaml
volumes:
  - ./data:/data
```

需要让容器内 `app` 用户可写：

```bash
mkdir -p data
sudo chown -R 1654:1654 data
```

## 5. 升级

```bash
git pull
docker compose up -d --build
```

数据库结构由服务端启动时自动补齐，升级不会清空房间与管理员。

## 6. HTTPS 反向代理示例（Caddy）

```caddyfile
room.example.com {
    reverse_proxy 127.0.0.1:5080
    encode zstd gzip
}
```

Caddy 会自动处理 WebSocket Upgrade 和证书。  
把 `.env` 中的 `NINEMROOM_COOKIE_SECURE` 改为 `1` 后重启：

```bash
docker compose up -d
```

Nginx 需要显式转发 Upgrade：

```nginx
location / {
    proxy_pass http://127.0.0.1:5080;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```

## 7. 安全注意事项

- 公网只暴露反向代理的 443，`5080` 只监听内网 / 本机。
- `Production` 环境默认关闭 `/debug/room`；确实需要临时打开时设置 `NINEMROOM_DEBUG=1`，用完立即关闭。
- 管理页目前**没有 CSRF token**，不要在不可信网络长期暴露；公网使用务必配合 HTTPS 与强密码。
- 聊天与成员在线状态只在内存中，服务端重启后需要客户端重连。
- 反向代理与容器都要限制请求体大小（当前 WebSocket 消息上限 64 KiB）。

## 8. 无 Docker 的 Linux 运行

```bash
dotnet publish server/NineMRoom.Server.csproj -c Release -o /opt/9mroom
NINEMROOM_ADMIN_USER=admin \
NINEMROOM_ADMIN_PASSWORD='...' \
NINEMROOM_DB=/var/lib/9mroom/9mroom.db \
ASPNETCORE_URLS=http://0.0.0.0:5080 \
dotnet /opt/9mroom/NineMRoom.Server.dll
```

建议用 systemd 托管，并把数据目录权限交给运行用户。
