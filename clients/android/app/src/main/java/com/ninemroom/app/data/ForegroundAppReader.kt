package com.ninemroom.app.data

import android.app.AppOpsManager
import android.app.usage.UsageEvents
import android.app.usage.UsageStatsManager
import android.content.Context
import android.os.Process

data class ForegroundApp(
    val id: String,
    val name: String,
    val category: String,
)

private val androidApps = mapOf(
    "com.microsoft.office.word" to ForegroundApp("word", "Microsoft Word", "writing"),
    "com.microsoft.office.onenote" to ForegroundApp("onenote", "OneNote", "writing"),
    "notion.id" to ForegroundApp("notion", "Notion", "writing"),
    "us.zoom.videomeetings" to ForegroundApp("zoom", "Zoom", "meeting"),
    "com.microsoft.teams" to ForegroundApp("teams", "Microsoft Teams", "meeting"),
    "com.tencent.wemeet.app" to ForegroundApp("wemeet", "腾讯会议", "meeting"),
    "com.alibaba.android.rimet" to ForegroundApp("dingtalk", "钉钉", "meeting"),
    "com.ss.android.lark" to ForegroundApp("feishu", "飞书", "meeting"),
    "org.videolan.vlc" to ForegroundApp("vlc", "VLC", "media"),
    "com.spotify.music" to ForegroundApp("spotify", "Spotify", "media"),
    "com.netease.cloudmusic" to ForegroundApp("cloudmusic", "网易云音乐", "media"),
    "com.tencent.qqmusic" to ForegroundApp("qqmusic", "QQ 音乐", "media"),
    "tv.danmaku.bili" to ForegroundApp("bilibili", "哔哩哔哩", "media"),
    "com.google.android.youtube" to ForegroundApp("youtube", "YouTube", "media"),
    "com.frameyou.player" to ForegroundApp("syplayer", "SYPlayer", "media"),
    "com.valvesoftware.android.steam.community" to ForegroundApp("steam", "Steam", "game"),
    "com.miHoYo.Yuanshen" to ForegroundApp("genshin", "原神", "game"),
    "com.android.chrome" to ForegroundApp("chrome", "Chrome", "browsing"),
    "com.microsoft.emmx" to ForegroundApp("edge", "Microsoft Edge", "browsing"),
    "org.mozilla.firefox" to ForegroundApp("firefox", "Firefox", "browsing"),
    "com.tencent.mm" to ForegroundApp("wechat", "微信", "other"),
    "com.tencent.mobileqq" to ForegroundApp("qq", "QQ", "other"),
    "com.tencent.tim" to ForegroundApp("tim", "TIM", "other"),
    "org.telegram.messenger" to ForegroundApp("telegram", "Telegram", "other"),
    "com.discord" to ForegroundApp("discord", "Discord", "other"),
    "com.ninemroom.app" to ForegroundApp("9mroom", "9mRoom", "other"),
)

class ForegroundAppReader(private val context: Context) {
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

        val name = packageName ?: return null
        return androidApps[name] ?: ForegroundApp("other", "其他应用", "other")
    }
}
