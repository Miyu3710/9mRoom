package com.ninemroom.app.feature.settings

import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.OpenInNew
import androidx.compose.material.icons.outlined.Apps
import androidx.compose.material.icons.outlined.ChevronRight
import androidx.compose.material.icons.outlined.ColorLens
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material.icons.outlined.Notifications
import androidx.compose.material.icons.outlined.Security
import androidx.compose.material.icons.outlined.Settings
import androidx.compose.material.icons.outlined.Wifi
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Slider
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.ninemroom.app.data.ConnectionStatus
import com.ninemroom.app.data.UpdateChecker
import com.ninemroom.app.data.UpdateInfo
import kotlinx.coroutines.launch

@Composable
fun SettingsScreen(
    dynamicColor: Boolean,
    onDynamicColorChange: (Boolean) -> Unit,
    onOpenAppRules: () -> Unit,
    serverUrl: String,
    roomName: String,
    displayName: String,
    connection: ConnectionStatus,
    keepOnline: Boolean,
    overlayEnabled: Boolean,
    notifyRoomEvents: Boolean,
    notifyFocusDone: Boolean,
    shareApp: Boolean,
    shareMusic: Boolean,
    autoStart: Boolean,
    autoConnect: Boolean,
    homeRoomCode: String,
    homeRoomName: String,
    isServerRoom: Boolean,
    overlayOpacity: Float,
    overlaySize: Int,
    overlayMargin: Int,
    onKeepOnlineChange: (Boolean) -> Unit,
    onOverlayChange: (Boolean) -> Unit,
    onNotifyRoomEventsChange: (Boolean) -> Unit,
    onNotifyFocusDoneChange: (Boolean) -> Unit,
    onShareAppChange: (Boolean) -> Unit,
    onShareMusicChange: (Boolean) -> Unit,
    onAutoStartChange: (Boolean) -> Unit,
    onAutoConnectChange: (Boolean) -> Unit,
    onSetHomeRoom: () -> Unit,
    onClearHomeRoom: () -> Unit,
    onOverlayOpacityChange: (Float) -> Unit,
    onOverlaySizeChange: (Int) -> Unit,
    onOverlayMarginChange: (Int) -> Unit,
    onRequestNotifications: () -> Unit,
    onRequestOverlay: () -> Unit,
    onRequestUsageAccess: () -> Unit,
    onRequestNotificationAccess: () -> Unit,
    onDisconnect: () -> Unit,
) {
    var checkingUpdate by remember { mutableStateOf(false) }
    var updateStatus by remember { mutableStateOf("") }
    var updateInfo by remember { mutableStateOf<UpdateInfo?>(null) }
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val currentVersion = remember {
        runCatching {
            context.packageManager.getPackageInfo(context.packageName, 0).versionName
        }.getOrNull().orEmpty().ifBlank { "1.0.0" }
    }

    LazyColumn(
        contentPadding = PaddingValues(horizontal = 20.dp, vertical = 16.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        item {
            Column {
                Text("设置", style = MaterialTheme.typography.headlineMedium)
                Text(
                    "连接、通知和隐私都由你控制。",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(top = 4.dp),
                )
            }
        }

        item {
            SettingsCard("当前连接", Icons.Outlined.Wifi) {
                SettingAction(title = "服务器", description = serverUrl.ifBlank { "未设置" })
                SettingAction(title = "房间", description = roomName.ifBlank { "未进入房间" })
                SettingAction(title = "昵称", description = displayName.ifBlank { "未设置" })
                if (connection != ConnectionStatus.Idle) {
                    OutlinedButton(
                        onClick = onDisconnect,
                        modifier = Modifier.padding(top = 6.dp),
                    ) {
                        Text("断开连接")
                    }
                }
            }
        }

        if (isServerRoom) {
            item {
                SettingsCard("家房间", Icons.Outlined.Home) {
                    Text(
                        if (homeRoomCode.isBlank()) "未设置" else "$homeRoomName（$homeRoomCode）",
                        style = MaterialTheme.typography.bodyLarge,
                    )
                    Text(
                        "打开应用时自动登录服务器并进入这个房间。",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(top = 2.dp),
                    )
                    SettingSwitch(
                        title = "打开应用自动连接",
                        description = "启动后自动登录并进入家房间",
                        checked = autoConnect,
                        onCheckedChange = onAutoConnectChange,
                    )
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        OutlinedButton(
                            onClick = onSetHomeRoom,
                            enabled = roomName.isNotBlank(),
                        ) {
                            Text("设为当前房间")
                        }
                        if (homeRoomCode.isNotBlank()) {
                            TextButton(onClick = onClearHomeRoom) {
                                Text("清除")
                            }
                        }
                    }
                }
            }
        }

        item {
            SettingsCard("应用与分类", Icons.Outlined.Apps) {
                SettingAction(
                    title = "管理应用分类",
                    description = "为应用设置分类、隐藏应用的状态共享",
                    onClick = onOpenAppRules,
                )
            }
        }

        item {
            SettingsCard("连接与常驻", Icons.Outlined.Settings) {
                SettingSwitch(
                    title = "保持在线",
                    description = "分享状态时显示常驻通知，退出界面后仍保持连接",
                    checked = keepOnline,
                    onCheckedChange = onKeepOnlineChange,
                )
                SettingSwitch(
                    title = "开机自启",
                    description = "开机后静默恢复上次的房间",
                    checked = autoStart,
                    onCheckedChange = onAutoStartChange,
                )
                SettingSwitch(
                    title = "悬浮窗",
                    description = "在其他应用上方显示迷你状态条，需要悬浮窗权限",
                    checked = overlayEnabled,
                    onCheckedChange = onOverlayChange,
                )
                SettingAction(
                    title = "悬浮窗权限",
                    description = "到系统设置允许 9mRoom 显示在其他应用上方",
                    onClick = onRequestOverlay,
                )
            }
        }

        item {
            SettingsCard("悬浮窗外观", Icons.Outlined.Wifi) {
                SettingSlider(
                    title = "透明度",
                    valueText = "${(overlayOpacity * 100).toInt()}%",
                    value = overlayOpacity,
                    valueRange = 0.3f..1f,
                    onValueChange = onOverlayOpacityChange,
                )
                SettingSlider(
                    title = "大小（宽度）",
                    valueText = "$overlaySize dp",
                    value = overlaySize.toFloat(),
                    valueRange = 80f..180f,
                    onValueChange = { onOverlaySizeChange(it.toInt()) },
                )
                SettingSlider(
                    title = "边距",
                    valueText = "$overlayMargin dp",
                    value = overlayMargin.toFloat(),
                    valueRange = 4f..48f,
                    onValueChange = { onOverlayMarginChange(it.toInt()) },
                )
            }
        }

        item {
            SettingsCard("通知", Icons.Outlined.Notifications) {
                SettingSwitch(
                    title = "成员进出通知",
                    description = "有人进入或离开房间时提醒",
                    checked = notifyRoomEvents,
                    onCheckedChange = onNotifyRoomEventsChange,
                )
                SettingSwitch(
                    title = "常驻通知",
                    description = "保持在线时显示当前房间和状态",
                    checked = keepOnline,
                    onCheckedChange = onKeepOnlineChange,
                )
                SettingSwitch(
                    title = "专注结束提醒",
                    description = "计时结束时通过通知提醒",
                    checked = notifyFocusDone,
                    onCheckedChange = onNotifyFocusDoneChange,
                )
                SettingAction(
                    title = "通知权限",
                    description = "Android 13+ 需要允许通知，否则系统不会显示提醒",
                    onClick = onRequestNotifications,
                )
            }
        }

        item {
            SettingsCard("隐私", Icons.Outlined.Security) {
                SettingSwitch(
                    title = "共享当前应用",
                    description = "只上传归一化 appId 和分类，不上传包名列表",
                    checked = shareApp,
                    onCheckedChange = onShareAppChange,
                )
                SettingSwitch(
                    title = "共享音乐",
                    description = "读取当前媒体会话的标题与歌手",
                    checked = shareMusic,
                    onCheckedChange = onShareMusicChange,
                )
                SettingAction(
                    title = "使用情况访问权限",
                    description = "允许 9mRoom 识别当前前台应用",
                    onClick = onRequestUsageAccess,
                )
                SettingAction(
                    title = "通知使用权",
                    description = "允许 9mRoom 读取当前媒体会话",
                    onClick = onRequestNotificationAccess,
                )
            }
        }

        item {
            SettingsCard("外观", Icons.Outlined.ColorLens) {
                SettingSwitch(
                    title = "动态取色",
                    description = "Android 12+ 跟随壁纸配色",
                    checked = dynamicColor,
                    onCheckedChange = onDynamicColorChange,
                )
                SettingAction(
                    title = "主题模式",
                    description = "跟随系统",
                )
            }
        }

        item {
            SettingsCard("关于", Icons.Outlined.Info) {
                SettingAction(
                    title = "版本",
                    description = currentVersion,
                )
                SettingAction(
                    title = "检查更新",
                    description = if (checkingUpdate) "正在检查…" else "从 GitHub Releases 获取最新 APK",
                    icon = Icons.AutoMirrored.Outlined.OpenInNew,
                    onClick = {
                        if (checkingUpdate) return@SettingAction
                        checkingUpdate = true
                        updateStatus = "正在检查更新…"
                        scope.launch {
                            try {
                                val info = UpdateChecker.check(currentVersion)
                                if (info == null) {
                                    updateStatus = "已是最新版本（$currentVersion）"
                                } else {
                                    updateInfo = info
                                    updateStatus = "发现新版本 ${info.latestVersion}"
                                }
                            } catch (error: Exception) {
                                updateStatus = "检查失败：${error.message ?: "网络错误"}"
                            } finally {
                                checkingUpdate = false
                            }
                        }
                    },
                )
                if (updateStatus.isNotBlank()) {
                    Text(
                        updateStatus,
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.padding(top = 4.dp, bottom = 4.dp),
                    )
                }
                SettingAction(
                    title = "开源许可",
                    description = "查看使用的开源组件",
                )
            }
        }

        item { Spacer(Modifier.height(72.dp)) }
    }

    updateInfo?.let { info ->
        AlertDialog(
            onDismissRequest = { updateInfo = null },
            title = { Text("发现新版本") },
            text = { Text("最新版本 ${info.latestVersion}\n\n是否前往 GitHub Releases 下载 APK？") },
            confirmButton = {
                TextButton(
                    onClick = {
                        updateInfo = null
                        runCatching {
                            context.startActivity(
                                Intent(Intent.ACTION_VIEW, Uri.parse(info.releaseUrl)),
                            )
                        }
                    },
                ) { Text("前往下载") }
            },
            dismissButton = {
                TextButton(onClick = { updateInfo = null }) { Text("稍后") }
            },
        )
    }
}

@Composable
private fun SettingsCard(
    title: String,
    icon: ImageVector,
    content: @Composable () -> Unit,
) {
    Card(
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainer),
        shape = MaterialTheme.shapes.large,
        modifier = Modifier.fillMaxWidth(),
    ) {
        Column(Modifier.padding(18.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(icon, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
                Text(
                    title,
                    style = MaterialTheme.typography.titleMedium,
                    modifier = Modifier.padding(start = 8.dp),
                )
            }
            Spacer(Modifier.height(10.dp))
            content()
        }
    }
}

@Composable
private fun SettingSwitch(
    title: String,
    description: String,
    checked: Boolean,
    onCheckedChange: (Boolean) -> Unit,
) {
    Row(
        Modifier
            .fillMaxWidth()
            .padding(vertical = 7.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(Modifier.weight(1f)) {
            Text(title, style = MaterialTheme.typography.bodyLarge)
            Text(
                description,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 2.dp),
            )
        }
        Spacer(Modifier.padding(start = 8.dp))
        Switch(checked = checked, onCheckedChange = onCheckedChange)
    }
}

@Composable
private fun SettingSlider(
    title: String,
    valueText: String,
    value: Float,
    valueRange: ClosedFloatingPointRange<Float>,
    onValueChange: (Float) -> Unit,
) {
    Column(
        Modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text(title, style = MaterialTheme.typography.bodyLarge, modifier = Modifier.weight(1f))
            Text(
                valueText,
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        Slider(
            value = value,
            onValueChange = onValueChange,
            valueRange = valueRange,
        )
    }
}

@Composable
private fun SettingAction(
    title: String,
    description: String,
    icon: ImageVector = Icons.Outlined.ChevronRight,
    onClick: (() -> Unit)? = null,
) {
    Row(
        Modifier
            .fillMaxWidth()
            .then(
                if (onClick != null) {
                    Modifier.clickable(onClick = onClick)
                } else {
                    Modifier
                },
            )
            .padding(vertical = 9.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(Modifier.weight(1f)) {
            Text(title, style = MaterialTheme.typography.bodyLarge)
            Text(
                description,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 2.dp),
            )
        }
        Icon(icon, contentDescription = null, tint = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

