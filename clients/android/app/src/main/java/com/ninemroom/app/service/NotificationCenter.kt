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
}
