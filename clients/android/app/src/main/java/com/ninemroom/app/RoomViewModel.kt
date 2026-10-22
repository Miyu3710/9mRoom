package com.ninemroom.app

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.ninemroom.app.data.AppSettings
import com.ninemroom.app.data.ChatLine
import com.ninemroom.app.data.ConnectionStatus
import com.ninemroom.app.data.LogLine
import com.ninemroom.app.data.PresenceSocket
import com.ninemroom.app.data.RoomApi
import com.ninemroom.app.data.RoomInfo
import com.ninemroom.app.data.RoomMember
import com.ninemroom.app.data.ServerAddress
import com.ninemroom.app.data.androidStatusBlocks
import com.ninemroom.app.data.nowTime
import com.ninemroom.app.data.parseMember
import com.ninemroom.app.data.parseMembers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
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
) {
    val self: RoomMember?
        get() = members.firstOrNull { it.id == selfId }

    val onlineCount: Int
        get() = members.count { it.online }
}

class RoomViewModel(application: Application) : AndroidViewModel(application) {
    private val settings = AppSettings(application)
    private val api = RoomApi()
    private val _state = MutableStateFlow(
        RoomUiState(
            serverUrl = settings.serverUrl,
            roomCode = settings.roomCode,
            roomName = settings.roomName,
            displayName = settings.displayName,
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
        if (connecting) return
        beginConnect()
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

        viewModelScope.launch {
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
                    )
                }
                startHeartbeat()
                sendPresence()
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
                it.copy(
                    status = ConnectionStatus.Reconnecting,
                    error = message,
                )
            }
            scheduleReconnect()
        } else {
            _state.update {
                it.copy(
                    status = ConnectionStatus.Error,
                    error = message,
                    showMain = false,
                )
            }
        }
    }

    private fun scheduleReconnect() {
        reconnectJob?.cancel()
        reconnectJob = viewModelScope.launch {
            delay(3000)
            beginConnect()
        }
    }

    private fun startHeartbeat() {
        heartbeatJob?.cancel()
        heartbeatJob = viewModelScope.launch {
            while (isActive) {
                sendPresence()
                delay(20_000)
            }
        }
    }

    private fun sendPresence() {
        socket?.sendPresence(_state.value.selfMode, androidStatusBlocks())
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

    fun disconnect() {
        intentionalClose = true
        connecting = false
        reconnectJob?.cancel()
        heartbeatJob?.cancel()
        heartbeatJob = null
        socket?.close()
        socket = null
        opened = false
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
                val payload = message.optJSONObject("payload") ?: return
                val id = payload.optString("userId")
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
                val member = message.optJSONObject("payload")?.optJSONObject("member") ?: return
                upsertMember(member)
                val parsed = parseMember(member) ?: return
                if (parsed.id != _state.value.selfId) {
                    addLog("${parsed.name} 加入了房间")
                }
            }

            "member.left" -> {
                val id = message.optJSONObject("payload")?.optString("id").orEmpty()
                if (id.isBlank()) return
                val name = _state.value.members.firstOrNull { it.id == id }?.name
                _state.update { state -> state.copy(members = state.members.filterNot { it.id == id }) }
                if (name != null && id != _state.value.selfId) {
                    addLog("$name 离开了房间")
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
                    _state.update {
                        it.copy(chat = (it.chat + line).takeLast(200))
                    }
                }
            }

            "error" -> {
                val payload = message.optJSONObject("payload")
                val text = payload?.optString("message").orEmpty()
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

    private fun addLog(text: String) {
        _state.update {
            it.copy(logs = (it.logs + LogLine(nowTime(), text)).takeLast(100))
        }
    }

    override fun onCleared() {
        intentionalClose = true
        socket?.close()
        heartbeatJob?.cancel()
        reconnectJob?.cancel()
        super.onCleared()
    }
}
