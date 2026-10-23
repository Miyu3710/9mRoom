# 9mRoom

一个「在线自习室」：把每个人此刻在做什么——在写代码、在看书、在听什么歌、番茄还剩多久——
以低打扰的方式同步到一个房间里，让远程自习也有同桌感。

- **服务端**：Linux + Docker 部署，负责账号、房间、状态同步与时长统计
- **客户端**：Windows 与 Android 两端，共用一套视觉语言
- **状态来源**：前台应用类别 + 系统音乐（Windows SMTC / Android MediaSession）+ 自习计时
- **状态展示**：可编排的「同步列表」——应用、音乐、待办、计时器、自定义文本与脚本，自由排序与模板

> 当前 Windows / Android 客户端与服务端（SQLite 房间 / 管理员、聊天、状态同步）均已可用。
> Windows 1.2.0 已完成 Material Design 3 重构，安装包改为 per-machine / x64。
> 下载与版本历史：[Windows](https://github.com/Miyu3710/9mRoom/releases/tag/windows-v1.2.0) ·
> [Linux 服务端](https://github.com/Miyu3710/9mRoom/releases/tag/server-v1.1.0) ·
> [Android](https://github.com/Miyu3710/9mRoom/releases/tag/android-v1.1.4)
> 先读 [Structure.md](Structure.md) 了解目录，再看 [docs/development-approach.md](docs/development-approach.md) 了解开发思路。
> Windows 端待办与功能提议见 [ToDo.md](ToDo.md)，用户提出的问题与需求见 [Fix.md](Fix.md)。

## 关于隐私的设计底线

这是协作式自习，不是员工监控：默认不上传窗口标题、文件名、URL 或屏幕内容，
不做键盘记录与截图。窗口标题传输默认关闭，开启后为黑名单模式；原始进程名 / 包名只留在本机，
服务端只接收归一化后的 `appId` 与类别。

## 目录

```
clients/windows/   clients/android/   server/   shared/   design/   docs/
```

各目录职责与放置约定见 [Structure.md](Structure.md)。
状态块的规格与两端一致性用例见 [shared/status-blocks/](shared/status-blocks/README.md)。

## 部署

服务端提供 Docker 镜像与 compose：

```bash
cp .env.example .env
# 修改 .env 里的管理员密码
docker compose up -d --build
```

默认监听 `5080`，管理页 `/admin`，健康检查 `/healthz`。
数据保存在命名卷 `9mroom-data`，升级用 `docker compose up -d --build` 不会清空数据。
完整说明（HTTPS 反代、备份恢复、无 Docker 运行）见 [docs/deploy-docker.md](docs/deploy-docker.md)。
三端版本、tag 与更新清单规则见 [docs/releases.md](docs/releases.md)。

## 安装 Windows 客户端

当前版本：**1.2.0**（`windows-v1.2.0`）

- 安装包：`9mRoom-Setup-1.2.0.msi`
- per-machine 安装，需要管理员权限（UAC）
- 默认安装到 `Program Files`，安装向导保留自定义路径页
- 安装 / 卸载都由 Windows Installer 以管理员令牌执行，不再依赖普通用户修改 `D:\Config.Msi`
- 仅提供 MSI，不发布便携版
- 当前为未签名构建，SmartScreen 可能提示“未知发布者”
- 打包、签名与报毒说明见 [packaging/README.md](packaging/README.md) 和 [docs/installer-antivirus.md](docs/installer-antivirus.md)

---

维护：朔月
