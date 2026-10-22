package com.ninemroom.app.data

import android.net.Uri
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.HttpUrl.Companion.toHttpUrl
import org.json.JSONObject
import java.util.concurrent.TimeUnit

object ServerAddress {
    fun normalize(input: String): String {
        var value = input.trim().trimEnd('/')
        if (value.isBlank()) return ""
        if (!value.startsWith("http://") && !value.startsWith("https://")) {
            value = "http://$value"
        }
        return value
    }

    fun webSocket(base: String): String = when {
        base.startsWith("https://") -> "wss://" + base.removePrefix("https://")
        base.startsWith("http://") -> "ws://" + base.removePrefix("http://")
        else -> "ws://$base"
    }
}

class RoomApi(
    private val client: OkHttpClient = OkHttpClient.Builder()
        .connectTimeout(8, TimeUnit.SECONDS)
        .readTimeout(8, TimeUnit.SECONDS)
        .build(),
) {
    val httpClient: OkHttpClient = client

    suspend fun checkHealth(base: String): Boolean = withContext(Dispatchers.IO) {
        val request = Request.Builder()
            .url("$base/healthz")
            .header("Accept", "application/json")
            .build()
        client.newCall(request).execute().use { response -> response.isSuccessful }
    }

    suspend fun roomInfo(base: String, code: String): RoomInfo = withContext(Dispatchers.IO) {
        val request = Request.Builder()
            .url("$base/api/rooms/${Uri.encode(code)}")
            .header("Accept", "application/json")
            .build()
        client.newCall(request).execute().use { response ->
            if (!response.isSuccessful) {
                throw IllegalStateException("房间不存在或服务器返回 ${response.code}")
            }
            val json = JSONObject(response.body?.string().orEmpty())
            RoomInfo(
                code = json.optString("code", code),
                name = json.optString("name"),
                exists = json.optBoolean("exists", false),
                online = json.optInt("online", 0),
                kind = json.optString("kind"),
            )
        }
    }

    fun webSocketUrl(base: String, room: String, name: String, clientId: String): String {
        val httpBase = ServerAddress.webSocket(base)
            .replaceFirst("ws://", "http://")
            .replaceFirst("wss://", "https://")
        val url = httpBase.toHttpUrl().newBuilder()
            .addPathSegment("v1")
            .addPathSegment("ws")
            .addQueryParameter("room", room)
            .addQueryParameter("name", name)
            .addQueryParameter("device", "android")
            .addQueryParameter("client", clientId)
            .addQueryParameter("role", "member")
            .build()
            .toString()
        return when {
            url.startsWith("https://") -> "wss://" + url.removePrefix("https://")
            else -> "ws://" + url.removePrefix("http://")
        }
    }
}
