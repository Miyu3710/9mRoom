# 9mRoom · Android 客户端

## 当前状态

Material You 界面骨架已可构建、安装。

已搭好：

- Kotlin + Jetpack Compose + Material 3
- Android 12+ 动态取色，低版本珊瑚橙回退主题
- 手机底部导航 / 平板侧边 NavigationRail 自适应
- 五个一级页面：自习室、专注、待办、房间、设置
- 成员详情 BottomSheet、状态编辑 BottomSheet
- 专注计时器、待办本地交互、聊天输入框
- 设置中的常驻通知、悬浮窗、共享当前应用、共享音乐开关

当前页面使用示例数据，**尚未接入 WebSocket / 服务端协议**。

## 包名

```text
com.ninemroom.app
```

## 构建

本地环境：JDK 17、Android SDK 35、Gradle 8.11.1。

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

APK：

```text
app/build/outputs/apk/debug/app-debug.apk
```

首次构建会下载 Compose / AndroidX 依赖。如果本机代理不可用，Gradle 可能连接超时，需要先恢复代理或临时直连。

## 下一阶段

1. 协议层：`/healthz`、`/api/rooms/{code}`、`/v1/ws`
2. 连接/加入房间与 `ClientId` 持久化
3. 房间快照、成员状态、聊天和日志
4. 前台服务、常驻通知、成员进出通知
5. 使用情况 / 媒体会话权限与本地映射
6. 悬浮窗
7. GitHub Releases APK 检查更新

## 目录

```text
app/src/main/java/com/ninemroom/app/
  MainActivity.kt
  ui/
    NineRoomApp.kt
    theme/
  feature/
    home/
    focus/
    todo/
    room/
    settings/
```
