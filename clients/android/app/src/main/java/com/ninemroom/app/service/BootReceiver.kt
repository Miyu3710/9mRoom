package com.ninemroom.app.service

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import androidx.core.content.ContextCompat
import com.ninemroom.app.data.AppSettings

class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action != Intent.ACTION_BOOT_COMPLETED) return
        val settings = AppSettings(context)
        if (!settings.autoStart ||
            !settings.keepOnline ||
            settings.serverUrl.isBlank() ||
            settings.roomCode.isBlank() ||
            settings.displayName.isBlank()
        ) {
            return
        }

        try {
            ContextCompat.startForegroundService(
                context,
                PresenceService.intent(context).putExtra(
                    PresenceService.EXTRA_WAIT_FOR_CONNECTION,
                    true,
                ),
            )
        } catch (_: Exception) {
            // 系统可能限制开机启动前台服务；下一次打开应用时会重新连接
        }
    }
}
