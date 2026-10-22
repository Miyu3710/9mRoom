package com.ninemroom.app.data

import android.content.Context
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import org.json.JSONArray
import org.json.JSONObject
import java.util.UUID

data class AppCategory(
    val id: String,
    val name: String,
    val template: String,
    val builtIn: Boolean = false,
)

val defaultAppCategories = listOf(
    AppCategory("coding", "编程", "📡 {app}", builtIn = true),
    AppCategory("writing", "写作", "📡 {app}", builtIn = true),
    AppCategory("reading", "阅读", "📡 {app}", builtIn = true),
    AppCategory("meeting", "会议", "📡 {app}", builtIn = true),
    AppCategory("media", "影音", "📡 {app}", builtIn = true),
    AppCategory("game", "游戏", "📡 {app}", builtIn = true),
    AppCategory("browsing", "浏览", "📡 {app}", builtIn = true),
    AppCategory("other", "其他", "📡 {app}", builtIn = true),
)

class AppCategoryStore(context: Context) {
    private val preferences = context.applicationContext
        .getSharedPreferences("nine_room_categories", Context.MODE_PRIVATE)
    private val _categories = MutableStateFlow(load())
    val categories: StateFlow<List<AppCategory>> = _categories.asStateFlow()

    init {
        if (_categories.value.isEmpty()) {
            _categories.value = defaultAppCategories
            persist()
        }
    }

    fun find(id: String?): AppCategory? =
        _categories.value.firstOrNull { it.id == id }

    fun add(name: String, template: String): AppCategory? {
        val trimmed = name.trim().take(12)
        if (trimmed.isBlank()) return null
        val category = AppCategory(
            id = "custom-" + UUID.randomUUID().toString().take(8),
            name = trimmed,
            template = template.trim().ifBlank { "📡 {app}" },
            builtIn = false,
        )
        _categories.value = _categories.value + category
        persist()
        return category
    }

    fun update(id: String, name: String, template: String) {
        _categories.value = _categories.value.map {
            if (it.id == id) {
                it.copy(
                    name = name.trim().take(12).ifBlank { it.name },
                    template = template.trim().take(80).ifBlank { "📡 {app}" },
                )
            } else {
                it
            }
        }
        persist()
    }

    fun delete(id: String): Boolean {
        val category = find(id) ?: return false
        if (category.builtIn) return false
        _categories.value = _categories.value.filterNot { it.id == id }
        persist()
        return true
    }

    private fun persist() {
        val array = JSONArray()
        _categories.value.forEach { category ->
            array.put(
                JSONObject()
                    .put("id", category.id)
                    .put("name", category.name)
                    .put("template", category.template)
                    .put("builtIn", category.builtIn),
            )
        }
        preferences.edit().putString("categories", array.toString()).apply()
    }

    private fun load(): List<AppCategory> {
        val raw = preferences.getString("categories", "").orEmpty()
        if (raw.isBlank()) return emptyList()
        return try {
            val array = JSONArray(raw)
            buildList {
                for (index in 0 until array.length()) {
                    val json = array.optJSONObject(index) ?: continue
                    add(
                        AppCategory(
                            id = json.optString("id"),
                            name = json.optString("name"),
                            template = json.optString("template").takeUnless { it == "null" }
                                ?: "📡 {app}",
                            builtIn = json.optBoolean("builtIn"),
                        ),
                    )
                }
            }
        } catch (_: Exception) {
            emptyList()
        }
    }
}

val appCategoryLabels: Map<String, String>
    get() = defaultAppCategories.associate { it.id to it.name }
