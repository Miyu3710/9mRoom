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
    AppCategory("coding", "编程", "", builtIn = true),
    AppCategory("writing", "写作", "", builtIn = true),
    AppCategory("reading", "阅读", "", builtIn = true),
    AppCategory("meeting", "会议", "", builtIn = true),
    AppCategory("media", "影音", "", builtIn = true),
    AppCategory("game", "游戏", "", builtIn = true),
    AppCategory("browsing", "浏览", "", builtIn = true),
    AppCategory("other", "其他", "", builtIn = true),
)

class AppCategoryStore(context: Context) {
    private val preferences = context.applicationContext
        .getSharedPreferences("nine_room_categories", Context.MODE_PRIVATE)
    private val _categories = MutableStateFlow(load())
    val categories: StateFlow<List<AppCategory>> = _categories.asStateFlow()

    init {
        val initialized = preferences.getBoolean("initialized", false) ||
            !preferences.getString("categories", "").isNullOrBlank()
        if (!initialized) {
            _categories.value = defaultAppCategories
            persist()
            preferences.edit().putBoolean("initialized", true).apply()
        } else {
            if (!preferences.getBoolean("category_templates_cleared", false)) {
                _categories.value = _categories.value.map { category ->
                    if (category.builtIn && category.template == "📡 {app}") {
                        category.copy(template = "")
                    } else {
                        category
                    }
                }
                preferences.edit().putBoolean("category_templates_cleared", true).apply()
                persist()
            }
            preferences.edit().putBoolean("initialized", true).apply()
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
            template = template.trim().take(80),
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
                    template = template.trim().take(80),
                )
            } else {
                it
            }
        }
        persist()
    }

    fun delete(id: String): Boolean {
        if (find(id) == null) return false
        _categories.value = _categories.value.filterNot { it.id == id }
        persist()
        return true
    }

    fun move(from: Int, to: Int) {
        if (from == to) return
        val current = _categories.value.toMutableList()
        if (from !in current.indices || to !in current.indices) return
        val item = current.removeAt(from)
        current.add(to, item)
        _categories.value = current
        persist()
    }

    fun setOrder(categories: List<AppCategory>) {
        _categories.value = categories
        persist()
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
                                ?: "",
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
