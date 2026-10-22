# 9mRoom · Android 客户端

## 当前状态

Material You 界面 + 基础房间协议已跑通。

已实现：

- Kotlin + Jetpack Compose + Material 3
- Android 12+ 动态取色，低版本珊瑚橙回退主题
- 手机底部导航 / 平板侧边 NavigationRail 自适应
- 服务器地址 / 房间码 / 昵称连接页
- `/healthz`、`/api/rooms/{code}`、`/v1/ws` 连接
- 稳定 `ClientId`，本地 SharedPreferences 保存
- 房间快照、成员加入/离开、在线状态
- 成员详情、房间日志、实时聊天
- 状态上报（当前为 Android 应用块）
- 断线 3 秒后自动重连
- 成员详情 BottomSheet、状态编辑 BottomSheet
- 设置中的常驻通知、悬浮窗、共享当前应用、共享音乐开关

还在用示例数据的部分：

- 专注计时器目前是本地演示，还未上报计时块
- 待办目前是本地内存列表，还未持久化
- 使用情况访问、媒体会话、悬浮窗、前台服务尚未接入
- 检查更新入口还未接 GitHub Releases

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

首次构建会下载 Compose / AndroidX / OkHttp 依赖。如果本机代理不可用，Gradle 可能连接超时，需要先恢复代理或临时直连。

## 目录

```text
app/src/main/java/com/ninemroom/app/
  MainActivity.kt
  RoomViewModel.kt
  data/
    AppSettings.kt
    PresenceSocket.kt
    RoomApi.kt
    RoomModels.kt
  ui/
    NineRoomApp.kt
    theme/
  feature/
    connect/
    home/
    focus/
    todo/
    room/
    settings/
```

