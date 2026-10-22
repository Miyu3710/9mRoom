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
- 前台服务 + 常驻通知（`specialUse`）
- 成员进入 / 离开通知
- 可选悬浮状态条，需要系统悬浮窗权限
- 使用情况访问识别前台应用，归一化为 `appId` / 分类后上报
- 通知使用权读取当前媒体会话，生成音乐状态块
- 待办本地持久化（SharedPreferences）
- 专注计时器持久化，计时块上报，完成通知
- GitHub Releases 检查更新，发现新版本打开 Release 页面
- 通知权限、悬浮窗权限、使用情况访问、通知使用权入口
- 设置中的常驻通知、悬浮窗、共享当前应用、共享音乐开关

还在用示例数据的部分：

- 应用分类管理页还未实现
- 待办子任务编辑、起止时间还未实现
- 开机自启还未接 `BOOT_COMPLETED`
- 悬浮条已支持点击回到房间
- 应用识别只映射内置的常见包名，未知应用统一归为“其他应用”，不会上传原包名
- 更新下载目前跳转 GitHub Release 页面，未做应用内 APK 下载与安装

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
    FocusTimerController.kt
    ForegroundAppReader.kt
    MediaReader.kt
    PresenceSocket.kt
    RoomApi.kt
    RoomModels.kt
    TodoStore.kt
    UpdateChecker.kt
  ui/
    NineRoomApp.kt
    theme/
  service/
    NotificationCenter.kt
    PresenceService.kt
  feature/
    connect/
    home/
    focus/
    todo/
    room/
    settings/
```

