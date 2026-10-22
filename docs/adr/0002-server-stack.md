# 0002 服务端技术栈与首版通信方式

- 日期：2026-10-20
- 状态：已接受

## 背景

服务端要承担账号、房间与实时在场同步。约束条件：

- 维护者只有一人，Windows 端已经是 .NET；
- 本机只装了 .NET 运行时，SDK 需要额外安装；沙箱内 NuGet 的 HTTPS 不可用；
- 部署目标是 Linux + Docker，单机起步即可。

## 方案对比

| 方案 | 优点 | 代价 |
| --- | --- | --- |
| **ASP.NET Core** | 与 Windows 客户端同语言，可共享 DTO；部署到 Linux 简单；零第三方包即可跑 HTTP 与 WebSocket | 本机需补 SDK；NuGet 受限时依赖扩展困难 |
| Ktor | 与 Android 同语言；Gradle 工具链已就绪 | 服务端生态与运维经验更少；JVM 镜像更大 |
| Go | 部署最简、并发模型清晰 | 引入第三种语言，两端都无法共享类型 |

## 决定

- 使用 **ASP.NET Core（net8.0，minimal API）**；
- 实时通道用**原生 WebSocket**，暂不引入 SignalR，保持协议语言中立；
- 序列化用 `System.Text.Json`；服务端只校验状态块，不渲染；
- M0 阶段房间与成员放在内存，PostgreSQL 推迟到账号里程碑。

## 后果

- 三端共享的是 `shared/protocol/` 的契约，而不是 C# 类型；C# 类型只作为服务端内部实现。
- 消息处理手写，但消息集很小；若将来改用 SignalR，客户端传输层需要重写。
- 零 NuGet 依赖让当前环境可以离线构建，但单元测试框架、EF Core 等需要先解决离线包方案。
- 渲染规则漂移的风险留在两端，由 `shared/status-blocks/fixtures.json` 兜住。
- 什么时候重新考虑：需要横向扩容到多实例时（引入 Redis 或改用托管实时服务），
  或客户端数量增长到需要成熟 SDK 时。
