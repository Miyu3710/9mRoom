package com.ninemroom.app

import android.content.Context
import androidx.core.content.ContextCompat
import com.ninemroom.app.data.AppSettings
import com.ninemroom.app.data.AppCategoryStore
import com.ninemroom.app.data.AppRuleStore
import com.ninemroom.app.data.ChatLine
import com.ninemroom.app.data.ConnectionStatus
import com.ninemroom.app.data.FocusTimerController
import com.ninemroom.app.data.ForegroundAppReader
import com.ninemroom.app.data.LogLine
import com.ninemroom.app.data.MediaReader
import com.ninemroom.app.data.PresenceSocket
import com.ninemroom.app.data.RoomApi
import com.ninemroom.app.data.RoomInfo
import com.ninemroom.app.data.RoomMember
import com.ninemroom.app.data.ServerAddress
import com.ninemroom.app.data.StatusBlockRule
import com.ninemroom.app.data.StatusLayoutStore
import com.ninemroom.app.data.TodoStore
import com.ninemroom.app.data.defaultStatusTemplate
import com.ninemroom.app.data.nowTime
import com.ninemroom.app.data.parseMember
import com.ninemroom.app.data.parseMembers
import com.ninemroom.app.service.NotificationCenter
import com.ninemroom.app.service.PresenceService
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import org.json.JSONArray
import org.json.JSONObject

data class RoomUiState(
    val status: ConnectionStatus = ConnectionStatus.Idle,
    val serverUrl: String = "",
    val roomCode: String = "",
    val roomName: String = "",
    val displayName: String = "",
    val members: List<RoomMember> = emptyList(),
    val selfId: String? = null,
    val selfMode: String = "focusing",
    val chat: List<ChatLine> = emptyList(),
    val logs: List<LogLine> = emptyList(),
    val error: String? = null,
    val showMain: Boolean = false,
    val keepOnline: Boolean = true,
    val overlayEnabled: Boolean = true,
    val notifyRoomEvents: Boolean = true,
    val notifyFocusDone: Boolean = true,
    val shareApp: Boolean = true,
    val shareMusic: Boolean = true,
    val mediaWhitelistEnabled: Boolean = false,
    val mediaWhitelistPackages: Set<String> = emptySet(),
    val autoStart: Boolean = true,
    val autoConnect: Boolean = true,
    val homeRoomCode: String = "",
    val homeRoomName: String = "",
    val roomKind: String = "",
    val currentTodo: String = "",
    val overlayOpacity: Float = 0.9f,
    val overlaySize: Int = 140,
    val overlayMargin: Int = 8,
) {
    val isServerRoom: Boolean
        get() = roomKind == "server"
    val self: RoomMember?
        get() = members.firstOrNull { it.id == selfId }

    val onlineCount: Int
        get() = members.count { it.online }
}

class RoomController(
    context: Context,
    private val todoStore: TodoStore,
    private val appRules: AppRuleStore,
    private val appCategories: AppCategoryStore,
    private val statusLayout: StatusLayoutStore,
    private val focusTimer: FocusTimerController,
) {
    private val appContext = context.applicationContext
    private val settings = AppSettings(appContext)
    private val api = RoomApi()
    private val foregroundAppReader = ForegroundAppReader(appContext)
    private val mediaReader = MediaReader(appContext)
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private val initialRoom = if (settings.autoConnect && settings.homeRoomCode.isNotBlank()) {
        settings.homeRoomCode
    } else {
        settings.roomCode
    }
    private val initialRoomName = if (initialRoom == settings.homeRoomCode && settings.homeRoomCode.isNotBlank()) {
        settings.homeRoomName
    } else {
        settings.roomName
    }
    private val _state = MutableStateFlow(
        RoomUiState(
            serverUrl = settings.serverUrl,
            roomCode = initialRoom,
            roomName = initialRoomName,
            displayName = settings.displayName,
            keepOnline = settings.keepOnline,
            overlayEnabled = settings.overlayEnabled,
            notifyRoomEvents = settings.notifyRoomEvents,
            notifyFocusDone = settings.notifyFocusDone,
            shareApp = settings.shareApp,
            shareMusic = settings.shareMusic,
            mediaWhitelistEnabled = settings.mediaWhitelistEnabled,
            mediaWhitelistPackages = settings.mediaWhitelistPackages,
            autoStart = settings.autoStart,
            autoConnect = settings.autoConnect,
            homeRoomCode = settings.homeRoomCode,
            homeRoomName = settings.homeRoomName,
            currentTodo = todoStore.current()?.title.orEmpty(),
            overlayOpacity = settings.overlayOpacity,
            overlaySize = settings.overlaySize,
            overlayMargin = settings.overlayMargin,
        ),
    )
    val state: StateFlow<RoomUiState> = _state.asStateFlow()

    private var socket: PresenceSocket? = null
    private var heartbeatJob: Job? = null
    private var reconnectJob: Job? = null
    private var connecting = false
    private var intentionalClose = false
    private var opened = false

    init {
        scope.launch {
            todoStore.items.collectLatest { items ->
                val current = todoStore.current()?.title.orEmpty()
                _state.update { it.copy(currentTodo = current) }
                if (_state.value.status == ConnectionStatus.Connected) {
                    sendPresence()
                }
            }
        }

        scope.launch {
            appRules.rules.collectLatest {
                if (_state.value.status == ConnectionStatus.Connected) {
                    sendPresence()
                }
            }
        }

        scope.launch {
            appCategories.categories.collectLatest {
                if (_state.value.status == ConnectionStatus.Connected) {
                    sendPresence()
                }
            }
        }

        scope.launch {
            statusLayout.rules.collectLatest {
                if (_state.value.status == ConnectionStatus.Connected) {
                    sendPresence()
                }
            }
        }

        if (settings.autoConnect &&
            initialRoom.isNotBlank() &&
            settings.serverUrl.isNotBlank() &&
            settings.displayName.isNotBlank()
        ) {
            beginConnect()
        }
    }

    fun connect(server: String, room: String, name: String) {
        val base = ServerAddress.normalize(server)
        val roomCode = room.trim().lowercase()
        val displayName = name.trim()
        if (base.isBlank() || roomCode.isBlank() || displayName.isBlank()) {
            _state.update { it.copy(error = "服务器、房间码和昵称都不能为空") }
            return
        }
        if (base.startsWith("https://")) {
            _state.update { it.copy(error = "当前版本优先使用 HTTP 自建服务器；HTTPS 需要有效证书") }
        }

        settings.saveConnection(base, roomCode, displayName)
        _state.update {
            it.copy(
                serverUrl = base,
                roomCode = roomCode,
                displayName = displayName,
                status = ConnectionStatus.Connecting,
                error = null,
            )
        }
        beginConnect()
    }

    fun retry() {
        if (!connecting) beginConnect()
    }

    private fun beginConnect() {
        if (connecting) return
        val snapshot = _state.value
        if (snapshot.serverUrl.isBlank() || snapshot.roomCode.isBlank() || snapshot.displayName.isBlank()) {
            _state.update { it.copy(status = ConnectionStatus.Idle, error = "请先填写连接信息") }
            return
        }

        connecting = true
        intentionalClose = false
        reconnectJob?.cancel()
        _state.update { it.copy(status = ConnectionStatus.Connecting, error = null) }

        scope.launch {
            try {
                val base = ServerAddress.normalize(snapshot.serverUrl)
                if (!api.checkHealth(base)) {
                    throw IllegalStateException("服务器健康检查失败")
                }
                val info = api.roomInfo(base, snapshot.roomCode)
                if (!info.exists) {
                    throw IllegalStateException("房间 ${snapshot.roomCode} 不存在")
                }
                settings.saveRoomName(info.name)
                _state.update {
                    it.copy(
                        serverUrl = base,
                        roomName = info.name.ifBlank { info.code },
                        roomKind = info.kind,
                    )
                }
                openSocket(base, info)
            } catch (error: Exception) {
                connecting = false
                _state.update {
                    it.copy(
                        status = ConnectionStatus.Error,
                        error = error.message ?: "连接失败",
                    )
                }
            }
        }
    }

    private fun openSocket(base: String, info: RoomInfo) {
        socket?.close()
        opened = false
        val snapshot = _state.value
        socket = PresenceSocket(api.httpClient, object : PresenceSocket.Listener {
            override fun onOpen() {
                connecting = false
                opened = true
                intentionalClose = false
                _state.update {
                    it.copy(
                        status = ConnectionStatus.Connected,
                        showMain = true,
                        error = null,
                        roomName = info.name.ifBlank { info.code },
                        roomKind = info.kind,
                    )
                }
                startHeartbeat()
                sendPresence()
                startPresenceServiceIfNeeded()
            }

            override fun onMessage(message: JSONObject) {
                handleMessage(message)
            }

            override fun onFailure(message: String) {
                handleDisconnected(message)
            }

            override fun onClosed() {
                handleDisconnected("连接已断开")
            }
        })
        socket?.connect(
            api.webSocketUrl(
                base = base,
                room = snapshot.roomCode,
                name = snapshot.displayName,
                clientId = settings.clientId,
            ),
        )
    }

    private fun handleDisconnected(message: String) {
        if (intentionalClose) return
        connecting = false
        heartbeatJob?.cancel()
        heartbeatJob = null
        val current = _state.value
        if (current.showMain && opened) {
            _state.update {
                it.copy(status = ConnectionStatus.Reconnecting, error = message)
            }
            scheduleReconnect()
        } else {
            _state.update {
                it.copy(status = ConnectionStatus.Error, error = message, showMain = false)
            }
        }
    }

    private fun scheduleReconnect() {
        reconnectJob?.cancel()
        reconnectJob = scope.launch {
            delay(3000)
            beginConnect()
        }
    }

    private fun startHeartbeat() {
        heartbeatJob?.cancel()
        heartbeatJob = scope.launch {
            while (isActive) {
                sendPresence()
                delay(10_000)
            }
        }
    }

    private fun sendPresence() {
        socket?.sendPresence(
            mode = _state.value.selfMode,
            blocks = buildStatusBlocks(),
            focusSecondsToday = focusTimer.focusSecondsToday(),
        )
    }

    private fun buildStatusBlocks(): JSONArray {
        val blocks = JSONArray()
        val current = _state.value
        val app = if (current.shareApp) foregroundAppReader.current() else null
        val appRule = app?.let { appRules.ruleFor(it.packageName) }
        val media = if (current.shareMusic) mediaReader.current() else null
        val todo = todoStore.current()?.title.orEmpty()
        val focus = focusTimer.state.value
        val focusActive = focus.running || focus.remainingSeconds < focus.durationSeconds

        statusLayout.rules.value.filter { it.enabled }.forEach { rule ->
            when (rule.type) {
                "app" -> {
                    if (current.shareApp && app != null && appRule?.hidden != true) {
                        val categoryId = appRule?.category ?: app.category ?: "other"
                        val categoryTemplate = appCategories.find(categoryId)?.template
                            ?.takeIf { it.isNotBlank() }
                        val appTemplate = appRule?.template?.takeUnless { it == "null" }
                            ?.takeIf { it.isNotBlank() }
                            ?: categoryTemplate
                            ?: rule.template.takeUnless { it.isBlank() || it == "null" }
                            ?: defaultStatusTemplate("app")
                        blocks.put(
                            JSONObject()
                                .put("id", rule.id)
                                .put("type", "app")
                                .put("template", appTemplate.ifBlank { defaultStatusTemplate("app") })
                                .put(
                                    "data",
                                    JSONObject()
                                        .put("appId", app.id)
                                        .put("category", categoryId)
                                        .put(
                                            "name",
                                            appRule?.displayName?.takeUnless { it == "null" }
                                                ?: app.name,
                                        ),
                                ),
                        )
                    }
                }

                "media" -> {
                    if (media != null) {
                        val progress = if (media.durationMs > 0) {
                            "${formatDuration(media.positionMs)}/${formatDuration(media.durationMs)}"
                        } else {
                            ""
                        }
                        blocks.put(
                            JSONObject()
                                .put("id", rule.id)
                                .put("type", "media")
                                .put("template", rule.template.ifBlank { defaultStatusTemplate("media") })
                                .put(
                                    "data",
                                    JSONObject()
                                        .put("title", media.title)
                                        .put("artist", media.artist)
                                        .put("isPlaying", media.isPlaying)
                                        .put("positionMs", media.positionMs)
                                        .put("durationMs", media.durationMs)
                                        .put("sampledAt", System.currentTimeMillis())
                                        .put("progress", progress),
                                ),
                        )
                    }
                }

                "todo" -> {
                    if (todo.isNotBlank()) {
                        blocks.put(
                            JSONObject()
                                .put("id", rule.id)
                                .put("type", "todo")
                                .put("template", rule.template.ifBlank { defaultStatusTemplate("todo") })
                                .put("data", JSONObject().put("text", todo)),
                        )
                    }
                }

                "timer" -> {
                    if (focusActive) {
                        val elapsedSeconds = (focus.durationSeconds - focus.remainingSeconds).coerceAtLeast(0)
                        val startedAt = System.currentTimeMillis() - elapsedSeconds * 1000L
                        blocks.put(
                            JSONObject()
                                .put("id", rule.id)
                                .put("type", "timer")
                                .put("template", rule.template.ifBlank { defaultStatusTemplate("timer") })
                                .put(
                                    "data",
                                    JSONObject()
                                        .put("mode", "focus")
                                        .put("startedAt", startedAt)
                                        .put("durationMinutes", focus.durationSeconds / 60),
                                ),
                        )
                    }
                }

                "text" -> {
                    if (rule.template.isNotBlank()) {
                        blocks.put(
                            JSONObject()
                                .put("id", rule.id)
                                .put("type", "text")
                                .put("template", rule.template)
                                .put("data", JSONObject().put("text", rule.template)),
                        )
                    }
                }
            }
        }

        return blocks
    }

    private fun formatDuration(milliseconds: Long): String {
        val totalSeconds = (milliseconds / 1000).coerceAtLeast(0)
        return "%d:%02d".format(totalSeconds / 60, totalSeconds % 60)
    }

    fun setShareApp(enabled: Boolean) {
        settings.shareApp = enabled
        _state.update { it.copy(shareApp = enabled) }
        sendPresence()
    }

    fun setShareMusic(enabled: Boolean) {
        settings.shareMusic = enabled
        _state.update { it.copy(shareMusic = enabled) }
        sendPresence()
    }

    fun setMediaWhitelistEnabled(enabled: Boolean) {
        settings.mediaWhitelistEnabled = enabled
        _state.update { it.copy(mediaWhitelistEnabled = enabled) }
        sendPresence()
    }

    fun setMediaWhitelistPackages(packages: Set<String>) {
        val normalized = packages
            .map { it.trim() }
            .filter { it.isNotBlank() }
            .toSet()
        settings.mediaWhitelistPackages = normalized
        _state.update { it.copy(mediaWhitelistPackages = normalized) }
        sendPresence()
    }

    fun setAutoStart(enabled: Boolean) {
        settings.autoStart = enabled
        _state.update { it.copy(autoStart = enabled) }
    }

    fun setAutoConnect(enabled: Boolean) {
        settings.autoConnect = enabled
        _state.update { it.copy(autoConnect = enabled) }
        if (enabled &&
            !_state.value.showMain &&
            _state.value.serverUrl.isNotBlank() &&
            _state.value.roomCode.isNotBlank() &&
            _state.value.displayName.isNotBlank()
        ) {
            beginConnect()
        }
    }

    fun setHomeToCurrentRoom() {
        val current = _state.value
        if (current.roomCode.isBlank() || !current.isServerRoom) return
        settings.homeRoomCode = current.roomCode
        settings.homeRoomName = current.roomName
        _state.update {
            it.copy(
                homeRoomCode = current.roomCode,
                homeRoomName = current.roomName,
            )
        }
    }

    fun clearHomeRoom() {
        settings.homeRoomCode = ""
        settings.homeRoomName = ""
        _state.update { it.copy(homeRoomCode = "", homeRoomName = "") }
    }

    fun refreshPresence() {
        sendPresence()
    }

    fun setMode(mode: String) {
        if (_state.value.selfMode == mode) return
        _state.update { it.copy(selfMode = mode) }
        sendPresence()
    }

    fun sendChat(text: String) {
        val message = text.trim()
        if (message.isBlank() || message.length > 500) return
        socket?.sendChat(message)
    }

    fun sendPoke(targetId: String) {
        val id = targetId.trim()
        if (id.isBlank() || id == _state.value.selfId) return
        socket?.sendPoke(id)
    }

    fun setKeepOnline(enabled: Boolean) {
        settings.keepOnline = enabled
        _state.update { it.copy(keepOnline = enabled) }
        refreshServiceState()
    }

    fun setOverlayEnabled(enabled: Boolean) {
        settings.overlayEnabled = enabled
        _state.update { it.copy(overlayEnabled = enabled) }
        refreshServiceState()
    }

    fun setOverlayOpacity(value: Float) {
        val opacity = value.coerceIn(0.3f, 1f)
        settings.overlayOpacity = opacity
        _state.update { it.copy(overlayOpacity = opacity) }
    }

    fun setOverlaySize(value: Int) {
        val size = value.coerceIn(80, 180)
        settings.overlaySize = size
        _state.update { it.copy(overlaySize = size) }
    }

    fun setOverlayMargin(value: Int) {
        val margin = value.coerceIn(4, 24)
        settings.overlayMargin = margin
        _state.update { it.copy(overlayMargin = margin) }
    }

    fun setNotifyRoomEvents(enabled: Boolean) {
        settings.notifyRoomEvents = enabled
        _state.update { it.copy(notifyRoomEvents = enabled) }
    }

    fun setNotifyFocusDone(enabled: Boolean) {
        settings.notifyFocusDone = enabled
        _state.update { it.copy(notifyFocusDone = enabled) }
    }

    fun refreshOverlay() {
        if (_state.value.status == ConnectionStatus.Connected) {
            startPresenceService()
        }
    }

    private fun refreshServiceState() {
        val current = _state.value
        if (current.status != ConnectionStatus.Connected) {
            stopPresenceService()
            return
        }
        if (current.keepOnline || current.overlayEnabled) {
            startPresenceService()
        } else {
            stopPresenceService()
        }
    }

    private fun startPresenceServiceIfNeeded() {
        val current = _state.value
        if (current.keepOnline || current.overlayEnabled) {
            startPresenceService()
        }
    }

    private fun startPresenceService() {
        try {
            ContextCompat.startForegroundService(
                appContext,
                PresenceService.intent(appContext),
            )
        } catch (_: Exception) {
            // Android 12+ 在后台启动前台服务可能被系统拒绝；下次回到前台再试
        }
    }

    private fun stopPresenceService() {
        try {
            appContext.stopService(PresenceService.intent(appContext))
        } catch (_: Exception) {
            // 忽略
        }
    }

    fun disconnect() {
        intentionalClose = true
        connecting = false
        reconnectJob?.cancel()
        heartbeatJob?.cancel()
        heartbeatJob = null
        socket?.close()
        socket = null
        opened = false
        stopPresenceService()
        _state.update {
            it.copy(
                status = ConnectionStatus.Idle,
                showMain = false,
                members = emptyList(),
                selfId = null,
                chat = emptyList(),
                logs = emptyList(),
                error = null,
            )
        }
    }

    private fun handleMessage(message: JSONObject) {
        when (message.optString("type")) {
            "welcome" -> {
                val id = message.optJSONObject("payload")?.optString("userId").orEmpty()
                if (id.isNotBlank()) {
                    _state.update { it.copy(selfId = id) }
                }
            }

            "room.snapshot" -> {
                val payload = message.optJSONObject("payload") ?: return
                val members = parseMembers(payload.optJSONArray("members"))
                _state.update { it.copy(members = members) }
            }

            "presence.changed" -> {
                val member = message.optJSONObject("payload")?.optJSONObject("member") ?: return
                upsertMember(member)
            }

            "member.joined" -> {
                val json = message.optJSONObject("payload")?.optJSONObject("member") ?: return
                val member = parseMember(json) ?: return
                upsertMember(json)
                if (member.id != _state.value.selfId) {
                    addLog("${member.name} 加入了房间")
                    notifyRoomEvent("${member.name} 进入了房间")
                }
            }

            "member.left" -> {
                val id = message.optJSONObject("payload")?.optString("id").orEmpty()
                if (id.isBlank()) return
                val name = _state.value.members.firstOrNull { it.id == id }?.name
                _state.update { state -> state.copy(members = state.members.filterNot { it.id == id }) }
                if (name != null && id != _state.value.selfId) {
                    addLog("$name 离开了房间")
                    notifyRoomEvent("$name 离开了房间")
                }
            }

            "chat.message" -> {
                val payload = message.optJSONObject("payload") ?: return
                val line = ChatLine(
                    id = payload.optString("id"),
                    memberId = payload.optString("memberId"),
                    author = payload.optString("name").ifBlank { "匿名" },
                    device = payload.optString("device"),
                    text = payload.optString("text"),
                    at = payload.optLong("at", System.currentTimeMillis()),
                )
                if (line.text.isNotBlank()) {
                    _state.update { it.copy(chat = (it.chat + line).takeLast(200)) }
                    if (line.memberId != _state.value.selfId) {
                        NotificationCenter.chat(
                            appContext,
                            author = line.author,
                            text = line.text,
                        )
                    }
                }
            }

            "poke.received" -> {
                val payload = message.optJSONObject("payload") ?: return
                val name = payload.optJSONObject("from")
                    ?.optString("name")
                    .orEmpty()
                    .ifBlank { "有人" }
                addLog("$name 戳了你一下！")
                NotificationCenter.poke(appContext, name)
            }

            "poke.sent" -> {
                val payload = message.optJSONObject("payload") ?: return
                val target = payload.optString("targetName").ifBlank { "对方" }
                addLog("你戳了 $target 一下")
            }

            "error" -> {
                val text = message.optJSONObject("payload")?.optString("message").orEmpty()
                if (text.isNotBlank()) {
                    _state.update { it.copy(error = text) }
                }
            }
        }
    }

    private fun upsertMember(json: JSONObject) {
        val member = parseMember(json) ?: return
        _state.update { state ->
            val members = (state.members.filterNot { it.id == member.id } + member)
                .sortedWith(compareByDescending<RoomMember> { it.online }.thenBy { it.name })
            state.copy(
                members = members,
                selfMode = if (member.id == state.selfId) member.mode else state.selfMode,
            )
        }
    }

    private fun notifyRoomEvent(text: String) {
        if (!_state.value.notifyRoomEvents) return
        NotificationCenter.roomEvent(appContext, text)
    }

    private fun addLog(text: String) {
        _state.update { it.copy(logs = (it.logs + LogLine(nowTime(), text)).takeLast(100)) }
    }
}
