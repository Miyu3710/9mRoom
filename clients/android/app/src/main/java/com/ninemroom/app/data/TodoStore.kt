package com.ninemroom.app.data

import android.content.Context
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import org.json.JSONArray
import org.json.JSONObject

data class TodoItem(
    val id: Long,
    val title: String,
    val subtasks: List<String> = emptyList(),
    val completed: Boolean = false,
    val pinned: Boolean = false,
)

class TodoStore(context: Context) {
    private val preferences = context.applicationContext
        .getSharedPreferences("nine_room_todos", Context.MODE_PRIVATE)
    private val _items = MutableStateFlow(load())
    val items: StateFlow<List<TodoItem>> = _items.asStateFlow()

    init {
        if (!preferences.getBoolean("seeded", false)) {
            preferences.edit().putBoolean("seeded", true).apply()
            if (_items.value.isEmpty()) {
                _items.value = listOf(
                    TodoItem(1, "完善 Android 首页", listOf("房间卡片", "成员横条"), pinned = true),
                    TodoItem(2, "整理同步列表模板"),
                    TodoItem(3, "阅读 Material You 指南"),
                )
                persist()
            }
        }
    }

    fun current(): TodoItem? = _items.value
        .filterNot { it.completed }
        .sortedWith(compareByDescending<TodoItem> { it.pinned }.thenBy { it.id })
        .firstOrNull()

    fun add(title: String) {
        if (title.isBlank()) return
        val id = System.currentTimeMillis()
        _items.value = listOf(TodoItem(id, title.trim())) + _items.value
        persist()
    }

    fun toggle(id: Long) {
        _items.value = _items.value.map {
            if (it.id == id) it.copy(completed = !it.completed) else it
        }
        persist()
    }

    fun togglePin(id: Long) {
        _items.value = _items.value.map {
            if (it.id == id) it.copy(pinned = !it.pinned) else it
        }
        persist()
    }

    fun delete(id: Long) {
        _items.value = _items.value.filterNot { it.id == id }
        persist()
    }

    private fun persist() {
        val array = JSONArray()
        _items.value.forEach { item ->
            array.put(
                JSONObject()
                    .put("id", item.id)
                    .put("title", item.title)
                    .put("completed", item.completed)
                    .put("pinned", item.pinned)
                    .put("subtasks", JSONArray(item.subtasks)),
            )
        }
        preferences.edit().putString("items", array.toString()).apply()
    }

    private fun load(): List<TodoItem> {
        val raw = preferences.getString("items", "").orEmpty()
        if (raw.isBlank()) return emptyList()
        return try {
            val array = JSONArray(raw)
            buildList {
                for (index in 0 until array.length()) {
                    val json = array.optJSONObject(index) ?: continue
                    val subtasks = mutableListOf<String>()
                    val subtaskArray = json.optJSONArray("subtasks")
                    if (subtaskArray != null) {
                        for (subIndex in 0 until subtaskArray.length()) {
                            subtasks += subtaskArray.optString(subIndex)
                        }
                    }
                    add(
                        TodoItem(
                            id = json.optLong("id"),
                            title = json.optString("title"),
                            subtasks = subtasks,
                            completed = json.optBoolean("completed"),
                            pinned = json.optBoolean("pinned"),
                        ),
                    )
                }
            }
        } catch (_: Exception) {
            emptyList()
        }
    }
}
