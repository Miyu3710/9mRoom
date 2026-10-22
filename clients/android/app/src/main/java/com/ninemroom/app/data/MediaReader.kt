package com.ninemroom.app.data

import android.content.ComponentName
import android.content.Context
import android.media.MediaMetadata
import android.media.session.MediaController
import android.media.session.MediaSessionManager
import android.media.session.PlaybackState

data class MediaInfo(
    val title: String,
    val artist: String,
    val isPlaying: Boolean,
    val positionMs: Long,
    val durationMs: Long,
)

class MediaReader(private val context: Context) {
    fun isEnabled(): Boolean {
        val enabled = android.provider.Settings.Secure.getString(
            context.contentResolver,
            "enabled_notification_listeners",
        ).orEmpty()
        return enabled.contains(context.packageName)
    }

    fun current(): MediaInfo? {
        if (!isEnabled()) return null
        return try {
            val manager = context.getSystemService(MediaSessionManager::class.java)
            val component = ComponentName(context, com.ninemroom.app.service.MediaListenerService::class.java)
            val sessions = manager.getActiveSessions(component)
            val session = sessions.firstOrNull { controller ->
                controller.playbackState?.state == PlaybackState.STATE_PLAYING
            } ?: sessions.firstOrNull() ?: return null
            val metadata = session.metadata ?: return null
            val title = metadata.getString(MediaMetadata.METADATA_KEY_TITLE).orEmpty()
            if (title.isBlank()) return null
            val position = session.playbackState?.position ?: 0L
            val duration = metadata.getLong(MediaMetadata.METADATA_KEY_DURATION)
            MediaInfo(
                title = title,
                artist = metadata.getString(MediaMetadata.METADATA_KEY_ARTIST).orEmpty(),
                isPlaying = session.playbackState?.state == PlaybackState.STATE_PLAYING,
                positionMs = position,
                durationMs = duration,
            )
        } catch (_: Exception) {
            null
        }
    }
}
