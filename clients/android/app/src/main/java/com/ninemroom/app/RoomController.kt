package com.ninemroom.app

import android.content.Context
import androidx.core.content.ContextCompat
import com.ninemroom.app.data.AppSettings
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
import com.ninemroom.app.data.TodoStore
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
    val shareApp: Boolean = true,
    val shareMusic: Boolean = true,
    val autoStart: Boolean = true,
    val currentTodo: String = "",
) {
    val self: RoomMember?
        get() = members.firstOrNull { it.id == selfId }

    val onlineCount: Int
        get() = members.count { it.online }
}

class RoomController(
    context: Context,
    private val todoStore: TodoStore,
    private val focusTimer: FocusTimerController,
) {
    private val appContext = context.applicationContext
    private val settings = AppSettings(appContext)
    private val api = RoomApi()
    private val foregroundAppReader = ForegroundAppReader(appContext)
    private val mediaReader = MediaReader(appContext)
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private val _state = MutableStateFlow(
        RoomUiState(
            serverUrl = settings.serverUrl,
            roomCode = settings.roomCode,
            roomName = settings.roomName,
            displayName = settings.displayName,
            keepOnline = settings.keepOnline,
            overlayEnabled = settings.overlayEnabled,
            notifyRoomEvents = settings.notifyRoomEvents,
            shareApp = settings.shareApp,
            shareMusic = settings.shareMusic,
            autoStart = settings.autoStart,
            currentTodo = todoStore.current()?.title.orEmpty(),
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

        if (settings.serverUrl.isNotBlank() &&
            settings.roomCode.isNotBlank() &&
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
                    it.copy(serverUrl = base, roomName = info.name.ifBlank { info.code })
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

        if (current.shareApp) {
            val app = foregroundAppReader.current()
            val appId = app?.id ?: "android"
            val category = app?.category ?: "other"
            blocks.put(
                JSONObject()
                    .put("id", "app")
                    .put("type", "app")
                    .put("template", "📡 {app}")
                    .put(
                        "data",
                        JSONObject()
                            .put("appId", appId)
                            .put("category", category),
                    ),
            )
        }

        if (current.shareMusic) {
            val media = mediaReader.current()
            if (media != null) {
                val progress = if (media.durationMs > 0) {
                    "${formatDuration(media.positionMs)}/${formatDuration(media.durationMs)}"
                } else {
                    ""
                }
                blocks.put(
                    JSONObject()
                        .put("id", "media")
                        .put("type", "media")
                        .put("template", "🎶 {title} - {artist} {progress}")
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

        val todo = todoStore.current()?.title.orEmpty()
        if (todo.isNotBlank()) {
            blocks.put(
                JSONObject()
                    .put("id", "todo")
                    .put("type", "todo")
                    .put("template", "📚 {todo}")
                    .put(
                        "data",
                        JSONObject().put("text", todo),
                    ),
            )
        }

        val focus = focusTimer.state.value
        if (focus.running || focus.remainingSeconds < focus.durationSeconds) {
            val elapsedSeconds = (focus.durationSeconds - focus.remainingSeconds).coerceAtLeast(0)
            val startedAt = System.currentTimeMillis() - elapsedSeconds * 1000L
            blocks.put(
                JSONObject()
                    .put("id", "timer")
                    .put("type", "timer")
                    .put("template", "⏱ {remaining}")
                    .put(
                        "data",
                        JSONObject()
                            .put("mode", "focus")
                            .put("startedAt", startedAt)
                            .put("durationMinutes", focus.durationSeconds / 60),
                    ),
            )
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

    fun setAutoStart(enabled: Boolean) {
        settings.autoStart = enabled
        _state.update { it.copy(autoStart = enabled) }
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

    fun setNotifyRoomEvents(enabled: Boolean) {
        settings.notifyRoomEvents = enabled
        _state.update { it.copy(notifyRoomEvents = enabled) }
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
                }
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
