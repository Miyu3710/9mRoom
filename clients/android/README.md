# 9mRoom · Android 客户端

## 这一端要做什么（v1）

- 前台服务常驻（带常驻通知），可选开机自启
- 用 `UsageStatsManager` 识别前台应用，**在本地**映射成 `appId` 与类别
- 读取当前音乐：`MediaSessionManager` 的媒体元数据
- 锁屏或长时间无操作 → 「离开」
- 房间看板界面，与 Windows 端使用同一套 design tokens

## 关键权限与限制

| 能力 | 权限 / API | 坑 |
| --- | --- | --- |
| 前台应用 | `PACKAGE_USAGE_STATS`（特殊权限，需跳系统设置授权）+ `UsageStatsManager` | 用户必须手动开启「使用情况访问权限」；国产 ROM 会杀后台 |
| 音乐元数据 | `BIND_NOTIFICATION_LISTENER_SERVICE` + `MediaSessionManager.getActiveSessions()` | 需要「通知使用权」；部分应用不暴露 MediaSession |
| 常驻后台 | `FOREGROUND_SERVICE` + 常驻通知 | Android 14 起必须声明 foreground service type；应用商店对长期后台监控类应用审核严格 |
| 应用展示名 | `PackageManager` | Android 11+ 有包可见性限制；用内置目录覆盖常见应用，未知归为「其他」 |
| 省电策略 | 引导关闭电池优化、开启自启动 | 各家 ROM 路径不同，只能给图文引导 |

## 技术栈

未定。候选：Kotlin + Jetpack Compose，对齐既有 Compose 项目的构建方式
（Gradle wrapper、JDK 17、compileSdk 35）。

## 构建

待定（补齐后在此记录 `build.ps1` 用法、APK 产物位置与真机安装步骤）。
