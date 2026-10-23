package com.ninemroom.app.service

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import com.ninemroom.app.MainActivity
import com.ninemroom.app.R
import com.ninemroom.app.RoomUiState
import com.ninemroom.app.data.modeLabel

object NotificationCenter {
    const val CHANNEL_PRESENCE = "presence"
    const val CHANNEL_ROOM_EVENTS = "room_events"
    const val CHANNEL_FOCUS = "focus"
    const val CHANNEL_POKES = "pokes"
    const val CHANNEL_MESSAGES = "messages"
    const val PRESENCE_ID = 1001

    fun ensureChannels(context: Context) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val manager = context.getSystemService(NotificationManager::class.java)
        manager.createNotificationChannel(
            NotificationChannel(
                CHANNEL_PRESENCE,
                "保持在线",
                NotificationManager.IMPORTANCE_LOW,
            ).apply {
                description = "保持房间在线时显示的通知"
                setShowBadge(false)
            },
        )
        manager.createNotificationChannel(
            NotificationChannel(
                CHANNEL_ROOM_EVENTS,
                "房间成员事件",
                NotificationManager.IMPORTANCE_DEFAULT,
            ).apply {
                description = "成员进入或离开房间"
            },
        )
        manager.createNotificationChannel(
            NotificationChannel(
                CHANNEL_FOCUS,
                "专注提醒",
                NotificationManager.IMPORTANCE_DEFAULT,
            ).apply {
                description = "专注计时结束"
            },
        )
        manager.createNotificationChannel(
            NotificationChannel(
                CHANNEL_POKES,
                "戳一下",
                NotificationManager.IMPORTANCE_HIGH,
            ).apply {
                description = "有人戳你一下"
                enableVibration(true)
                enableLights(true)
            },
        )
        manager.createNotificationChannel(
            NotificationChannel(
                CHANNEL_MESSAGES,
                "聊天消息",
                NotificationManager.IMPORTANCE_DEFAULT,
            ).apply {
                description = "房间内收到新的聊天消息"
                enableVibration(true)
            },
        )
    }

    fun presence(context: Context, state: RoomUiState): Notification {
        val mode = state.self?.modeLabel ?: modeLabel(state.selfMode)
        val title = state.roomName.ifBlank { "9mRoom" }
        val text = "${state.onlineCount} 人在线 · $mode"
        val openIntent = PendingIntent.getActivity(
            context,
            0,
            Intent(context, MainActivity::class.java),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )

        return NotificationCompat.Builder(context, CHANNEL_PRESENCE)
            .setSmallIcon(R.drawable.ic_notification)
            .setContentTitle(title)
            .setContentText(text)
            .setContentIntent(openIntent)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setCategory(NotificationCompat.CATEGORY_SERVICE)
            .setForegroundServiceBehavior(NotificationCompat.FOREGROUND_SERVICE_IMMEDIATE)
            .build()
    }

    fun updatePresence(context: Context, state: RoomUiState) {
        if (!NotificationManagerCompat.from(context).areNotificationsEnabled()) return
        NotificationManagerCompat.from(context).notify(PRESENCE_ID, presence(context, state))
    }

    fun roomEvent(context: Context, text: String) {
        if (!NotificationManagerCompat.from(context).areNotificationsEnabled()) return
        ensureChannels(context)
        val openIntent = PendingIntent.getActivity(
            context,
            1,
            Intent(context, MainActivity::class.java),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        val notification = NotificationCompat.Builder(context, CHANNEL_ROOM_EVENTS)
            .setSmallIcon(R.drawable.ic_notification)
            .setContentTitle("9mRoom")
            .setContentText(text)
            .setContentIntent(openIntent)
            .setAutoCancel(true)
            .setCategory(NotificationCompat.CATEGORY_SOCIAL)
            .build()
        NotificationManagerCompat.from(context)
            .notify(text.hashCode(), notification)
    }

    fun focusDone(context: Context, text: String = "这一轮完成了，休息一下。") {
        if (!NotificationManagerCompat.from(context).areNotificationsEnabled()) return
        ensureChannels(context)
        val openIntent = PendingIntent.getActivity(
            context,
            2,
            Intent(context, MainActivity::class.java),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        val notification = NotificationCompat.Builder(context, CHANNEL_FOCUS)
            .setSmallIcon(R.drawable.ic_notification)
            .setContentTitle("专注结束")
            .setContentText(text)
            .setContentIntent(openIntent)
            .setAutoCancel(true)
            .setCategory(NotificationCompat.CATEGORY_REMINDER)
            .build()
        NotificationManagerCompat.from(context)
            .notify(2001, notification)
    }

    fun breakDone(context: Context, text: String = "准备好开始下一轮专注了吗？") {
        if (!NotificationManagerCompat.from(context).areNotificationsEnabled()) return
        ensureChannels(context)
        val openIntent = PendingIntent.getActivity(
            context,
            5,
            Intent(context, MainActivity::class.java),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        val notification = NotificationCompat.Builder(context, CHANNEL_FOCUS)
            .setSmallIcon(R.drawable.ic_notification)
            .setContentTitle("休息结束")
            .setContentText(text)
            .setContentIntent(openIntent)
            .setAutoCancel(true)
            .setCategory(NotificationCompat.CATEGORY_REMINDER)
            .build()
        NotificationManagerCompat.from(context)
            .notify(2002, notification)
    }

    fun poke(context: Context, fromName: String) {
        if (!NotificationManagerCompat.from(context).areNotificationsEnabled()) return
        ensureChannels(context)
        val openIntent = PendingIntent.getActivity(
            context,
            3,
            Intent(context, MainActivity::class.java),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        val text = "${fromName.ifBlank { "有人" }} 戳了你一下！"
        val notification = NotificationCompat.Builder(context, CHANNEL_POKES)
            .setSmallIcon(R.drawable.ic_notification)
            .setContentTitle("9mRoom")
            .setContentText(text)
            .setStyle(NotificationCompat.BigTextStyle().bigText(text))
            .setContentIntent(openIntent)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setDefaults(NotificationCompat.DEFAULT_ALL)
            .setAutoCancel(true)
            .setCategory(NotificationCompat.CATEGORY_SOCIAL)
            .build()
        NotificationManagerCompat.from(context)
            .notify(3000 + (fromName.hashCode() and 0x7fffffff) % 1000, notification)
    }

    fun chat(context: Context, author: String, text: String) {
        if (!NotificationManagerCompat.from(context).areNotificationsEnabled()) return
        val body = text.trim()
        if (body.isBlank()) return
        ensureChannels(context)
        val openIntent = PendingIntent.getActivity(
            context,
            4,
            Intent(context, MainActivity::class.java),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        val notification = NotificationCompat.Builder(context, CHANNEL_MESSAGES)
            .setSmallIcon(R.drawable.ic_notification)
            .setContentTitle(author.ifBlank { "房间消息" })
            .setContentText(body)
            .setStyle(NotificationCompat.BigTextStyle().bigText(body))
            .setContentIntent(openIntent)
            .setPriority(NotificationCompat.PRIORITY_DEFAULT)
            .setDefaults(NotificationCompat.DEFAULT_ALL)
            .setAutoCancel(true)
            .setCategory(NotificationCompat.CATEGORY_MESSAGE)
            .build()
        NotificationManagerCompat.from(context)
            .notify(4000 + (body.hashCode() and 0x7fffffff) % 1000, notification)
    }
}
