package com.ninemroom.app.data

import android.content.Context
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import org.json.JSONArray
import org.json.JSONObject
import java.util.UUID

data class StatusBlockRule(
    val id: String,
    val type: String,
    val template: String,
    val enabled: Boolean = true,
)

val statusBlockTypes = linkedMapOf(
    "app" to "应用",
    "media" to "音乐",
    "todo" to "待办",
    "timer" to "计时",
    "text" to "自定义",
)

fun defaultStatusTemplate(type: String): String = when (type) {
    "app" -> "📡 {app}"
    "media" -> "🎶 {title} - {artist} {progress}"
    "todo" -> "📚 {todo}"
    "timer" -> "⏱ {remaining}"
    else -> "✏️ 自定义"
}

class StatusLayoutStore(context: Context) {
    private val preferences = context.applicationContext
        .getSharedPreferences("nine_room_status_layout", Context.MODE_PRIVATE)
    private val _rules = MutableStateFlow(load())
    val rules: StateFlow<List<StatusBlockRule>> = _rules.asStateFlow()

    init {
        if (!preferences.getBoolean("seeded", false)) {
            preferences.edit().putBoolean("seeded", true).apply()
            _rules.value = listOf(
                StatusBlockRule("app", "app", defaultStatusTemplate("app")),
                StatusBlockRule("media", "media", defaultStatusTemplate("media")),
                StatusBlockRule("timer", "timer", defaultStatusTemplate("timer")),
                StatusBlockRule("todo", "todo", defaultStatusTemplate("todo")),
            )
            persist()
        }
    }

    fun add(type: String): String {
        val rule = StatusBlockRule(
            id = UUID.randomUUID().toString().take(8),
            type = type,
            template = defaultStatusTemplate(type),
        )
        _rules.value = _rules.value + rule
        persist()
        return rule.id
    }

    fun remove(id: String) {
        _rules.value = _rules.value.filterNot { it.id == id }
        persist()
    }

    fun move(from: Int, to: Int) {
        if (from == to) return
        val items = _rules.value.toMutableList()
        if (from !in items.indices || to !in items.indices) return
        val item = items.removeAt(from)
        items.add(to, item)
        _rules.value = items
        persist()
    }

    fun updateTemplate(id: String, template: String) {
        _rules.value = _rules.value.map {
            if (it.id == id) it.copy(template = template.take(160)) else it
        }
        persist()
    }

    fun setEnabled(id: String, enabled: Boolean) {
        _rules.value = _rules.value.map {
            if (it.id == id) it.copy(enabled = enabled) else it
        }
        persist()
    }

    private fun persist() {
        val array = JSONArray()
        _rules.value.forEach { rule ->
            array.put(
                JSONObject()
                    .put("id", rule.id)
                    .put("type", rule.type)
                    .put("template", rule.template)
                    .put("enabled", rule.enabled),
            )
        }
        preferences.edit().putString("rules", array.toString()).apply()
    }

    private fun load(): List<StatusBlockRule> {
        val raw = preferences.getString("rules", "").orEmpty()
        if (raw.isBlank()) return emptyList()
        return try {
            val array = JSONArray(raw)
            buildList {
                for (index in 0 until array.length()) {
                    val json = array.optJSONObject(index) ?: continue
                    add(
                        StatusBlockRule(
                            id = json.optString("id"),
                            type = json.optString("type"),
                            template = json.optString("template"),
                            enabled = json.optBoolean("enabled", true),
                        ),
                    )
                }
            }
        } catch (_: Exception) {
            emptyList()
        }
    }
}
