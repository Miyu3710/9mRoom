package com.ninemroom.app.data

import android.content.Context
import java.util.UUID

class AppSettings(context: Context) {
    private val preferences = context.getSharedPreferences("nine_room", Context.MODE_PRIVATE)

    var serverUrl: String
        get() = preferences.getString("server_url", "").orEmpty()
        set(value) = preferences.edit().putString("server_url", value.trim()).apply()

    var roomCode: String
        get() = preferences.getString("room_code", "").orEmpty()
        set(value) = preferences.edit().putString("room_code", value.trim().lowercase()).apply()

    var roomName: String
        get() = preferences.getString("room_name", "").orEmpty()
        set(value) = preferences.edit().putString("room_name", value.trim()).apply()

    var displayName: String
        get() = preferences.getString("display_name", "").orEmpty()
        set(value) = preferences.edit().putString("display_name", value.trim()).apply()

    var keepOnline: Boolean
        get() = preferences.getBoolean("keep_online", true)
        set(value) = preferences.edit().putBoolean("keep_online", value).apply()

    var overlayEnabled: Boolean
        get() = preferences.getBoolean("overlay_enabled", true)
        set(value) = preferences.edit().putBoolean("overlay_enabled", value).apply()

    var notifyRoomEvents: Boolean
        get() = preferences.getBoolean("notify_room_events", true)
        set(value) = preferences.edit().putBoolean("notify_room_events", value).apply()

    val clientId: String
        get() {
            val existing = preferences.getString("client_id", "").orEmpty()
            if (existing.isNotBlank()) {
                return existing
            }

            val generated = UUID.randomUUID().toString().replace("-", "").take(16)
            preferences.edit().putString("client_id", generated).apply()
            return generated
        }

    fun saveConnection(server: String, room: String, name: String) {
        preferences.edit()
            .putString("server_url", server.trim())
            .putString("room_code", room.trim().lowercase())
            .putString("display_name", name.trim())
            .apply()
    }

    fun saveRoomName(name: String) {
        preferences.edit().putString("room_name", name.trim()).apply()
    }
}
