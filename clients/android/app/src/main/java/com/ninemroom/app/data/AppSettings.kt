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

    var notifyFocusDone: Boolean
        get() = preferences.getBoolean("notify_focus_done", true)
        set(value) = preferences.edit().putBoolean("notify_focus_done", value).apply()

    var dynamicColor: Boolean
        get() = preferences.getBoolean("dynamic_color", true)
        set(value) = preferences.edit().putBoolean("dynamic_color", value).apply()

    var shareApp: Boolean
        get() = preferences.getBoolean("share_app", true)
        set(value) = preferences.edit().putBoolean("share_app", value).apply()

    var shareMusic: Boolean
        get() = preferences.getBoolean("share_music", true)
        set(value) = preferences.edit().putBoolean("share_music", value).apply()

    var mediaWhitelistEnabled: Boolean
        get() = preferences.getBoolean("media_whitelist_enabled", false)
        set(value) = preferences.edit().putBoolean("media_whitelist_enabled", value).apply()

    var mediaWhitelistPackages: Set<String>
        get() = preferences.getStringSet("media_whitelist_packages", emptySet())
            .orEmpty()
            .toSet()
        set(value) = preferences.edit()
            .putStringSet("media_whitelist_packages", value)
            .apply()

    var autoStart: Boolean
        get() = preferences.getBoolean("auto_start", true)
        set(value) = preferences.edit().putBoolean("auto_start", value).apply()

    var autoConnect: Boolean
        get() = preferences.getBoolean("auto_connect", true)
        set(value) = preferences.edit().putBoolean("auto_connect", value).apply()

    var homeRoomCode: String
        get() = preferences.getString("home_room_code", "").orEmpty()
        set(value) = preferences.edit().putString("home_room_code", value.trim().lowercase()).apply()

    var homeRoomName: String
        get() = preferences.getString("home_room_name", "").orEmpty()
        set(value) = preferences.edit().putString("home_room_name", value.trim()).apply()

    var overlayOpacity: Float
        get() = preferences.getFloat("overlay_opacity", 0.9f).coerceIn(0.3f, 1f)
        set(value) = preferences.edit().putFloat("overlay_opacity", value.coerceIn(0.3f, 1f)).apply()

    var overlaySize: Int
        get() = preferences.getInt("overlay_size", 140).coerceIn(80, 180)
        set(value) = preferences.edit().putInt("overlay_size", value.coerceIn(80, 180)).apply()

    var overlayMargin: Int
        get() = preferences.getInt("overlay_margin", 8).coerceIn(4, 24)
        set(value) = preferences.edit().putInt("overlay_margin", value.coerceIn(4, 24)).apply()

    var overlayXFraction: Float
        get() = preferences.getFloat("overlay_x_fraction", -1f)
        set(value) = preferences.edit().putFloat("overlay_x_fraction", value).apply()

    var overlayYFraction: Float
        get() = preferences.getFloat("overlay_y_fraction", -1f)
        set(value) = preferences.edit().putFloat("overlay_y_fraction", value).apply()

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
