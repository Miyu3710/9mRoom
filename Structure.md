# 9mRoom 项目结构

> 更新：2026-10-20 · 阶段：M0 服务端 + M1 采集 + WPF 基础界面
>
> 本文件只回答「东西放哪里、怎么放」。技术选型与取舍记录在 `docs/adr/`，
> 为什么这样分层、先做什么见 [`docs/development-approach.md`](docs/development-approach.md)。

## 1. 顶层结构

```
9mRoom/
├─ clients/                     客户端，按平台分目录，各自独立构建
│  ├─ windows/
│  │  ├─ src/NineMRoom.Client.Core   采集与协议（前台 / SMTC / 空闲 / 上报）
│  │  ├─ src/NineMRoom.Client.Cli    无界面探针（M1 验证）
│  │  └─ src/NineMRoom.Client.App    WPF 界面（连接 / 状态 / 成员）
│  └─ android/                  Android 客户端（UsageStats / MediaSession / 前台服务）
├─ server/                      服务端（M0 已可运行；Linux + Docker 为部署目标）
├─ shared/                      跨端共享的「契约」与「数据」，不放可执行代码
│  ├─ protocol/                 HTTP 与 WebSocket 的消息契约（单一来源）
│  ├─ app-catalog/              平台标识 → appId / 类别 的归一化目录
│  └─ status-blocks/            状态块（同步列表）规格 + 两端共用的一致性用例
├─ design/                      统一视觉：tokens、图标、参考屏
│  └─ reference/                参考实现的截图，用于对齐信息密度
├─ docs/                        方案与决策
│  ├─ development-approach.md   开发思路（当前主线文档）
│  └─ adr/                      架构决策记录（0001 已记录）
├─ README.md
├─ Structure.md                 本文件
├─ ToDo.md                      Windows 端待办、功能提议与验收标准
└─ .gitignore
```

## 2. 各目录职责

| 路径 | 职责 | 不该放什么 | 现状 |
| --- | --- | --- | --- |
| `clients/windows/` | 前台窗口识别、SMTC 音乐、空闲检测、托盘与自启、自定义状态脚本、Windows UI | 服务端代码、Android 代码、共享业务规则 | 1.2.0，M3 全页面 |
| `clients/android/` | UsageStats 前台识别、MediaSession、前台服务、Android UI | 同上 | 基础版可用 |
| `server/` | 管理员与房间持久化、实时在场广播、状态块校验、Docker 部署 | 客户端 UI、平台相关代码 | SQLite + 管理页可运行 |
| `shared/protocol/` | 消息定义、错误码、版本与兼容策略 | 任何一种语言的实现 | M0 子集已实现 |
| `shared/app-catalog/` | Windows 进程名 / Android 包名 → `appId`、类别、展示名 | 运行时逻辑、图标二进制 | 首批 50 个应用 |
| `shared/status-blocks/` | 状态块规格、渲染规则、两端共用的黄金用例 | 任一端的渲染实现 | 草案 v0.2 |
| `design/` | `tokens.json`、SVG 图标源文件、参考屏截图 | 生成后的主题代码（生成物进各端） | tokens + 参考图 |
| `docs/` | 方案、调研结论、ADR | 临时草稿（放 `tmp/`，不入库） | 使用中 |

## 3. 放置约定

1. **客户端私有代码只进本端目录。** 跨端复用的只有「契约」与「数据」，通过 `shared/` 与生成物同步，不做源码级共享。
2. **契约先行。** 任何新增状态字段，先改 `shared/protocol/`，再改两端与服务端。
3. **`appId` 是唯一联结点。** 两端各自在本地把进程名 / 包名映射成 `appId` 与类别，服务端与其他用户只见到 `appId`，见不到原始标识。
4. **生成物不入库。** 由 tokens 生成的 `Tokens.xaml` / `Theme.kt`、构建产物、位图资源由脚本产出。
5. **文档命名。** 方案文档用 `kebab-case.md`；决策用 `docs/adr/NNNN-短标题.md`（四位递增序号）。
6. **临时脚本**放 `tools/`（暂未创建），一次性实验放 `tmp/`（已被忽略）。
7. **状态文本的渲染规则以 `shared/status-blocks/` 为准。** 两端各写一份实现，但必须跑同一份 `fixtures.json`。
8. **待办清单**统一在根目录 `ToDo.md` 维护，按优先级分组，完成项打勾并保留 ID。

## 4. 目录进化规则

- 新增平台：`clients/<platform>/`，不改动现有目录。
- 服务端若要拆分（api / worker / 定时任务），在 `server/` 内部拆，不新增顶层目录。
- 共享代码出现第二份拷贝、且有明确收益时，才抽到 `shared/` 下做成可执行包，避免过早抽象。
- 重命名或移动顶层目录，需要先写一条 ADR。

## 5. 待补（骨架阶段刻意留空）

- [x] `server/` 的 M0 骨架（内存态房间 + WebSocket + 调试台）
- [x] `shared/protocol/` 的 M0 子集（正式 schema 待补）
- [x] Windows 端 M1 采集探针（前台 / SMTC / 空闲 / 上报）
- [x] Windows 构建脚本与本地 SDK 环境说明
- [x] WPF 基础界面（连接 / 我的状态 / 房间成员）
- [x] `design/tokens.json` 与 WPF 资源生成
- [ ] 托盘、开机自启、自定义标题栏
- [ ] Android 端采集探针（S1 需要真机）
- [ ] 两端状态块渲染器（C# 已通过 fixtures，Kotlin 待做）
- [ ] `server/` 的 Dockerfile 与 compose 编排
- [ ] 剩余工具链：Docker 可只在 Linux 侧使用
