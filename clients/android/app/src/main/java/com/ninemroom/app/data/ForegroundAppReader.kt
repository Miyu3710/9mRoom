package com.ninemroom.app.data

import android.app.AppOpsManager
import android.app.usage.UsageEvents
import android.app.usage.UsageStatsManager
import android.content.Context
import android.content.Intent
import android.os.Process

data class ForegroundApp(
    val packageName: String,
    val id: String,
    val name: String,
    val category: String,
)

private fun app(id: String, name: String, category: String) = ForegroundApp("", id, name, category)

private val androidApps = mapOf(
    "com.microsoft.office.word" to app("word", "Microsoft Word", "writing"),
    "com.microsoft.office.onenote" to app("onenote", "OneNote", "writing"),
    "notion.id" to app("notion", "Notion", "writing"),
    "us.zoom.videomeetings" to app("zoom", "Zoom", "meeting"),
    "com.microsoft.teams" to app("teams", "Microsoft Teams", "meeting"),
    "com.tencent.wemeet.app" to app("wemeet", "腾讯会议", "meeting"),
    "com.alibaba.android.rimet" to app("dingtalk", "钉钉", "meeting"),
    "com.ss.android.lark" to app("feishu", "飞书", "meeting"),
    "org.videolan.vlc" to app("vlc", "VLC", "media"),
    "com.spotify.music" to app("spotify", "Spotify", "media"),
    "com.netease.cloudmusic" to app("cloudmusic", "网易云音乐", "media"),
    "com.tencent.qqmusic" to app("qqmusic", "QQ 音乐", "media"),
    "tv.danmaku.bili" to app("bilibili", "哔哩哔哩", "media"),
    "com.google.android.youtube" to app("youtube", "YouTube", "media"),
    "com.frameyou.player" to app("syplayer", "SYPlayer", "media"),
    "com.valvesoftware.android.steam.community" to app("steam", "Steam", "game"),
    "com.miHoYo.Yuanshen" to app("genshin", "原神", "game"),
    "com.android.chrome" to app("chrome", "Chrome", "browsing"),
    "com.microsoft.emmx" to app("edge", "Microsoft Edge", "browsing"),
    "org.mozilla.firefox" to app("firefox", "Firefox", "browsing"),
    "com.tencent.mm" to app("wechat", "微信", "other"),
    "com.tencent.mobileqq" to app("qq", "QQ", "other"),
    "com.tencent.tim" to app("tim", "TIM", "other"),
    "org.telegram.messenger" to app("telegram", "Telegram", "other"),
    "com.discord" to app("discord", "Discord", "other"),
    "com.ninemroom.app" to app("9mroom", "9mRoom", "other"),
)

class ForegroundAppReader(private val context: Context) {
    private var lastApp: ForegroundApp? = null

    private val launcherPackage: String? by lazy {
        val intent = Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_HOME)
        context.packageManager.resolveActivity(intent, 0)?.activityInfo?.packageName
    }

    private fun isLauncher(packageName: String): Boolean {
        if (packageName == launcherPackage) return true
        val lower = packageName.lowercase()
        return lower.contains("launcher") ||
            lower == "com.android.launcher3" ||
            lower == "com.oplus.launcher" ||
            lower == "com.miui.home"
    }

    fun hasPermission(): Boolean {
        val appOps = context.getSystemService(AppOpsManager::class.java)
        val mode = appOps.checkOpNoThrow(
            AppOpsManager.OPSTR_GET_USAGE_STATS,
            Process.myUid(),
            context.packageName,
        )
        return mode == AppOpsManager.MODE_ALLOWED
    }

    fun current(): ForegroundApp? {
        if (!hasPermission()) return null
        val manager = context.getSystemService(UsageStatsManager::class.java)
        val end = System.currentTimeMillis()
        val events = manager.queryEvents(end - 60_000, end)
        val event = UsageEvents.Event()
        var packageName: String? = null
        while (events.hasNextEvent()) {
            events.getNextEvent(event)
            if (event.eventType == UsageEvents.Event.MOVE_TO_FOREGROUND) {
                packageName = event.packageName
            }
        }

        if (packageName == null) {
            packageName = lastApp?.packageName ?: latestUsedPackage(manager, end)
        }
        val name = packageName ?: return lastApp
        return resolve(name).also { lastApp = it }
    }

    private fun latestUsedPackage(manager: UsageStatsManager, end: Long): String? {
        return runCatching {
            manager.queryUsageStats(
                UsageStatsManager.INTERVAL_DAILY,
                end - 24 * 60 * 60 * 1000,
                end,
            ).filter { it.lastTimeUsed > 0 }
                .maxByOrNull { it.lastTimeUsed }
                ?.packageName
        }.getOrNull()
    }

    private fun resolve(name: String): ForegroundApp {
        if (isLauncher(name)) {
            return ForegroundApp(
                packageName = name,
                id = "desktop",
                name = "桌面",
                category = "other",
            )
        }
        val mapped = androidApps[name]
        if (mapped != null) {
            return mapped.copy(packageName = name)
        }

        val label = runCatching {
            val info = context.packageManager.getApplicationInfo(name, 0)
            context.packageManager.getApplicationLabel(info).toString()
        }.getOrDefault("其他应用")

        return ForegroundApp(
            packageName = name,
            id = "other",
            name = label,
            category = "other",
        )
    }
}
