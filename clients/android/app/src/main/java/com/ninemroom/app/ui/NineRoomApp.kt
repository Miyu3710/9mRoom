package com.ninemroom.app.ui

import android.Manifest
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInHorizontally
import androidx.compose.animation.slideOutHorizontally
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Apps
import androidx.compose.material.icons.outlined.Checklist
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.People
import androidx.compose.material.icons.outlined.Settings
import androidx.compose.material.icons.outlined.Timer
import androidx.compose.material.icons.outlined.Tune
import androidx.compose.material3.CenterAlignedTopAppBar
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.NavigationRail
import androidx.compose.material3.NavigationRailItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import com.ninemroom.app.RoomViewModel
import com.ninemroom.app.data.ConnectionStatus
import com.ninemroom.app.feature.apps.AppRulesScreen
import com.ninemroom.app.feature.apps.CategoryManagerScreen
import com.ninemroom.app.feature.connect.ConnectScreen
import com.ninemroom.app.feature.focus.FocusScreen
import com.ninemroom.app.feature.home.HomeScreen
import com.ninemroom.app.feature.room.RoomScreen
import com.ninemroom.app.feature.settings.SettingsScreen
import com.ninemroom.app.feature.sync.SyncListScreen
import com.ninemroom.app.feature.todo.TodoScreen
import kotlinx.coroutines.launch

enum class Destination(
    val route: String,
    val label: String,
    val icon: ImageVector,
) {
    Home("home", "自习室", Icons.Outlined.Home),
    Focus("focus", "专注", Icons.Outlined.Timer),
    Todo("todo", "待办", Icons.Outlined.Checklist),
    Room("room", "房间", Icons.Outlined.People),
    Settings("settings", "设置", Icons.Outlined.Settings),
    Apps("apps", "应用", Icons.Outlined.Apps),
    Categories("categories", "分类管理", Icons.Outlined.Apps),
    SyncList("sync-list", "同步列表", Icons.Outlined.Tune),
}

private val navDestinations = Destination.entries.filter {
    it != Destination.Apps &&
        it != Destination.Categories &&
        it != Destination.SyncList
}

private fun navDirection(fromRoute: String?, toRoute: String?): Int {
    val fromIndex = Destination.entries.indexOfFirst { it.route == fromRoute }
    val toIndex = Destination.entries.indexOfFirst { it.route == toRoute }
    return if (fromIndex >= 0 && toIndex >= 0 && toIndex < fromIndex) -1 else 1
}

private fun openBackgroundSettings(context: Context) {    val candidates = listOf(
        ComponentName(
            "com.coloros.safecenter",
            "com.coloros.safecenter.permission.startup.StartupAppListActivity",
        ),
        ComponentName(
            "com.oplus.safecenter",
            "com.oplus.safecenter.permission.startup.StartupAppListActivity",
        ),
        ComponentName(
            "com.miui.securitycenter",
            "com.miui.permcenter.autostart.AutoStartManagementActivity",
        ),
        ComponentName(
            "com.huawei.systemmanager",
            "com.huawei.systemmanager.startupmgr.ui.StartupNormalAppListActivity",
        ),
        ComponentName(
            "com.vivo.permissionmanager",
            "com.vivo.permissionmanager.activity.BgStartUpManagerActivity",
        ),
        ComponentName(
            "com.samsung.android.lool",
            "com.samsung.android.sm.ui.battery.BatteryActivity",
        ),
    )
    for (component in candidates) {
        val intent = Intent().setComponent(component)
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        if (context.packageManager.resolveActivity(intent, 0) == null) continue
        if (runCatching { context.startActivity(intent) }.isSuccess) return
    }
    runCatching {
        context.startActivity(
            Intent(
                Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                Uri.parse("package:${context.packageName}"),
            ).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK),
        )
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NineRoomApp(
    dynamicColor: Boolean,
    onDynamicColorChange: (Boolean) -> Unit,
    roomViewModel: RoomViewModel = viewModel(),
) {
    val state by roomViewModel.state.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val notificationPermissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission(),
    ) { }
    val overlayLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.StartActivityForResult(),
    ) { roomViewModel.refreshOverlay() }
    val usageAccessLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.StartActivityForResult(),
    ) { roomViewModel.refreshPresence() }
    val mediaAccessLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.StartActivityForResult(),
    ) { roomViewModel.refreshPresence() }
    var askedNotifications by rememberSaveable { mutableStateOf(false) }

    LaunchedEffect(state.showMain) {
        if (state.showMain &&
            !askedNotifications &&
            Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
            ContextCompat.checkSelfPermission(
                context,
                Manifest.permission.POST_NOTIFICATIONS,
            ) != PackageManager.PERMISSION_GRANTED
        ) {
            askedNotifications = true
            notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
        }
    }

    if (!state.showMain) {
        ConnectScreen(
            state = state,
            onConnect = roomViewModel::connect,
        )
        return
    }

    val navController = rememberNavController()
    val backStackEntry by navController.currentBackStackEntryAsState()
    val currentRoute = backStackEntry?.destination?.route
    val current = Destination.entries.firstOrNull { it.route == currentRoute } ?: Destination.Home
    val snackbar = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    val clipboard = LocalClipboardManager.current

    fun navigate(destination: Destination) {
        navController.navigate(destination.route) {
            popUpTo(navController.graph.startDestinationId) { saveState = true }
            launchSingleTop = true
            restoreState = true
        }
    }

    BoxWithConstraints(Modifier.fillMaxSize()) {
        val useRail = maxWidth >= 600.dp

        Scaffold(
            topBar = {
                CenterAlignedTopAppBar(
                    title = {
                        Column(horizontalAlignment = Alignment.CenterHorizontally) {
                            Text(
                                "9mRoom",
                                fontWeight = FontWeight.SemiBold,
                                fontSize = 18.sp,
                            )
                            Text(
                                current.label,
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                    },
                    actions = {
                        ConnectionPill(state.status, state.onlineCount)
                    },
                    colors = TopAppBarDefaults.centerAlignedTopAppBarColors(
                        containerColor = MaterialTheme.colorScheme.background,
                    ),
                )
            },
            bottomBar = {
                if (!useRail) {
                    NavigationBar(containerColor = MaterialTheme.colorScheme.surfaceContainer) {
                        navDestinations.forEach { destination ->
                            NavigationBarItem(
                                selected = current == destination,
                                onClick = { navigate(destination) },
                                icon = { Icon(destination.icon, contentDescription = destination.label) },
                                label = { Text(destination.label) },
                            )
                        }
                    }
                }
            },
            snackbarHost = { SnackbarHost(snackbar) },
            containerColor = MaterialTheme.colorScheme.background,
            contentWindowInsets = WindowInsets.safeDrawing,
        ) { padding ->
            Row(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(padding),
            ) {
                if (useRail) {
                    NavigationRail(
                        containerColor = MaterialTheme.colorScheme.surfaceContainer,
                    ) {
                        navDestinations.forEach { destination ->
                            NavigationRailItem(
                                selected = current == destination,
                                onClick = { navigate(destination) },
                                icon = { Icon(destination.icon, contentDescription = destination.label) },
                                label = { Text(destination.label) },
                            )
                        }
                    }
                }

                NavHost(
                    navController = navController,
                    startDestination = Destination.Home.route,
                    modifier = Modifier.weight(1f),
                    enterTransition = {
                        val direction = navDirection(
                            initialState.destination.route,
                            targetState.destination.route,
                        )
                        fadeIn(tween(220)) +
                            slideInHorizontally(tween(300)) { direction * it / 6 }
                    },
                    exitTransition = {
                        val direction = navDirection(
                            initialState.destination.route,
                            targetState.destination.route,
                        )
                        fadeOut(tween(180)) +
                            slideOutHorizontally(tween(260)) { -direction * it / 6 }
                    },
                    popEnterTransition = {
                        val direction = navDirection(
                            initialState.destination.route,
                            targetState.destination.route,
                        )
                        fadeIn(tween(220)) +
                            slideInHorizontally(tween(300)) { direction * it / 6 }
                    },
                    popExitTransition = {
                        val direction = navDirection(
                            initialState.destination.route,
                            targetState.destination.route,
                        )
                        fadeOut(tween(180)) +
                            slideOutHorizontally(tween(260)) { -direction * it / 6 }
                    },
                ) {
                    composable(Destination.Home.route) {
                        HomeScreen(
                            roomName = state.roomName,
                            roomCode = state.roomCode,
                            members = state.members,
                            selfId = state.selfId,
                            selfMode = state.selfMode,
                            currentTodo = state.currentTodo,
                            onCopyRoomCode = {
                                if (state.roomCode.isNotBlank()) {
                                    clipboard.setText(AnnotatedString(state.roomCode))
                                    scope.launch {
                                        snackbar.showSnackbar("房间码已复制：${state.roomCode}")
                                    }
                                }
                            },
                            onModeChange = roomViewModel::setMode,
                            onEditStatus = {
                                navController.navigate(Destination.SyncList.route)
                            },
                            onPoke = roomViewModel::sendPoke,
                            onTimerChanged = roomViewModel::refreshPresence,
                        )
                    }
                    composable(Destination.Focus.route) {
                        FocusScreen(onTimerChanged = roomViewModel::refreshPresence)
                    }
                    composable(Destination.Todo.route) { TodoScreen() }
                    composable(Destination.Room.route) {
                        RoomScreen(
                            roomName = state.roomName,
                            members = state.members,
                            selfId = state.selfId,
                            chat = state.chat,
                            logs = state.logs,
                            onSendChat = roomViewModel::sendChat,
                            onPoke = roomViewModel::sendPoke,
                        )
                    }
                    composable(Destination.Settings.route) {
                        SettingsScreen(
                            dynamicColor = dynamicColor,
                            onDynamicColorChange = onDynamicColorChange,
                            onOpenAppRules = { navController.navigate(Destination.Apps.route) },
                            serverUrl = state.serverUrl,
                            roomName = state.roomName,
                            displayName = state.displayName,
                            connection = state.status,
                            keepOnline = state.keepOnline,
                            overlayEnabled = state.overlayEnabled,
                            notifyRoomEvents = state.notifyRoomEvents,
                            notifyFocusDone = state.notifyFocusDone,
                            shareApp = state.shareApp,
                            shareMusic = state.shareMusic,
                            mediaWhitelistEnabled = state.mediaWhitelistEnabled,
                            mediaWhitelistPackages = state.mediaWhitelistPackages,
                            autoStart = state.autoStart,
                            autoConnect = state.autoConnect,
                            homeRoomCode = state.homeRoomCode,
                            homeRoomName = state.homeRoomName,
                            isServerRoom = state.isServerRoom,
                            overlayOpacity = state.overlayOpacity,
                            overlaySize = state.overlaySize,
                            overlayMargin = state.overlayMargin,
                            onKeepOnlineChange = roomViewModel::setKeepOnline,
                            onOverlayChange = { enabled ->
                                roomViewModel.setOverlayEnabled(enabled)
                                if (enabled &&
                                    Build.VERSION.SDK_INT >= Build.VERSION_CODES.M &&
                                    !Settings.canDrawOverlays(context)
                                ) {
                                    overlayLauncher.launch(
                                        Intent(
                                            Settings.ACTION_MANAGE_OVERLAY_PERMISSION,
                                            Uri.parse("package:${context.packageName}"),
                                        ),
                                    )
                                }
                            },
                            onNotifyRoomEventsChange = roomViewModel::setNotifyRoomEvents,
                            onNotifyFocusDoneChange = roomViewModel::setNotifyFocusDone,
                            onShareAppChange = roomViewModel::setShareApp,
                            onShareMusicChange = roomViewModel::setShareMusic,
                            onMediaWhitelistEnabledChange = roomViewModel::setMediaWhitelistEnabled,
                            onMediaWhitelistPackagesChange = roomViewModel::setMediaWhitelistPackages,
                            onAutoStartChange = roomViewModel::setAutoStart,
                            onAutoConnectChange = roomViewModel::setAutoConnect,
                            onSetHomeRoom = roomViewModel::setHomeToCurrentRoom,
                            onClearHomeRoom = roomViewModel::clearHomeRoom,
                            onOverlayOpacityChange = roomViewModel::setOverlayOpacity,
                            onOverlaySizeChange = roomViewModel::setOverlaySize,
                            onOverlayMarginChange = roomViewModel::setOverlayMargin,
                            onRequestNotifications = {
                                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
                                    ContextCompat.checkSelfPermission(
                                        context,
                                        Manifest.permission.POST_NOTIFICATIONS,
                                    ) != PackageManager.PERMISSION_GRANTED
                                ) {
                                    notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
                                }
                            },
                            onRequestOverlay = {
                                overlayLauncher.launch(
                                    Intent(
                                        Settings.ACTION_MANAGE_OVERLAY_PERMISSION,
                                        Uri.parse("package:${context.packageName}"),
                                    ),
                                )
                            },
                            onRequestUsageAccess = {
                                usageAccessLauncher.launch(Intent(Settings.ACTION_USAGE_ACCESS_SETTINGS))
                            },
                            onRequestNotificationAccess = {
                                mediaAccessLauncher.launch(
                                    Intent("android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS"),
                                )
                            },
                            onRequestBatteryOptimization = {
                                runCatching {
                                    context.startActivity(
                                        Intent(
                                            Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS,
                                            Uri.parse("package:${context.packageName}"),
                                        ),
                                    )
                                }.onFailure {
                                    runCatching {
                                        context.startActivity(
                                            Intent(Settings.ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS),
                                        )
                                    }
                                }
                            },
                            onOpenBackgroundSettings = {
                                openBackgroundSettings(context)
                            },
                            onDisconnect = roomViewModel::disconnect,
                        )
                    }
                    composable(Destination.Apps.route) {
                        AppRulesScreen(
                            onBack = { navController.popBackStack() },
                            onManageCategories = {
                                navController.navigate(Destination.Categories.route)
                            },
                        )
                    }
                    composable(Destination.Categories.route) {
                        CategoryManagerScreen(onBack = { navController.popBackStack() })
                    }
                    composable(Destination.SyncList.route) {
                        SyncListScreen(onBack = { navController.popBackStack() })
                    }
                }
            }
        }
    }
}

@Composable
private fun ConnectionPill(status: ConnectionStatus, onlineCount: Int) {
    val connected = status == ConnectionStatus.Connected
    val container = when (status) {
        ConnectionStatus.Connected -> MaterialTheme.colorScheme.primaryContainer
        ConnectionStatus.Error -> MaterialTheme.colorScheme.errorContainer
        ConnectionStatus.Connecting, ConnectionStatus.Reconnecting -> MaterialTheme.colorScheme.tertiaryContainer
        ConnectionStatus.Idle -> MaterialTheme.colorScheme.surfaceContainerHigh
    }
    val content = when (status) {
        ConnectionStatus.Connected -> MaterialTheme.colorScheme.onPrimaryContainer
        ConnectionStatus.Error -> MaterialTheme.colorScheme.onErrorContainer
        ConnectionStatus.Connecting, ConnectionStatus.Reconnecting -> MaterialTheme.colorScheme.onTertiaryContainer
        ConnectionStatus.Idle -> MaterialTheme.colorScheme.onSurfaceVariant
    }
    val label = when (status) {
        ConnectionStatus.Connected -> if (onlineCount > 0) "已连接 · $onlineCount 人" else "已连接"
        ConnectionStatus.Connecting -> "连接中"
        ConnectionStatus.Reconnecting -> "重连中"
        ConnectionStatus.Error -> "未连接"
        ConnectionStatus.Idle -> "未连接"
    }

    Surface(
        shape = MaterialTheme.shapes.extraLarge,
        color = container,
        contentColor = content,
        modifier = Modifier.padding(end = 12.dp),
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 10.dp, vertical = 5.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Surface(
                shape = MaterialTheme.shapes.extraLarge,
                color = if (connected) MaterialTheme.colorScheme.primary else content,
                modifier = Modifier
                    .padding(end = 6.dp)
                    .size(7.dp),
            ) {}
            Text(label, style = MaterialTheme.typography.labelMedium)
        }
    }
}
