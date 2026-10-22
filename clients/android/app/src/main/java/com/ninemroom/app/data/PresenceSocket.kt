package com.ninemroom.app.data

import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import org.json.JSONArray
import org.json.JSONObject

class PresenceSocket(
    private val client: OkHttpClient,
    private val listener: Listener,
) {
    interface Listener {
        fun onOpen()
        fun onMessage(message: JSONObject)
        fun onFailure(message: String)
        fun onClosed()
    }

    private var socket: WebSocket? = null

    fun connect(url: String) {
        socket?.cancel()
        val request = Request.Builder().url(url).build()
        socket = client.newWebSocket(request, object : WebSocketListener() {
            override fun onOpen(webSocket: WebSocket, response: Response) {
                listener.onOpen()
            }

            override fun onMessage(webSocket: WebSocket, text: String) {
                try {
                    listener.onMessage(JSONObject(text))
                } catch (_: Exception) {
                    // 忽略无法解析的消息
                }
            }

            override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                listener.onFailure(t.message ?: "连接已断开")
            }

            override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                listener.onClosed()
            }
        })
    }

    fun sendPresence(mode: String, blocks: JSONArray, focusSecondsToday: Long = 0) {
        val payload = JSONObject()
            .put("mode", mode)
            .put("focusSecondsToday", focusSecondsToday)
            .put(
                "status",
                JSONObject()
                    .put("layout", "inline")
                    .put("separator", " | ")
                    .put("blocks", blocks),
            )
        sendEnvelope("presence.update", payload)
    }

    fun sendChat(text: String) {
        sendEnvelope("chat.send", JSONObject().put("text", text))
    }

    fun sendPoke(targetId: String) {
        sendEnvelope("poke.send", JSONObject().put("targetId", targetId))
    }

    private fun sendEnvelope(type: String, payload: JSONObject) {
        val envelope = JSONObject()
            .put("v", 1)
            .put("type", type)
            .put("id", java.util.UUID.randomUUID().toString().replace("-", "").take(12))
            .put("ts", System.currentTimeMillis())
            .put("payload", payload)
        socket?.send(envelope.toString())
    }

    fun close() {
        socket?.close(1000, "bye")
        socket = null
    }
}

fun androidStatusBlocks(): JSONArray = JSONArray().put(
    JSONObject()
        .put("id", "app")
        .put("type", "app")
        .put("template", "📱 {app}")
        .put(
            "data",
            JSONObject()
                .put("appId", "android")
                .put("category", "other"),
        ),
)
