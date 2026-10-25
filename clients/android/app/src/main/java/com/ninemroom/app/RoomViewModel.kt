package com.ninemroom.app

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import com.ninemroom.app.data.ConnectionStatus
import com.ninemroom.app.data.RoomMember
import kotlinx.coroutines.flow.StateFlow

class RoomViewModel(application: Application) : AndroidViewModel(application) {
    private val controller = (application as NineRoomApplication).roomController

    val state: StateFlow<RoomUiState> = controller.state

    fun connect(server: String, room: String, name: String) =
        controller.connect(server, room, name)

    fun retry() = controller.retry()

    fun setMode(mode: String) = controller.setMode(mode)

    fun sendChat(text: String) = controller.sendChat(text)

    fun sendPoke(targetId: String) = controller.sendPoke(targetId)

    fun setAvatar(uri: android.net.Uri) = controller.setAvatar(uri)

    fun setAvatar(bitmap: android.graphics.Bitmap) = controller.setAvatar(bitmap)

    fun disconnect() = controller.disconnect()

    fun setKeepOnline(enabled: Boolean) = controller.setKeepOnline(enabled)

    fun setOverlayEnabled(enabled: Boolean) = controller.setOverlayEnabled(enabled)

    fun setOverlayOpacity(value: Float) = controller.setOverlayOpacity(value)

    fun setOverlaySize(value: Int) = controller.setOverlaySize(value)

    fun setOverlayMargin(value: Int) = controller.setOverlayMargin(value)

    fun setNotifyRoomEvents(enabled: Boolean) = controller.setNotifyRoomEvents(enabled)

    fun setNotifyFocusDone(enabled: Boolean) = controller.setNotifyFocusDone(enabled)

    fun setShareApp(enabled: Boolean) = controller.setShareApp(enabled)

    fun setShareMusic(enabled: Boolean) = controller.setShareMusic(enabled)

    fun setMediaWhitelistEnabled(enabled: Boolean) = controller.setMediaWhitelistEnabled(enabled)

    fun setMediaWhitelistPackages(packages: Set<String>) =
        controller.setMediaWhitelistPackages(packages)

    fun setAutoStart(enabled: Boolean) = controller.setAutoStart(enabled)

    fun setAutoConnect(enabled: Boolean) = controller.setAutoConnect(enabled)

    fun setHomeToCurrentRoom() = controller.setHomeToCurrentRoom()

    fun clearHomeRoom() = controller.clearHomeRoom()

    fun refreshPresence() = controller.refreshPresence()

    fun refreshOverlay() = controller.refreshOverlay()
}
