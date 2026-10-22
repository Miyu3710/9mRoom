package com.ninemroom.app.data

import android.content.Context
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import org.json.JSONObject

data class AppRule(
    val packageName: String,
    val category: String? = null,
    val hidden: Boolean = false,
)

class AppRuleStore(context: Context) {
    private val preferences = context.applicationContext
        .getSharedPreferences("nine_room_app_rules", Context.MODE_PRIVATE)
    private val _rules = MutableStateFlow(load())
    val rules: StateFlow<Map<String, AppRule>> = _rules.asStateFlow()

    fun ruleFor(packageName: String): AppRule? = _rules.value[packageName]

    fun setCategory(packageName: String, category: String?) {
        val current = _rules.value[packageName] ?: AppRule(packageName)
        _rules.value = _rules.value + (packageName to current.copy(category = category))
        persist()
    }

    fun setHidden(packageName: String, hidden: Boolean) {
        val current = _rules.value[packageName] ?: AppRule(packageName)
        _rules.value = _rules.value + (packageName to current.copy(hidden = hidden))
        persist()
    }

    private fun persist() {
        val root = JSONObject()
        _rules.value.forEach { (packageName, rule) ->
            root.put(
                packageName,
                JSONObject()
                    .put("category", rule.category ?: JSONObject.NULL)
                    .put("hidden", rule.hidden),
            )
        }
        preferences.edit().putString("rules", root.toString()).apply()
    }

    private fun load(): Map<String, AppRule> {
        val raw = preferences.getString("rules", "").orEmpty()
        if (raw.isBlank()) return emptyMap()
        return try {
            val root = JSONObject(raw)
            buildMap {
                root.keys().forEach { packageName ->
                    val json = root.optJSONObject(packageName) ?: return@forEach
                    put(
                        packageName,
                        AppRule(
                            packageName = packageName,
                            category = json.optString("category").takeIf { it.isNotBlank() },
                            hidden = json.optBoolean("hidden"),
                        ),
                    )
                }
            }
        } catch (_: Exception) {
            emptyMap()
        }
    }
}

val appCategoryLabels = linkedMapOf(
    "coding" to "编程",
    "writing" to "写作",
    "reading" to "阅读",
    "meeting" to "会议",
    "media" to "影音",
    "game" to "游戏",
    "browsing" to "浏览",
    "other" to "其他",
)
