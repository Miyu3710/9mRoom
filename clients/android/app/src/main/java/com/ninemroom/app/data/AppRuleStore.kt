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
    val displayName: String? = null,
    val template: String? = null,
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

    fun setDisplayName(packageName: String, displayName: String?) {
        val current = _rules.value[packageName] ?: AppRule(packageName)
        _rules.value = _rules.value + (
            packageName to current.copy(
                displayName = displayName?.trim()?.takeIf { it.isNotBlank() },
            )
        )
        persist()
    }

    fun setTemplate(packageName: String, template: String?) {
        val current = _rules.value[packageName] ?: AppRule(packageName)
        _rules.value = _rules.value + (
            packageName to current.copy(
                template = template?.trim()?.takeIf { it.isNotBlank() },
            )
        )
        persist()
    }

    fun clearCategory(categoryId: String) {
        _rules.value = _rules.value.mapValues { (_, rule) ->
            if (rule.category == categoryId) rule.copy(category = null) else rule
        }
        persist()
    }

    private fun persist() {
        val root = JSONObject()
        _rules.value.forEach { (packageName, rule) ->
            root.put(
                packageName,
                JSONObject()
                    .put("category", rule.category ?: JSONObject.NULL)
                    .put("hidden", rule.hidden)
                    .put("displayName", rule.displayName ?: JSONObject.NULL)
                    .put("template", rule.template ?: JSONObject.NULL),
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
                            displayName = json.optString("displayName").takeIf { it.isNotBlank() },
                            template = json.optString("template").takeIf { it.isNotBlank() },
                        ),
                    )
                }
            }
        } catch (_: Exception) {
            emptyMap()
        }
    }
}
