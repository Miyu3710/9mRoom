package com.ninemroom.app.data

import org.json.JSONArray
import org.json.JSONObject
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import kotlin.math.max

enum class ConnectionStatus {
    Idle,
    Connecting,
    Connected,
    Reconnecting,
    Error,
}

data class RoomInfo(
    val code: String,
    val name: String,
    val exists: Boolean,
    val online: Int,
    val kind: String = "",
)

data class AvatarData(
    val mime: String,
    val hash: String,
    val width: Int,
    val height: Int,
    val data: String,
)

data class RoomMember(
    val id: String,
    val name: String,
    val device: String,
    val role: String,
    val online: Boolean,
    val mode: String,
    val updatedAt: Long,
    val statusText: String,
    val focusSecondsToday: Long,
    val connectionState: String = "online",
    val avatar: AvatarData? = null,
    val avatarHash: String? = null,
) {
    val initial: String
        get() = name.trim().take(1).ifBlank { "?" }

    val modeLabel: String
        get() = modeLabel(mode)

    val isReconnecting: Boolean
        get() = connectionState == "reconnecting"

    val connectionLabel: String?
        get() = when {
            isReconnecting -> "重连中"
            !online -> "离线"
            else -> null
        }

    val agoText: String
        get() = agoText(updatedAt)

    val deviceLabel: String
        get() = when (device.lowercase()) {
            "android" -> "Android"
            "windows" -> "Windows"
            "linux" -> "Linux"
            else -> device.ifBlank { "未知设备" }
        }
}

data class ChatLine(
    val id: String,
    val memberId: String,
    val author: String,
    val device: String,
    val text: String,
    val at: Long,
)

data class LogLine(
    val time: String,
    val text: String,
)

fun modeLabel(mode: String): String = when (mode) {
    "focusing" -> "专注中"
    "resting" -> "休息中"
    "away" -> "离开"
    "invisible" -> "隐身"
    "offline" -> "离线"
    "auto" -> "自动"
    else -> mode.ifBlank { "在线" }
}

fun agoText(updatedAt: Long): String {
    if (updatedAt <= 0) return "未知"
    val seconds = max(0, (System.currentTimeMillis() - updatedAt) / 1000)
    return when {
        seconds < 60 -> "刚刚"
        seconds < 3600 -> "${seconds / 60} 分钟前"
        seconds < 86400 -> "${seconds / 3600} 小时前"
        else -> "${seconds / 86400} 天前"
    }
}

fun parseMember(json: JSONObject): RoomMember? {
    val id = json.optString("id")
    if (id.isBlank()) return null
    return RoomMember(
        id = id,
        name = json.optString("name").ifBlank { "匿名" },
        device = json.optString("device"),
        role = json.optString("role"),
        online = json.optBoolean("online", true),
        mode = json.optString("mode", "focusing"),
        updatedAt = json.optLong("updatedAt", 0),
        statusText = renderStatus(json.optJSONObject("status")),
        focusSecondsToday = json.optLong("focusSecondsToday", 0),
        connectionState = json.optString(
            "connectionState",
            if (json.optBoolean("online", true)) "online" else "offline",
        ),
        avatar = parseAvatar(json.optJSONObject("avatar")),
        avatarHash = json.optString("avatarHash").takeIf { it.isNotBlank() && it != "null" },
    )
}

fun parseAvatar(json: JSONObject?): AvatarData? {
    if (json == null) return null
    val data = json.optString("data")
    if (data.isBlank()) return null
    return AvatarData(
        mime = json.optString("mime").ifBlank { "image/jpeg" },
        hash = json.optString("hash"),
        width = json.optInt("width", 0),
        height = json.optInt("height", 0),
        data = data,
    )
}

fun parseMembers(array: JSONArray?): List<RoomMember> {
    if (array == null) return emptyList()
    return buildList {
        for (index in 0 until array.length()) {
            val item = array.optJSONObject(index) ?: continue
            parseMember(item)?.let(::add)
        }
    }.sortedWith(compareByDescending<RoomMember> { it.online }.thenBy { it.name })
}

fun renderStatus(status: JSONObject?): String {
    if (status == null) return ""
    val blocks = status.optJSONArray("blocks") ?: return ""
    val separator = status.optString("separator", " | ")
    val rendered = buildList {
        for (index in 0 until blocks.length()) {
            val block = blocks.optJSONObject(index) ?: continue
            val text = renderBlock(block)
            if (text.isNotBlank()) add(text)
        }
    }
    return rendered.joinToString(separator)
}

private fun renderBlock(block: JSONObject): String {
    val type = block.optString("type")
    val data = block.optJSONObject("data") ?: JSONObject()
    val template = block.optString("template").takeUnless { it == "null" }.orEmpty()
    val appId = data.optString("appId")
    val customAppName = data.optString("name")
        .takeUnless { it == "null" || it.isBlank() }
        ?: appName(appId)
    val title = data.optString("title")
    val artist = data.optString("artist")
    val progress = mediaProgress(data)
    val remaining = timerRemaining(data)
    val todo = data.optString("text")
    val value = when (type) {
        "app" -> customAppName
        "media" -> listOf(title, artist).filter { it.isNotBlank() }.joinToString(" - ")
        "timer" -> remaining
        "todo" -> todo
        "text" -> data.optString("text")
        else -> ""
    }
    if (value.isBlank()) return ""
    if (template.isBlank()) return value
    return template
        .replace("{app}", customAppName)
        .replace("{title}", title)
        .replace("{artist}", artist)
        .replace("{progress}", progress)
        .replace("{remaining}", remaining)
        .replace("{todo}", todo)
        .replace("{text}", data.optString("text"))
        .trim()
}

private fun appName(appId: String): String = when (appId.lowercase()) {
    "vscode", "visual studio code" -> "VS Code"
    "chrome" -> "Chrome"
    "android" -> "Android"
    "9mroom" -> "9mRoom"
    "explorer" -> "文件管理器"
    else -> appId
}

private fun mediaProgress(data: JSONObject): String {
    val duration = data.optLong("durationMs", 0)
    if (duration <= 0) return ""
    val position = data.optLong("positionMs", 0)
    return "${formatDuration(position)}/${formatDuration(duration)}"
}

private fun formatDuration(milliseconds: Long): String {
    val totalSeconds = max(0, milliseconds / 1000)
    return "%d:%02d".format(totalSeconds / 60, totalSeconds % 60)
}

private fun timerRemaining(data: JSONObject): String {
    val duration = data.optInt("durationMinutes", 25) * 60L
    val startedAt = data.optLong("startedAt", 0)
    if (startedAt <= 0) return ""
    val elapsed = max(0, (System.currentTimeMillis() - startedAt) / 1000)
    val remaining = max(0, duration - elapsed)
    return "%d:%02d".format(remaining / 60, remaining % 60)
}

fun chatTime(at: Long): String {
    if (at <= 0) return ""
    return Instant.ofEpochMilli(at)
        .atZone(ZoneId.systemDefault())
        .format(DateTimeFormatter.ofPattern("HH:mm"))
}

fun nowTime(): String =
    Instant.now().atZone(ZoneId.systemDefault()).format(DateTimeFormatter.ofPattern("HH:mm:ss"))
